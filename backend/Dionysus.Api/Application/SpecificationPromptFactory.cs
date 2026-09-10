public sealed class SpecificationPromptFactory : ISpecificationPromptFactory
{
    public string Stage0Instructions() => """
        You are a transcript editor. Correct transcription errors without changing meaning.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage0CleanupResponse shape: {"schemaVersion":"1.0","segments":[{"segmentId":"guid","cleanedText":"text"}]}.
        Preserve every input segment's count, order, and IDs exactly. Copy the actual input segmentId values character-for-character into the corresponding output items. Never generate, replace, shorten, reformat, or omit a segmentId; "guid" is only a schema placeholder, not a value to output. Do not return an empty segments array when input segments are supplied. Do not alter timestamps. Each cleanedText must be nonempty.
        """;

    public string Stage1Instructions() => """
        You are a business analyst extracting business context, functional topics, and atomic statements from a transcript.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage1ExtractionResponse shape: {"schemaVersion":"1.0","businessContext":[{"id":"ctx-N","text":"text","sourceSegmentIds":["guid"]}],"topics":[{"id":"topic-N","name":"text","statements":[{"id":"st-N","text":"text","sourceSegmentIds":["guid"]}]}]}.
        Every context item and statement must cite one or more supplied source segment IDs.
        """;

    public string Stage2Instructions() => """
        You are a strict reviewer of an extracted specification structure. Normalize duplicate topics and add statuses and relations.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and the exact Stage2ReviewResponse shape: {"schemaVersion":"1.0","businessContext":[],"topics":[{"id":"topic-N","name":"text","statementIds":["st-N"]}],"statements":[{"id":"st-N","text":"text","status":"active","sourceSegmentIds":["guid"]}],"relations":[{"id":"rel-N","type":"clarifies","sourceStatementIds":["st-N"],"targetStatementIds":["st-N"],"reason":"text"}]}.
        businessContext is immutable: copy every item unchanged, with the same IDs, text, source segment IDs, and order. Do not review, edit, remove, add, merge, or reorder businessContext.
        Do not invent facts, change statement text, remove statements, or change source links. Each statement belongs to exactly one topic.
        """;

    public string Stage3Instructions() => """
        You are a system analyst producing one concise functional specification block.
        Return one JSON object only; no Markdown or extra text. Use "schemaVersion": "1.0" and this exact Stage3FunctionResponse shape:
        {"schemaVersion":"1.0","function":{"title":"text","description":"text","sourceStatementIds":["st-N"]},"roles":[{"id":"role-N","name":"text","description":"text","sourceStatementIds":["st-N"]}],"functionalRequirements":[{"id":"req-N","title":"text","description":"text","priority":"required|desirable|future|unknown","sourceStatementIds":["st-N"]}],"userScenarios":[{"id":"scenario-N","title":"text","actor":"text","description":"text","sourceStatementIds":["st-N"]}],"constraints":[{"id":"constraint-N","description":"text","sourceStatementIds":["st-N"]}],"conditions":[{"id":"condition-N","description":"text","sourceStatementIds":["st-N"]}],"agreements":[{"id":"agreement-N","description":"text","sourceStatementIds":["st-N"]}],"keyQuestions":[{"id":"question-N","title":"text","description":"text","reason":"contradiction|unresolved|missingInformation","sourceStatementIds":["st-N"]}]}.
        Every array item must contain every shown field. Use the shown ID prefixes exactly; IDs are unique within their array. Use an empty array instead of an incomplete item.
        The function object must use "title", not "name".
        Write every generated title, description, and question in Russian.
        Synthesize only from the supplied source statements. Do not invent facts, roles, permissions, scenarios, constraints, or implementation details. If an item is not explicitly supported by supplied source statements, do not create it; use keyQuestions only for an explicit contradiction, unresolved statement, or missing information. Every generated item, including function, must cite one or more supplied source statement IDs. keyQuestions is required and may be an empty array.
        """;
}
