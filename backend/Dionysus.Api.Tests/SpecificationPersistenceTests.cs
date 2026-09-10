using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

public sealed class SpecificationPersistenceTests
{
    [Fact]
    public async Task Project_accepts_one_recording_and_one_analysis_only()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity { OwnerId = "user-1", Name = "Interview" };
        db.Projects.Add(project);
        db.VoiceRecordings.Add(new VoiceRecording
        {
            ProjectEntityId = project.Id,
            FileName = "one.wav",
            ContentType = "audio/wav",
            SourceType = "audio"
        });
        db.SpecificationAnalyses.Add(new SpecificationAnalysis { ProjectEntityId = project.Id });
        await db.SaveChangesAsync();

        db.VoiceRecordings.Add(new VoiceRecording
        {
            ProjectEntityId = project.Id,
            FileName = "two.wav",
            ContentType = "audio/wav",
            SourceType = "audio"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Analysis_persists_statement_and_specification_source_links()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity { OwnerId = "user-1", Name = "Interview" };
        var recording = new VoiceRecording
        {
            ProjectEntityId = project.Id,
            FileName = "one.wav",
            ContentType = "audio/wav",
            SourceType = "audio"
        };
        var segment = new TranscriptSegment { VoiceRecordingId = recording.Id, Text = "Need corporate sign-in." };
        var analysis = new SpecificationAnalysis { ProjectEntityId = project.Id };
        var topic = new AnalysisTopic { SpecificationAnalysisId = analysis.Id, ExternalId = "topic-1", Name = "Authentication" };
        var statement = new AnalysisStatement
        {
            SpecificationAnalysisId = analysis.Id,
            AnalysisTopicId = topic.Id,
            ExternalId = "st-1",
            Text = "Users sign in with their corporate account."
        };
        var function = new SpecificationFunction
        {
            SpecificationAnalysisId = analysis.Id,
            AnalysisTopicId = topic.Id,
            Title = "Authentication",
            Description = "Corporate sign-in.",
            SortOrder = 0
        };
        var relation = new AnalysisRelation
        {
            SpecificationAnalysisId = analysis.Id,
            ExternalId = "rel-1",
            Type = AnalysisRelationType.Clarifies
        };
        var item = new SpecificationItem
        {
            SpecificationAnalysisId = analysis.Id,
            SpecificationFunctionId = function.Id,
            Kind = SpecificationItemKind.FunctionalRequirement,
            Description = "Support corporate sign-in."
        };

        project.Recordings.Add(recording);
        recording.Segments.Add(segment);
        project.SpecificationAnalysis = analysis;
        analysis.Topics.Add(topic);
        topic.Statements.Add(statement);
        analysis.Relations.Add(relation);
        analysis.Functions.Add(function);
        function.Items.Add(item);
        db.Projects.Add(project);
        db.AnalysisStatementSegments.Add(new AnalysisStatementSegment
        {
            AnalysisStatementId = statement.Id,
            TranscriptSegmentId = segment.Id
        });
        db.SpecificationFunctionStatements.Add(new SpecificationFunctionStatement
        {
            SpecificationFunctionId = function.Id,
            AnalysisStatementId = statement.Id
        });
        db.AnalysisRelationSourceStatements.Add(new AnalysisRelationSourceStatement
        {
            AnalysisRelationId = relation.Id,
            AnalysisStatementId = statement.Id
        });
        db.AnalysisRelationTargetStatements.Add(new AnalysisRelationTargetStatement
        {
            AnalysisRelationId = relation.Id,
            AnalysisStatementId = statement.Id
        });
        db.SpecificationItemStatements.Add(new SpecificationItemStatement
        {
            SpecificationItemId = item.Id,
            AnalysisStatementId = statement.Id
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persistedStatement = await db.AnalysisStatements
            .Include(x => x.SegmentLinks)
            .SingleAsync(x => x.Id == statement.Id);
        var persistedFunction = await db.SpecificationFunctions
            .Include(x => x.StatementLinks)
            .SingleAsync(x => x.Id == function.Id);
        var persistedRelation = await db.AnalysisRelations
            .Include(x => x.SourceStatementLinks)
            .Include(x => x.TargetStatementLinks)
            .SingleAsync(x => x.Id == relation.Id);
        var persistedItem = await db.SpecificationItems
            .Include(x => x.StatementLinks)
            .SingleAsync(x => x.Id == item.Id);

        Assert.Equal(segment.Id, Assert.Single(persistedStatement.SegmentLinks).TranscriptSegmentId);
        Assert.Equal(statement.Id, Assert.Single(persistedFunction.StatementLinks).AnalysisStatementId);
        Assert.Equal(statement.Id, Assert.Single(persistedItem.StatementLinks).AnalysisStatementId);
        Assert.Equal(statement.Id, Assert.Single(persistedRelation.SourceStatementLinks).AnalysisStatementId);
        Assert.Equal(statement.Id, Assert.Single(persistedRelation.TargetStatementLinks).AnalysisStatementId);
    }

    private sealed class PostgresDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private PostgresDatabase(string connectionString) => _connectionString = connectionString;

        public static async Task<PostgresDatabase> CreateAsync()
        {
            var template = Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
                ?? "Host=localhost;Port=5432;Database=dionysus;Username=dionysus;Password=dionysus";
            var builder = new NpgsqlConnectionStringBuilder(template)
            {
                Database = $"dionysus_task_1_{Guid.NewGuid():N}"
            };
            var database = new PostgresDatabase(builder.ConnectionString);
            await using var db = database.CreateContext();
            await db.Database.EnsureCreatedAsync();
            return database;
        }

        public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);

        public async ValueTask DisposeAsync()
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
