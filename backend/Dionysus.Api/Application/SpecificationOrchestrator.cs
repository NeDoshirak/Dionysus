using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class SpecificationOrchestrator(
    AppDbContext db,
    IStructuredSpecificationAiService ai,
    ISpecificationContractValidator validator,
    ILogger<SpecificationOrchestrator>? logger = null) : ISpecificationAnalysisOrchestrator
{
    private readonly ILogger<SpecificationOrchestrator> _logger = logger ?? NullLogger<SpecificationOrchestrator>.Instance;
    public async Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct)
    {
        var analysis = await LoadAsync(job.AnalysisId, ct);
        if (analysis is null || analysis.RunId != job.RunId || analysis.Status == SpecificationAnalysisStatus.Completed)
            return;

        var stage = "stage0";
        try
        {
            var recording = analysis.Project.Recordings.SingleOrDefault(recording => recording.IsCurrent)
                ?? throw new InvalidOperationException("Current recording is missing.");
            var segments = recording.Segments.OrderBy(segment => segment.StartSeconds).ThenBy(segment => segment.Id).ToList();

            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage0, null, ct)) return;
            var stage0Request = new Stage0CleanupRequest("1.0", segments.Select(ToStageSegment).ToList());
            var stage0 = await ai.RunStage0Async(stage0Request, ct);
            validator.ValidateStage0(stage0Request, stage0);
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage0, () =>
            {
                analysis.Stage0RawResponse = Serialize(stage0);
                var cleaned = stage0.Segments.ToDictionary(segment => segment.SegmentId);
                foreach (var segment in segments) segment.CleanedText = cleaned[segment.Id].CleanedText;
            }, ct)) return;

            stage = "final";
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage3, null, ct)) return;
            var finalRequest = new FinalSpecificationRequest(
                "1.0",
                segments.Select(segment => new StageSegmentDto(
                    segment.Id,
                    segment.StartSeconds,
                    segment.EndSeconds,
                    segment.CleanedText ?? segment.Text)).ToList());
            var final = await ai.RunFinalSpecificationAsync(finalRequest, ct);
            validator.ValidateFinal(finalRequest, final);
            await PublishFinalAsync(analysis, job, segments, final, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(job, stage, exception);
        }
    }

    private async Task<SpecificationAnalysis?> LoadAsync(Guid analysisId, CancellationToken ct) =>
        await db.SpecificationAnalyses
            .Include(x => x.Project).ThenInclude(x => x.Recordings.Where(recording => recording.IsCurrent)).ThenInclude(x => x.Segments)
            .Include(x => x.Topics)
            .Include(x => x.Statements).ThenInclude(x => x.SegmentLinks)
            .Include(x => x.Relations).ThenInclude(x => x.SourceStatementLinks)
            .Include(x => x.Relations).ThenInclude(x => x.TargetStatementLinks)
            .SingleOrDefaultAsync(x => x.Id == analysisId, ct);

    private async Task<bool> MutateAsync(
        SpecificationAnalysis analysis,
        SpecificationAnalysisJob job,
        SpecificationAnalysisStatus status,
        Action? mutation,
        CancellationToken ct)
    {
        var current = await db.SpecificationAnalyses.AsNoTracking()
            .Where(x => x.Id == job.AnalysisId)
            .Select(x => new { x.RunId, x.Status })
            .SingleOrDefaultAsync(ct);
        if (current is null || current.RunId != job.RunId || current.Status == SpecificationAnalysisStatus.Completed) return false;

        analysis.RunId = current.RunId;
        analysis.Status = status;
        mutation?.Invoke();
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task MarkFailedAsync(SpecificationAnalysisJob job, string stage, Exception exception)
    {
        db.ChangeTracker.Clear();
        var analysis = await db.SpecificationAnalyses.SingleOrDefaultAsync(x => x.Id == job.AnalysisId);
        if (analysis is null || analysis.RunId != job.RunId || analysis.Status == SpecificationAnalysisStatus.Completed) return;
        analysis.Status = SpecificationAnalysisStatus.Failed;
        var (topicId, cause) = exception is Stage3TopicException topicException
            ? (topicException.TopicId, topicException.InnerException ?? topicException)
            : ((string?)null, exception);
        var diagnosticCode = DiagnosticCode(cause);
        analysis.Error = topicId is null
            ? $"{stage} failed: {diagnosticCode}"
            : $"{stage} failed: {topicId}/{diagnosticCode}";
        _logger.LogError(
            "Specification analysis failed. AnalysisId: {AnalysisId}; Stage: {Stage}; TopicId: {TopicId}; FailureType: {FailureType}; DiagnosticCode: {DiagnosticCode}",
            job.AnalysisId,
            stage,
            topicId,
            cause.GetType().Name,
            diagnosticCode);
        await db.SaveChangesAsync();
    }

    private static string DiagnosticCode(Exception exception) => exception switch
    {
        SpecificationContractException contractException => $"{contractException.Stage}/{contractException.Rule}",
        SpecificationAiInfrastructureException infrastructureException => infrastructureException.DiagnosticCode,
        _ => exception.GetType().Name
    };

    private async Task<IReadOnlyList<Stage3Result>> RunStage3Async(SpecificationAnalysis analysis, CancellationToken ct)
    {
        var context = analysis.Statements
            .Where(statement => statement.IsBusinessContext)
            .OrderBy(statement => statement.ExternalId, StringComparer.Ordinal)
            .Select(ToBusinessContext)
            .ToList();
        var statementsById = analysis.Statements.ToDictionary(statement => statement.ExternalId, StringComparer.Ordinal);
        var relations = analysis.Relations.Select(ToRelation).ToList();
        var topics = analysis.Topics
            .Where(topic => topic.Statements.Any(statement => statement.Status is AnalysisStatementStatus.Active or AnalysisStatementStatus.Unresolved))
            .OrderBy(topic => TopicOrder(topic.ExternalId))
            .ThenBy(topic => topic.ExternalId, StringComparer.Ordinal)
            .ToList();

        using var gate = new SemaphoreSlim(4, 4);
        var tasks = topics.Select(async topic =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var topicStatements = topic.Statements
                    .Where(statement => statement.Status is AnalysisStatementStatus.Active or AnalysisStatementStatus.Unresolved)
                    .OrderBy(statement => statement.ExternalId, StringComparer.Ordinal)
                    .Select(ToStatement)
                    .ToList();
                var topicStatementIds = topicStatements.Select(statement => statement.Id).ToHashSet(StringComparer.Ordinal);
                var topicRelations = relations
                    .Where(relation => relation.SourceStatementIds.Concat(relation.TargetStatementIds).All(topicStatementIds.Contains))
                    .ToList();
                var request = new Stage3FunctionRequest(
                    "1.0",
                    context,
                    new Stage3FunctionInputDto(topic.ExternalId, topic.Name, topicStatements, topicRelations));
                var response = await ai.RunStage3Async(request, ct);
                validator.ValidateStage3(request, response);
                return new Stage3Result(topic, response);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new Stage3TopicException(topic.ExternalId, exception);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        return await Task.WhenAll(tasks);

        StageBusinessContextDto ToBusinessContext(AnalysisStatement statement) =>
            new(statement.ExternalId, statement.Text, statement.SegmentLinks.Select(link => link.TranscriptSegmentId).ToList());

        Stage2StatementDto ToStatement(AnalysisStatement statement) =>
            new(statement.ExternalId, statement.Text, statement.Status, statement.SegmentLinks.Select(link => link.TranscriptSegmentId).ToList());

        StageRelationDto ToRelation(AnalysisRelation relation) =>
            new(
                relation.ExternalId,
                relation.Type,
                relation.SourceStatementLinks.Select(link => statementsById.Values.Single(statement => statement.Id == link.AnalysisStatementId).ExternalId).ToList(),
                relation.TargetStatementLinks.Select(link => statementsById.Values.Single(statement => statement.Id == link.AnalysisStatementId).ExternalId).ToList(),
                relation.Reason ?? string.Empty);
    }

    private async Task PublishFinalAsync(
        SpecificationAnalysis analysis,
        SpecificationAnalysisJob job,
        IReadOnlyList<TranscriptSegment> segments,
        FinalSpecificationResponse response,
        CancellationToken ct)
    {
        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var statementsBySegmentId = new Dictionary<Guid, AnalysisStatement>();
            foreach (var segment in segments)
            {
                var statement = new AnalysisStatement
                {
                    SpecificationAnalysisId = analysis.Id,
                    ExternalId = $"segment-{segment.Id:N}",
                    Text = segment.CleanedText ?? segment.Text,
                    Status = AnalysisStatementStatus.Active
                };
                statement.SegmentLinks.Add(new AnalysisStatementSegment
                {
                    AnalysisStatementId = statement.Id,
                    TranscriptSegmentId = segment.Id
                });
                analysis.Statements.Add(statement);
                db.Add(statement);
                db.AddRange(statement.SegmentLinks);
                statementsBySegmentId.Add(segment.Id, statement);
            }

            var function = new SpecificationFunction
            {
                SpecificationAnalysisId = analysis.Id,
                Title = response.Title,
                Description = response.Description,
                SortOrder = 0
            };
            foreach (var statement in statementsBySegmentId.Values)
            {
                function.StatementLinks.Add(new SpecificationFunctionStatement
                {
                    SpecificationFunctionId = function.Id,
                    AnalysisStatementId = statement.Id
                });
            }
            analysis.Functions.Add(function);
            db.Add(function);
            db.AddRange(function.StatementLinks);

            var sortOrder = 0;
            AddItems(response.BusinessContext.Select(item => (SpecificationItemKind.BusinessContext, (string?)item.Id, item.Text, (string?)null, item.SourceSegmentIds)));
            AddItems(response.Roles.Select(item => (SpecificationItemKind.Role, (string?)item.Name, item.Description, (string?)null, item.SourceSegmentIds)));
            AddItems(response.FunctionalRequirements.Select(item => (SpecificationItemKind.FunctionalRequirement, (string?)item.Title, item.Description, (string?)item.Priority.ToString(), item.SourceSegmentIds)));
            AddItems(response.UserScenarios.Select(item => (SpecificationItemKind.UserScenario, (string?)item.Title, $"{item.Actor}: {item.Description}", (string?)null, item.SourceSegmentIds)));
            AddItems(response.Constraints.Select(item => (SpecificationItemKind.Constraint, (string?)null, item.Description, (string?)null, item.SourceSegmentIds)));
            AddItems(response.Conditions.Select(item => (SpecificationItemKind.Condition, (string?)null, item.Description, (string?)null, item.SourceSegmentIds)));
            AddItems(response.Agreements.Select(item => (SpecificationItemKind.Agreement, (string?)null, item.Description, (string?)null, item.SourceSegmentIds)));
            AddItems(response.KeyQuestions.Select(item => (SpecificationItemKind.KeyQuestion, (string?)item.Title, item.Description, (string?)item.Reason.ToString(), item.SourceSegmentIds)));

            analysis.Status = SpecificationAnalysisStatus.Completed;
            analysis.CompletedAt = DateTimeOffset.UtcNow;
            analysis.Error = null;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);

            void AddItems(IEnumerable<(SpecificationItemKind Kind, string? Title, string Description, string? Priority, IReadOnlyList<Guid> SourceSegmentIds)> items)
            {
                foreach (var source in items)
                {
                    var item = new SpecificationItem
                    {
                        SpecificationAnalysisId = analysis.Id,
                        SpecificationFunctionId = function.Id,
                        Kind = source.Kind,
                        Title = source.Title,
                        Description = source.Description,
                        Priority = source.Priority,
                        SortOrder = sortOrder++,
                        IsManual = false
                    };
                    foreach (var segmentId in source.SourceSegmentIds)
                    {
                        var statement = statementsBySegmentId[segmentId];
                        item.StatementLinks.Add(new SpecificationItemStatement
                        {
                            SpecificationItemId = item.Id,
                            AnalysisStatementId = statement.Id
                        });
                    }
                    function.Items.Add(item);
                    db.Add(item);
                    db.AddRange(item.StatementLinks);
                }
            }
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task PublishStage3Async(
        SpecificationAnalysis analysis,
        SpecificationAnalysisJob job,
        IReadOnlyList<Stage3Result> results,
        CancellationToken ct)
    {
        var statementsByExternalId = analysis.Statements.ToDictionary(statement => statement.ExternalId, StringComparer.Ordinal);
        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var sortOrder = 0;
            var businessContext = analysis.Statements
                .Where(statement => statement.IsBusinessContext)
                .OrderBy(statement => statement.ExternalId, StringComparer.Ordinal)
                .ToList();
            if (businessContext.Count > 0)
            {
                var item = new SpecificationItem
                {
                    SpecificationAnalysisId = analysis.Id,
                    Kind = SpecificationItemKind.BusinessContext,
                    Description = string.Join("\n", businessContext.Select(statement => statement.Text)),
                    SortOrder = sortOrder++,
                    IsManual = false
                };
                foreach (var statement in businessContext)
                    item.StatementLinks.Add(new SpecificationItemStatement { SpecificationItemId = item.Id, AnalysisStatementId = statement.Id });
                db.Add(item);
                db.AddRange(item.StatementLinks);
            }

            foreach (var result in results.OrderBy(result => TopicOrder(result.Topic.ExternalId)).ThenBy(result => result.Topic.ExternalId, StringComparer.Ordinal))
            {
                var function = new SpecificationFunction
                {
                    SpecificationAnalysisId = analysis.Id,
                    AnalysisTopicId = result.Topic.Id,
                    Title = result.Response.Function.Title,
                    Description = result.Response.Function.Description,
                    SortOrder = sortOrder++
                };
                AddStatementLinks(function, result.Response.Function.SourceStatementIds);
                db.Add(function);
                AddItems(function, result.Response);
            }

            foreach (var relation in analysis.Relations
                         .Where(relation => relation.Type == AnalysisRelationType.Contradicts)
                         .OrderBy(relation => relation.ExternalId, StringComparer.Ordinal))
            {
                var sourceIds = relation.SourceStatementLinks.Select(link => link.AnalysisStatementId)
                    .Concat(relation.TargetStatementLinks.Select(link => link.AnalysisStatementId))
                    .Distinct()
                    .ToList();
                var item = new SpecificationItem
                {
                    SpecificationAnalysisId = analysis.Id,
                    Kind = SpecificationItemKind.ProjectContradiction,
                    Title = "Contradiction",
                    Description = relation.Reason ?? "Statements contradict.",
                    SortOrder = sortOrder++,
                    IsManual = false
                };
                foreach (var statementId in sourceIds)
                    item.StatementLinks.Add(new SpecificationItemStatement { SpecificationItemId = item.Id, AnalysisStatementId = statementId });
                db.Add(item);
                db.AddRange(item.StatementLinks);
            }

            analysis.Status = SpecificationAnalysisStatus.Completed;
            analysis.CompletedAt = DateTimeOffset.UtcNow;
            analysis.Error = null;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        void AddItems(SpecificationFunction function, Stage3FunctionResponse response)
        {
            Add(response.Roles.Select(item => (SpecificationItemKind.Role, (string?)item.Name, item.Description, (string?)null, item.SourceStatementIds)));
            Add(response.FunctionalRequirements.Select(item => (Kind: SpecificationItemKind.FunctionalRequirement, Title: (string?)item.Title, item.Description, Priority: (string?)item.Priority.ToString(), SourceIds: item.SourceStatementIds)));
            Add(response.UserScenarios.Select(item => (SpecificationItemKind.UserScenario, (string?)item.Title, $"{item.Actor}: {item.Description}", (string?)null, item.SourceStatementIds)));
            Add(response.Constraints.Select(item => (SpecificationItemKind.Constraint, (string?)null, item.Description, (string?)null, item.SourceStatementIds)));
            Add(response.Conditions.Select(item => (SpecificationItemKind.Condition, (string?)null, item.Description, (string?)null, item.SourceStatementIds)));
            Add(response.Agreements.Select(item => (SpecificationItemKind.Agreement, (string?)null, item.Description, (string?)null, item.SourceStatementIds)));
            Add(response.KeyQuestions.Select(item => (Kind: SpecificationItemKind.KeyQuestion, Title: (string?)item.Title, item.Description, Priority: (string?)item.Reason.ToString(), SourceIds: item.SourceStatementIds)));

            void Add(IEnumerable<(SpecificationItemKind Kind, string? Title, string Description, string? Priority, IReadOnlyList<string> SourceIds)> items)
            {
                foreach (var source in items)
                {
                    var item = new SpecificationItem
                    {
                        SpecificationAnalysisId = analysis.Id,
                        SpecificationFunctionId = function.Id,
                        Kind = source.Kind,
                        Title = source.Title,
                        Description = source.Description,
                        Priority = source.Priority,
                        SortOrder = function.Items.Count,
                        IsManual = false
                    };
                    foreach (var sourceId in source.SourceIds)
                        item.StatementLinks.Add(new SpecificationItemStatement { SpecificationItemId = item.Id, AnalysisStatementId = statementsByExternalId[sourceId].Id });
                    function.Items.Add(item);
                    db.Add(item);
                    db.AddRange(item.StatementLinks);
                }
            }
        }

        void AddStatementLinks(SpecificationFunction function, IReadOnlyList<string> sourceIds)
        {
            foreach (var sourceId in sourceIds)
                function.StatementLinks.Add(new SpecificationFunctionStatement { SpecificationFunctionId = function.Id, AnalysisStatementId = statementsByExternalId[sourceId].Id });
            db.AddRange(function.StatementLinks);
        }
    }

    private sealed record Stage3Result(AnalysisTopic Topic, Stage3FunctionResponse Response);

    private sealed class Stage3TopicException(string topicId, Exception innerException) : Exception("Stage 3 topic failed.", innerException)
    {
        public string TopicId { get; } = topicId;
    }

    private static int TopicOrder(string externalId) =>
        int.TryParse(externalId.AsSpan("topic-".Length), out var value) ? value : int.MaxValue;

    private void PersistStage1(SpecificationAnalysis analysis, Stage1ExtractionResponse response)
    {
        foreach (var context in response.BusinessContext)
        {
            var statement = new AnalysisStatement
            {
                SpecificationAnalysisId = analysis.Id,
                ExternalId = context.Id,
                Text = context.Text,
                IsBusinessContext = true
            };
            AddSegmentLinks(statement, context.SourceSegmentIds);
            analysis.Statements.Add(statement);
            db.Add(statement);
            db.AddRange(statement.SegmentLinks);
        }

        foreach (var topicDto in response.Topics)
        {
            var topic = new AnalysisTopic { SpecificationAnalysisId = analysis.Id, ExternalId = topicDto.Id, Name = topicDto.Name };
            analysis.Topics.Add(topic);
            db.Add(topic);
            foreach (var statementDto in topicDto.Statements)
            {
                var statement = new AnalysisStatement
                {
                    SpecificationAnalysisId = analysis.Id,
                    AnalysisTopicId = topic.Id,
                    ExternalId = statementDto.Id,
                    Text = statementDto.Text
                };
                AddSegmentLinks(statement, statementDto.SourceSegmentIds);
                topic.Statements.Add(statement);
                analysis.Statements.Add(statement);
                db.Add(statement);
                db.AddRange(statement.SegmentLinks);
            }
        }

        void AddSegmentLinks(AnalysisStatement statement, IReadOnlyList<Guid> segmentIds)
        {
            foreach (var segmentId in segmentIds)
                statement.SegmentLinks.Add(new AnalysisStatementSegment { AnalysisStatementId = statement.Id, TranscriptSegmentId = segmentId });
        }
    }

    private void PersistStage2(SpecificationAnalysis analysis, Stage2ReviewResponse response)
    {
        var existingTopics = analysis.Topics
            .Select(topic => (Topic: topic, OriginalExternalId: topic.ExternalId))
            .ToList();
        var statements = analysis.Statements.ToDictionary(statement => statement.ExternalId, StringComparer.Ordinal);
        var topicAssignments = response.Topics
            .Select(topic => (Topic: topic, StatementIds: topic.StatementIds.ToHashSet(StringComparer.Ordinal)))
            .ToList();

        foreach (var existing in existingTopics)
            existing.Topic.ExternalId = $"stage2-pending-{Guid.NewGuid():N}";

        foreach (var statement in analysis.Statements.Where(statement => !statement.IsBusinessContext))
            statement.AnalysisTopicId = null;

        var unusedTopics = existingTopics.Select(x => x.Topic).ToHashSet();
        foreach (var assignment in topicAssignments)
        {
            var topic = unusedTopics
                .OrderByDescending(existing => existing.Statements.Count(statement => assignment.StatementIds.Contains(statement.ExternalId)))
                .ThenBy(existing => TopicOrder(existingTopics.Single(x => x.Topic == existing).OriginalExternalId))
                .ThenBy(existing => existing.Id)
                .First();
            unusedTopics.Remove(topic);
            topic.ExternalId = assignment.Topic.Id;
            topic.Name = assignment.Topic.Name;
            foreach (var statementId in assignment.Topic.StatementIds)
            {
                var statement = statements[statementId];
                statement.AnalysisTopicId = topic.Id;
            }
        }
        var outputTopicIds = topicAssignments.Select(x => x.Topic.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var unused in unusedTopics)
        {
            var originalId = existingTopics.Single(x => x.Topic == unused).OriginalExternalId;
            unused.ExternalId = outputTopicIds.Contains(originalId)
                ? $"stage2-unused-{Guid.NewGuid():N}"
                : originalId;
        }

        foreach (var statementDto in response.Statements)
            statements[statementDto.Id].Status = statementDto.Status;

        var existingRelations = analysis.Relations.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        foreach (var relation in analysis.Relations.ToList())
        {
            db.AnalysisRelationSourceStatements.RemoveRange(relation.SourceStatementLinks);
            db.AnalysisRelationTargetStatements.RemoveRange(relation.TargetStatementLinks);
        }
        foreach (var relation in analysis.Relations.Where(x => response.Relations.All(dto => dto.Id != x.ExternalId)).ToList())
            db.AnalysisRelations.Remove(relation);

        foreach (var relationDto in response.Relations)
        {
            var relation = existingRelations.TryGetValue(relationDto.Id, out var existing)
                ? existing
                : new AnalysisRelation { SpecificationAnalysisId = analysis.Id, ExternalId = relationDto.Id };
            relation.Type = relationDto.Type;
            relation.Reason = relationDto.Reason;
            foreach (var sourceId in relationDto.SourceStatementIds)
                relation.SourceStatementLinks.Add(new AnalysisRelationSourceStatement { AnalysisRelationId = relation.Id, AnalysisStatementId = statements[sourceId].Id });
            foreach (var targetId in relationDto.TargetStatementIds)
                relation.TargetStatementLinks.Add(new AnalysisRelationTargetStatement { AnalysisRelationId = relation.Id, AnalysisStatementId = statements[targetId].Id });
            if (existing is null) analysis.Relations.Add(relation);
            db.Add(relation);
            db.AddRange(relation.SourceStatementLinks);
            db.AddRange(relation.TargetStatementLinks);
        }
    }

    private static StageSegmentDto ToStageSegment(TranscriptSegment segment) =>
        new(segment.Id, segment.StartSeconds, segment.EndSeconds, segment.CleanedText ?? segment.Text);

    private static string Serialize<T>(T response) => JsonSerializer.Serialize(response, SpecificationJson.Options);
}
