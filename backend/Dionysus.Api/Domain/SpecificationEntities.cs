public enum SpecificationAnalysisStatus
{
    Queued,
    RunningStage0,
    RunningStage1,
    RunningStage2,
    RunningStage3,
    Completed,
    Failed
}

public enum AnalysisStatementStatus
{
    Active,
    Superseded,
    Unresolved
}

public enum AnalysisRelationType
{
    Same,
    Clarifies,
    Supersedes,
    Contradicts,
    Unresolved,
    Related
}

public enum SpecificationItemKind
{
    BusinessContext,
    Role,
    FunctionalRequirement,
    UserScenario,
    Constraint,
    Condition,
    Agreement,
    KeyQuestion,
    ProjectContradiction
}

public sealed class SpecificationAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectEntityId { get; set; }
    public SpecificationAnalysisStatus Status { get; set; } = SpecificationAnalysisStatus.Queued;
    public Guid RunId { get; set; } = Guid.NewGuid();
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public string? Stage0RawResponse { get; set; }
    public string? Stage1RawResponse { get; set; }
    public string? Stage2RawResponse { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public List<AnalysisTopic> Topics { get; set; } = [];
    public List<AnalysisStatement> Statements { get; set; } = [];
    public List<AnalysisRelation> Relations { get; set; } = [];
    public List<SpecificationFunction> Functions { get; set; } = [];
    public List<SpecificationItem> Items { get; set; } = [];
}

public sealed class AnalysisTopic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecificationAnalysisId { get; set; }
    public string ExternalId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public SpecificationAnalysis SpecificationAnalysis { get; set; } = null!;
    public List<AnalysisStatement> Statements { get; set; } = [];
    public List<SpecificationFunction> Functions { get; set; } = [];
}

public sealed class AnalysisStatement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecificationAnalysisId { get; set; }
    public Guid? AnalysisTopicId { get; set; }
    public string ExternalId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public AnalysisStatementStatus Status { get; set; } = AnalysisStatementStatus.Active;
    public bool IsBusinessContext { get; set; }
    public SpecificationAnalysis SpecificationAnalysis { get; set; } = null!;
    public AnalysisTopic? Topic { get; set; }
    public List<AnalysisStatementSegment> SegmentLinks { get; set; } = [];
    public List<AnalysisRelationSourceStatement> SourceRelationLinks { get; set; } = [];
    public List<AnalysisRelationTargetStatement> TargetRelationLinks { get; set; } = [];
    public List<SpecificationFunctionStatement> FunctionLinks { get; set; } = [];
    public List<SpecificationItemStatement> ItemLinks { get; set; } = [];
}

public sealed class AnalysisStatementSegment
{
    public Guid AnalysisStatementId { get; set; }
    public Guid TranscriptSegmentId { get; set; }
    public AnalysisStatement AnalysisStatement { get; set; } = null!;
    public TranscriptSegment TranscriptSegment { get; set; } = null!;
}

public sealed class AnalysisRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecificationAnalysisId { get; set; }
    public string ExternalId { get; set; } = null!;
    public AnalysisRelationType Type { get; set; }
    public string? Reason { get; set; }
    public SpecificationAnalysis SpecificationAnalysis { get; set; } = null!;
    public List<AnalysisRelationSourceStatement> SourceStatementLinks { get; set; } = [];
    public List<AnalysisRelationTargetStatement> TargetStatementLinks { get; set; } = [];
}

public sealed class AnalysisRelationSourceStatement
{
    public Guid AnalysisRelationId { get; set; }
    public Guid AnalysisStatementId { get; set; }
    public AnalysisRelation AnalysisRelation { get; set; } = null!;
    public AnalysisStatement AnalysisStatement { get; set; } = null!;
}

public sealed class AnalysisRelationTargetStatement
{
    public Guid AnalysisRelationId { get; set; }
    public Guid AnalysisStatementId { get; set; }
    public AnalysisRelation AnalysisRelation { get; set; } = null!;
    public AnalysisStatement AnalysisStatement { get; set; } = null!;
}

public sealed class SpecificationFunction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecificationAnalysisId { get; set; }
    public Guid? AnalysisTopicId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int SortOrder { get; set; }
    public SpecificationAnalysis SpecificationAnalysis { get; set; } = null!;
    public AnalysisTopic? Topic { get; set; }
    public List<SpecificationFunctionStatement> StatementLinks { get; set; } = [];
    public List<SpecificationItem> Items { get; set; } = [];
}

public sealed class SpecificationFunctionStatement
{
    public Guid SpecificationFunctionId { get; set; }
    public Guid AnalysisStatementId { get; set; }
    public SpecificationFunction SpecificationFunction { get; set; } = null!;
    public AnalysisStatement AnalysisStatement { get; set; } = null!;
}

public sealed class SpecificationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecificationAnalysisId { get; set; }
    public Guid? SpecificationFunctionId { get; set; }
    public SpecificationItemKind Kind { get; set; }
    public string? Title { get; set; }
    public string Description { get; set; } = null!;
    public string? Priority { get; set; }
    public int SortOrder { get; set; }
    public bool IsManual { get; set; }
    public SpecificationAnalysis SpecificationAnalysis { get; set; } = null!;
    public SpecificationFunction? SpecificationFunction { get; set; }
    public List<SpecificationItemStatement> StatementLinks { get; set; } = [];
}

public sealed class SpecificationItemStatement
{
    public Guid SpecificationItemId { get; set; }
    public Guid AnalysisStatementId { get; set; }
    public SpecificationItem SpecificationItem { get; set; } = null!;
    public AnalysisStatement AnalysisStatement { get; set; } = null!;
}
