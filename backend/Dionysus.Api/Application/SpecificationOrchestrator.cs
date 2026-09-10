using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public sealed class SpecificationOrchestrator(
    AppDbContext db,
    IStructuredSpecificationAiService ai,
    ISpecificationContractValidator validator) : ISpecificationAnalysisOrchestrator
{
    public async Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct)
    {
        var analysis = await LoadAsync(job.AnalysisId, ct);
        if (analysis is null || analysis.RunId != job.RunId || analysis.Status == SpecificationAnalysisStatus.Completed)
            return;

        var stage = 0;
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

            stage = 1;
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage1, null, ct)) return;
            var stage1Request = new Stage1ExtractionRequest("1.0", segments.Select(ToStageSegment).ToList());
            var stage1 = await ai.RunStage1Async(stage1Request, ct);
            validator.ValidateStage1(segments, stage1);
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage1, () =>
            {
                analysis.Stage1RawResponse = Serialize(stage1);
                PersistStage1(analysis, stage1);
            }, ct)) return;

            stage = 2;
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage2, null, ct)) return;
            var stage2 = await ai.RunStage2Async(stage1, ct);
            validator.ValidateStage2(stage1, stage2);
            if (!await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage2, () =>
            {
                analysis.Stage2RawResponse = Serialize(stage2);
                PersistStage2(analysis, stage2);
            }, ct)) return;

            await MutateAsync(analysis, job, SpecificationAnalysisStatus.RunningStage3, null, ct);
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

    private async Task MarkFailedAsync(SpecificationAnalysisJob job, int stage, Exception exception)
    {
        db.ChangeTracker.Clear();
        var analysis = await db.SpecificationAnalyses.SingleOrDefaultAsync(x => x.Id == job.AnalysisId);
        if (analysis is null || analysis.RunId != job.RunId || analysis.Status == SpecificationAnalysisStatus.Completed) return;
        analysis.Status = SpecificationAnalysisStatus.Failed;
        analysis.Error = $"stage{stage} failed: {exception.GetType().Name}";
        await db.SaveChangesAsync();
    }

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
        var topics = analysis.Topics.ToDictionary(topic => topic.ExternalId, StringComparer.Ordinal);
        var statements = analysis.Statements.ToDictionary(statement => statement.ExternalId, StringComparer.Ordinal);
        foreach (var topicDto in response.Topics)
        {
            var topic = topics[topicDto.Id];
            topic.Name = topicDto.Name;
            foreach (var statement in topic.Statements.ToList()) statement.AnalysisTopicId = null;
            foreach (var statementId in topicDto.StatementIds)
            {
                var statement = statements[statementId];
                statement.AnalysisTopicId = topic.Id;
            }
        }

        foreach (var statementDto in response.Statements)
            statements[statementDto.Id].Status = statementDto.Status;

        foreach (var relationDto in response.Relations)
        {
            var relation = new AnalysisRelation
            {
                SpecificationAnalysisId = analysis.Id,
                ExternalId = relationDto.Id,
                Type = relationDto.Type,
                Reason = relationDto.Reason
            };
            foreach (var sourceId in relationDto.SourceStatementIds)
                relation.SourceStatementLinks.Add(new AnalysisRelationSourceStatement { AnalysisRelationId = relation.Id, AnalysisStatementId = statements[sourceId].Id });
            foreach (var targetId in relationDto.TargetStatementIds)
                relation.TargetStatementLinks.Add(new AnalysisRelationTargetStatement { AnalysisRelationId = relation.Id, AnalysisStatementId = statements[targetId].Id });
            analysis.Relations.Add(relation);
            db.Add(relation);
            db.AddRange(relation.SourceStatementLinks);
            db.AddRange(relation.TargetStatementLinks);
        }
    }

    private static StageSegmentDto ToStageSegment(TranscriptSegment segment) =>
        new(segment.Id, segment.StartSeconds, segment.EndSeconds, segment.CleanedText ?? segment.Text);

    private static string Serialize<T>(T response) => JsonSerializer.Serialize(response, SpecificationJson.Options);
}
