using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

public sealed class SpecificationPersistenceTests
{
    [Fact]
    public async Task Migrations_create_the_current_schema_from_an_empty_database()
    {
        await using var database = await PostgresDatabase.CreateEmptyAsync();
        await using var db = database.CreateContext();

        await db.Database.MigrateAsync();

        Assert.Equal(3, await database.QueryIntAsync("SELECT COUNT(*) FROM \"__EFMigrationsHistory\""));
        Assert.Equal(1, await database.QueryIntAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'SpecificationAnalyses'"));
    }

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

        db.ChangeTracker.Clear();
        db.SpecificationAnalyses.Add(new SpecificationAnalysis { ProjectEntityId = project.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Migrations_preserve_legacy_recordings_after_the_baseline_is_recorded()
    {
        await using var database = await PostgresDatabase.CreateEmptyAsync();
        var projectId = Guid.NewGuid();
        var olderId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var createdAt = DateTimeOffset.Parse("2026-09-11T10:00:00+00:00");

        await database.ExecuteAsync($"""
            CREATE TABLE "Projects" ("Id" uuid NOT NULL PRIMARY KEY);
            CREATE TABLE "VoiceRecordings" ("Id" uuid NOT NULL PRIMARY KEY, "ProjectEntityId" uuid NOT NULL, "CreatedAt" timestamp with time zone NOT NULL);
            CREATE INDEX "IX_VoiceRecordings_ProjectEntityId" ON "VoiceRecordings" ("ProjectEntityId");
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL PRIMARY KEY, "ProductVersion" character varying(32) NOT NULL);
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260910100000_InitialCreate', '8.0.11');
            INSERT INTO "Projects" ("Id") VALUES ('{projectId}');
            INSERT INTO "VoiceRecordings" ("Id", "ProjectEntityId", "CreatedAt") VALUES ('{olderId}', '{projectId}', '{createdAt:O}'), ('{newerId}', '{projectId}', '{createdAt:O}');
            """);

        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();

        var currentIds = await database.QueryGuidsAsync("SELECT \"Id\" FROM \"VoiceRecordings\" WHERE \"IsCurrent\" ORDER BY \"Id\"");

        Assert.Equal(2, await database.QueryIntAsync("SELECT COUNT(*) FROM \"VoiceRecordings\""));
        Assert.Equal([newerId], currentIds);
    }

    [Theory]
    [InlineData("statement-segment")]
    [InlineData("relation-source")]
    [InlineData("relation-target")]
    [InlineData("function")]
    [InlineData("item")]
    public async Task Source_links_reject_cross_project_or_analysis_references(string linkType)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await db.Database.EnsureCreatedAsync();
        var ids = await SeedTwoAnalysisAggregatesAsync(db);

        switch (linkType)
        {
            case "statement-segment":
                db.AnalysisStatementSegments.Add(new AnalysisStatementSegment
                {
                    AnalysisStatementId = ids.FirstStatementId,
                    TranscriptSegmentId = ids.SecondSegmentId
                });
                break;
            case "relation-source":
                db.AnalysisRelationSourceStatements.Add(new AnalysisRelationSourceStatement
                {
                    AnalysisRelationId = ids.FirstRelationId,
                    AnalysisStatementId = ids.SecondStatementId
                });
                break;
            case "relation-target":
                db.AnalysisRelationTargetStatements.Add(new AnalysisRelationTargetStatement
                {
                    AnalysisRelationId = ids.FirstRelationId,
                    AnalysisStatementId = ids.SecondStatementId
                });
                break;
            case "function":
                db.SpecificationFunctionStatements.Add(new SpecificationFunctionStatement
                {
                    SpecificationFunctionId = ids.FirstFunctionId,
                    AnalysisStatementId = ids.SecondStatementId
                });
                break;
            case "item":
                db.SpecificationItemStatements.Add(new SpecificationItemStatement
                {
                    SpecificationItemId = ids.FirstItemId,
                    AnalysisStatementId = ids.SecondStatementId
                });
                break;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
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

        public string ConnectionString => _connectionString;

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

        public static async Task<PostgresDatabase> CreateEmptyAsync()
        {
            var template = Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
                ?? "Host=localhost;Port=5432;Database=dionysus;Username=dionysus;Password=dionysus";
            var builder = new NpgsqlConnectionStringBuilder(template)
            {
                Database = $"dionysus_task_1_{Guid.NewGuid():N}"
            };
            await using var connection = new NpgsqlConnection(template);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{builder.Database}\"", connection);
            await command.ExecuteNonQueryAsync();
            return new PostgresDatabase(builder.ConnectionString);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<Guid[]> QueryGuidsAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            var ids = new List<Guid>();
            while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
            return ids.ToArray();
        }

        public async Task<int> QueryIntAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
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

    private static async Task<AggregateIds> SeedTwoAnalysisAggregatesAsync(AppDbContext db)
    {
        var firstProject = new ProjectEntity { OwnerId = "user-1", Name = "First" };
        var secondProject = new ProjectEntity { OwnerId = "user-2", Name = "Second" };
        var firstRecording = new VoiceRecording { ProjectEntityId = firstProject.Id, FileName = "one.wav", ContentType = "audio/wav", SourceType = "audio" };
        var secondRecording = new VoiceRecording { ProjectEntityId = secondProject.Id, FileName = "two.wav", ContentType = "audio/wav", SourceType = "audio" };
        var firstAnalysis = new SpecificationAnalysis { ProjectEntityId = firstProject.Id };
        var secondAnalysis = new SpecificationAnalysis { ProjectEntityId = secondProject.Id };
        var firstStatement = new AnalysisStatement { SpecificationAnalysisId = firstAnalysis.Id, ExternalId = "st-1", Text = "First statement" };
        var secondStatement = new AnalysisStatement { SpecificationAnalysisId = secondAnalysis.Id, ExternalId = "st-1", Text = "Second statement" };
        var firstRelation = new AnalysisRelation { SpecificationAnalysisId = firstAnalysis.Id, ExternalId = "rel-1" };
        var firstFunction = new SpecificationFunction { SpecificationAnalysisId = firstAnalysis.Id, Title = "First", Description = "First function" };
        var firstItem = new SpecificationItem { SpecificationAnalysisId = firstAnalysis.Id, SpecificationFunctionId = firstFunction.Id, Kind = SpecificationItemKind.FunctionalRequirement, Description = "First item" };
        var firstSegment = new TranscriptSegment { VoiceRecordingId = firstRecording.Id, Text = "First segment" };
        var secondSegment = new TranscriptSegment { VoiceRecordingId = secondRecording.Id, Text = "Second segment" };

        db.AddRange(firstProject, secondProject, firstRecording, secondRecording, firstAnalysis, secondAnalysis,
            firstStatement, secondStatement, firstRelation, firstFunction, firstItem, firstSegment, secondSegment);
        await db.SaveChangesAsync();

        return new AggregateIds(firstStatement.Id, secondStatement.Id, firstRelation.Id, firstFunction.Id, firstItem.Id, secondSegment.Id);
    }

    private sealed record AggregateIds(
        Guid FirstStatementId,
        Guid SecondStatementId,
        Guid FirstRelationId,
        Guid FirstFunctionId,
        Guid FirstItemId,
        Guid SecondSegmentId);
}
