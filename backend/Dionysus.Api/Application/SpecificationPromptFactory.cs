public sealed class SpecificationPromptFactory : ISpecificationPromptFactory
{
    public string Stage0Instructions() => """
        You are a transcript editor. Correct transcription errors without changing meaning.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage0CleanupResponse shape: {"schemaVersion":"1.0","segments":[{"segmentId":"guid","cleanedText":"text"}]}.
        Preserve every input segment's count, order, and IDs exactly. Do not alter timestamps. Each cleanedText must be nonempty.
        """;

    public string Stage1Instructions() => """
        You are a business analyst extracting business context, functional topics, and atomic statements from a transcript.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage1ExtractionResponse shape: {"schemaVersion":"1.0","businessContext":[{"id":"ctx-N","text":"text","sourceSegmentIds":["guid"]}],"topics":[{"id":"topic-N","name":"text","statements":[{"id":"st-N","text":"text","sourceSegmentIds":["guid"]}]}]}.
        Every context item and statement must cite one or more supplied source segment IDs.
        """;

    public string Stage2Instructions() => """
        You are a strict reviewer of an extracted specification structure. Normalize duplicate topics and add statuses and relations.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage2ReviewResponse shape: {"schemaVersion":"1.0","businessContext":[],"topics":[{"id":"topic-N","name":"text","statementIds":["st-N"]}],"statements":[{"id":"st-N","text":"text","status":"active","sourceSegmentIds":["guid"]}],"relations":[{"id":"rel-N","type":"clarifies","sourceStatementIds":["st-N"],"targetStatementIds":["st-N"],"reason":"text"}]}.
        Do not invent facts, change statement text, remove statements, or change source links. Each statement belongs to exactly one topic.
        """;

    public string Stage3Instructions() => """
        You are a system analyst producing one concise functional specification block.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage3FunctionResponse shape with function, roles, functionalRequirements, userScenarios, constraints, conditions, agreements, and keyQuestions arrays.
        Synthesize only from the supplied source statements. Every generated item, including function, must cite one or more supplied source statement IDs. keyQuestions is required and may be an empty array.
        """;
}
