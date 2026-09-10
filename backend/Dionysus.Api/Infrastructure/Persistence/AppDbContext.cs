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

        modelBuilder.Entity<VoiceRecording>().HasIndex(x => x.ProjectEntityId).IsUnique();
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

}
