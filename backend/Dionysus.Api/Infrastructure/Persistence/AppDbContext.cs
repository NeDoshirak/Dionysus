using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class AppUser : IdentityUser { }

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<VoiceRecording> VoiceRecordings => Set<VoiceRecording>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<SpecificationAnalysis> SpecificationAnalyses => Set<SpecificationAnalysis>();
    public DbSet<AnalysisTopic> AnalysisTopics => Set<AnalysisTopic>();
    public DbSet<AnalysisStatement> AnalysisStatements => Set<AnalysisStatement>();
    public DbSet<AnalysisStatementSegment> AnalysisStatementSegments => Set<AnalysisStatementSegment>();
    public DbSet<AnalysisRelation> AnalysisRelations => Set<AnalysisRelation>();
    public DbSet<AnalysisRelationSourceStatement> AnalysisRelationSourceStatements => Set<AnalysisRelationSourceStatement>();
    public DbSet<AnalysisRelationTargetStatement> AnalysisRelationTargetStatements => Set<AnalysisRelationTargetStatement>();
    public DbSet<SpecificationFunction> SpecificationFunctions => Set<SpecificationFunction>();
    public DbSet<SpecificationFunctionStatement> SpecificationFunctionStatements => Set<SpecificationFunctionStatement>();
    public DbSet<SpecificationItem> SpecificationItems => Set<SpecificationItem>();
    public DbSet<SpecificationItemStatement> SpecificationItemStatements => Set<SpecificationItemStatement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VoiceRecording>().HasIndex(x => x.ProjectEntityId);
        modelBuilder.Entity<VoiceRecording>().Property(x => x.IsCurrent).HasDefaultValue(true);
        modelBuilder.Entity<VoiceRecording>()
            .HasIndex(x => x.ProjectEntityId, "IX_VoiceRecordings_ProjectEntityId_Current")
            .HasFilter("\"IsCurrent\" = TRUE")
            .IsUnique();
        modelBuilder.Entity<SpecificationAnalysis>().HasIndex(x => x.ProjectEntityId).IsUnique();
        modelBuilder.Entity<AnalysisTopic>().HasIndex(x => new { x.SpecificationAnalysisId, x.ExternalId }).IsUnique();
        modelBuilder.Entity<AnalysisStatement>().HasIndex(x => new { x.SpecificationAnalysisId, x.ExternalId }).IsUnique();
        modelBuilder.Entity<AnalysisRelation>().HasIndex(x => new { x.SpecificationAnalysisId, x.ExternalId }).IsUnique();

        modelBuilder.Entity<ProjectEntity>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithOne(x => x.Project)
            .HasForeignKey<SpecificationAnalysis>(x => x.ProjectEntityId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VoiceRecording>()
            .HasOne<ProjectEntity>()
            .WithMany(x => x.Recordings)
            .HasForeignKey(x => x.ProjectEntityId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TranscriptSegment>()
            .HasOne<VoiceRecording>()
            .WithMany(x => x.Segments)
            .HasForeignKey(x => x.VoiceRecordingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AnalysisTopic>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithMany(x => x.Topics)
            .HasForeignKey(x => x.SpecificationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisStatement>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithMany(x => x.Statements)
            .HasForeignKey(x => x.SpecificationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisStatement>()
            .HasOne(x => x.Topic)
            .WithMany(x => x.Statements)
            .HasForeignKey(x => x.AnalysisTopicId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AnalysisStatementSegment>().HasKey(x => new { x.AnalysisStatementId, x.TranscriptSegmentId });
        modelBuilder.Entity<AnalysisStatementSegment>()
            .HasOne(x => x.AnalysisStatement)
            .WithMany(x => x.SegmentLinks)
            .HasForeignKey(x => x.AnalysisStatementId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisStatementSegment>()
            .HasOne(x => x.TranscriptSegment)
            .WithMany()
            .HasForeignKey(x => x.TranscriptSegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AnalysisRelation>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithMany(x => x.Relations)
            .HasForeignKey(x => x.SpecificationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisRelationSourceStatement>().HasKey(x => new { x.AnalysisRelationId, x.AnalysisStatementId });
        modelBuilder.Entity<AnalysisRelationSourceStatement>()
            .HasOne(x => x.AnalysisRelation)
            .WithMany(x => x.SourceStatementLinks)
            .HasForeignKey(x => x.AnalysisRelationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisRelationSourceStatement>()
            .HasOne(x => x.AnalysisStatement)
            .WithMany(x => x.SourceRelationLinks)
            .HasForeignKey(x => x.AnalysisStatementId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisRelationTargetStatement>().HasKey(x => new { x.AnalysisRelationId, x.AnalysisStatementId });
        modelBuilder.Entity<AnalysisRelationTargetStatement>()
            .HasOne(x => x.AnalysisRelation)
            .WithMany(x => x.TargetStatementLinks)
            .HasForeignKey(x => x.AnalysisRelationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnalysisRelationTargetStatement>()
            .HasOne(x => x.AnalysisStatement)
            .WithMany(x => x.TargetRelationLinks)
            .HasForeignKey(x => x.AnalysisStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecificationFunction>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithMany(x => x.Functions)
            .HasForeignKey(x => x.SpecificationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SpecificationFunction>()
            .HasOne(x => x.Topic)
            .WithMany(x => x.Functions)
            .HasForeignKey(x => x.AnalysisTopicId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SpecificationFunctionStatement>().HasKey(x => new { x.SpecificationFunctionId, x.AnalysisStatementId });
        modelBuilder.Entity<SpecificationFunctionStatement>()
            .HasOne(x => x.SpecificationFunction)
            .WithMany(x => x.StatementLinks)
            .HasForeignKey(x => x.SpecificationFunctionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SpecificationFunctionStatement>()
            .HasOne(x => x.AnalysisStatement)
            .WithMany(x => x.FunctionLinks)
            .HasForeignKey(x => x.AnalysisStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecificationItem>()
            .HasOne(x => x.SpecificationAnalysis)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SpecificationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SpecificationItem>()
            .HasOne(x => x.SpecificationFunction)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SpecificationFunctionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SpecificationItemStatement>().HasKey(x => new { x.SpecificationItemId, x.AnalysisStatementId });
        modelBuilder.Entity<SpecificationItemStatement>()
            .HasOne(x => x.SpecificationItem)
            .WithMany(x => x.StatementLinks)
            .HasForeignKey(x => x.SpecificationItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SpecificationItemStatement>()
            .HasOne(x => x.AnalysisStatement)
            .WithMany(x => x.ItemLinks)
            .HasForeignKey(x => x.AnalysisStatementId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateProvenanceLinks();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateProvenanceLinks();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateProvenanceLinks()
    {
        ChangeTracker.DetectChanges();

        var statementSegments = Changed<AnalysisStatementSegment>();
        var sourceRelations = Changed<AnalysisRelationSourceStatement>();
        var targetRelations = Changed<AnalysisRelationTargetStatement>();
        var functionStatements = Changed<SpecificationFunctionStatement>();
        var itemStatements = Changed<SpecificationItemStatement>();
        if (statementSegments.Count == 0 && sourceRelations.Count == 0 && targetRelations.Count == 0 &&
            functionStatements.Count == 0 && itemStatements.Count == 0) return;

        var statementIds = statementSegments.Select(x => x.AnalysisStatementId)
            .Concat(sourceRelations.Select(x => x.AnalysisStatementId))
            .Concat(targetRelations.Select(x => x.AnalysisStatementId))
            .Concat(functionStatements.Select(x => x.AnalysisStatementId))
            .Concat(itemStatements.Select(x => x.AnalysisStatementId))
            .Distinct()
            .ToArray();
        var statementAnalysisIds = AnalysisStatements.AsNoTracking()
            .Where(x => statementIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.SpecificationAnalysisId);
        foreach (var entry in ChangeTracker.Entries<AnalysisStatement>().Where(x => x.State != EntityState.Deleted))
            statementAnalysisIds[entry.Entity.Id] = entry.Entity.SpecificationAnalysisId;

        var relationIds = sourceRelations.Select(x => x.AnalysisRelationId)
            .Concat(targetRelations.Select(x => x.AnalysisRelationId))
            .Distinct()
            .ToArray();
        var relationAnalysisIds = AnalysisRelations.AsNoTracking()
            .Where(x => relationIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.SpecificationAnalysisId);
        foreach (var entry in ChangeTracker.Entries<AnalysisRelation>().Where(x => x.State != EntityState.Deleted))
            relationAnalysisIds[entry.Entity.Id] = entry.Entity.SpecificationAnalysisId;

        var functionIds = functionStatements.Select(x => x.SpecificationFunctionId).Distinct().ToArray();
        var functionAnalysisIds = SpecificationFunctions.AsNoTracking()
            .Where(x => functionIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.SpecificationAnalysisId);
        foreach (var entry in ChangeTracker.Entries<SpecificationFunction>().Where(x => x.State != EntityState.Deleted))
            functionAnalysisIds[entry.Entity.Id] = entry.Entity.SpecificationAnalysisId;

        var itemIds = itemStatements.Select(x => x.SpecificationItemId).Distinct().ToArray();
        var itemAnalysisIds = SpecificationItems.AsNoTracking()
            .Where(x => itemIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.SpecificationAnalysisId);
        foreach (var entry in ChangeTracker.Entries<SpecificationItem>().Where(x => x.State != EntityState.Deleted))
            itemAnalysisIds[entry.Entity.Id] = entry.Entity.SpecificationAnalysisId;

        var segmentIds = statementSegments.Select(x => x.TranscriptSegmentId).Distinct().ToArray();
        var recordingProjectIds = VoiceRecordings.AsNoTracking().ToDictionary(x => x.Id, x => x.ProjectEntityId);
        foreach (var entry in ChangeTracker.Entries<VoiceRecording>().Where(x => x.State != EntityState.Deleted))
            recordingProjectIds[entry.Entity.Id] = entry.Entity.ProjectEntityId;
        var segmentProjectIds = TranscriptSegments.AsNoTracking()
            .Where(x => segmentIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => recordingProjectIds[x.VoiceRecordingId]);
        foreach (var entry in ChangeTracker.Entries<TranscriptSegment>().Where(x => x.State != EntityState.Deleted))
            if (recordingProjectIds.TryGetValue(entry.Entity.VoiceRecordingId, out var projectId))
                segmentProjectIds[entry.Entity.Id] = projectId;

        var analysisIds = statementAnalysisIds.Values
            .Concat(relationAnalysisIds.Values)
            .Concat(functionAnalysisIds.Values)
            .Concat(itemAnalysisIds.Values)
            .Distinct()
            .ToArray();
        var analysisProjectIds = SpecificationAnalyses.AsNoTracking()
            .Where(x => analysisIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.ProjectEntityId);
        foreach (var entry in ChangeTracker.Entries<SpecificationAnalysis>().Where(x => x.State != EntityState.Deleted))
            analysisProjectIds[entry.Entity.Id] = entry.Entity.ProjectEntityId;

        foreach (var link in statementSegments)
            EnsureSameProject(GetAnalysisProject(link.AnalysisStatementId), GetSegmentProject(link.TranscriptSegmentId));
        foreach (var link in sourceRelations)
            EnsureSameAnalysis(GetRelationAnalysis(link.AnalysisRelationId), GetStatementAnalysis(link.AnalysisStatementId));
        foreach (var link in targetRelations)
            EnsureSameAnalysis(GetRelationAnalysis(link.AnalysisRelationId), GetStatementAnalysis(link.AnalysisStatementId));
        foreach (var link in functionStatements)
            EnsureSameAnalysis(GetFunctionAnalysis(link.SpecificationFunctionId), GetStatementAnalysis(link.AnalysisStatementId));
        foreach (var link in itemStatements)
            EnsureSameAnalysis(GetItemAnalysis(link.SpecificationItemId), GetStatementAnalysis(link.AnalysisStatementId));

        Guid GetStatementAnalysis(Guid id) => statementAnalysisIds.TryGetValue(id, out var analysisId) ? analysisId : throw InvalidProvenance();
        Guid GetRelationAnalysis(Guid id) => relationAnalysisIds.TryGetValue(id, out var analysisId) ? analysisId : throw InvalidProvenance();
        Guid GetFunctionAnalysis(Guid id) => functionAnalysisIds.TryGetValue(id, out var analysisId) ? analysisId : throw InvalidProvenance();
        Guid GetItemAnalysis(Guid id) => itemAnalysisIds.TryGetValue(id, out var analysisId) ? analysisId : throw InvalidProvenance();
        Guid GetAnalysisProject(Guid statementId)
        {
            var analysisId = GetStatementAnalysis(statementId);
            return analysisProjectIds.TryGetValue(analysisId, out var projectId) ? projectId : throw InvalidProvenance();
        }
        Guid GetSegmentProject(Guid id) => segmentProjectIds.TryGetValue(id, out var projectId) ? projectId : throw InvalidProvenance();
    }

    private List<T> Changed<T>() where T : class => ChangeTracker.Entries<T>()
        .Where(x => x.State is EntityState.Added or EntityState.Modified)
        .Select(x => x.Entity)
        .ToList();

    private static void EnsureSameAnalysis(Guid firstAnalysisId, Guid secondAnalysisId)
    {
        if (firstAnalysisId != secondAnalysisId) throw InvalidProvenance();
    }

    private static void EnsureSameProject(Guid firstProjectId, Guid secondProjectId)
    {
        if (firstProjectId != secondProjectId) throw InvalidProvenance();
    }

    private static InvalidOperationException InvalidProvenance() => new("Specification provenance links must remain within one project and analysis aggregate.");

}
