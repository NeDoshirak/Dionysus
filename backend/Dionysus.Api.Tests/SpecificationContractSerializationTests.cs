using System;
using System.Text.Json;
using Xunit;

public sealed class SpecificationContractSerializationTests
{
    [Fact]
    public void Stage0_response_rejects_unknown_json_fields()
    {
        const string json = """{ "schemaVersion":"1.0", "segments":[], "extra":true }""";

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage0CleanupResponse>(json));
    }

    [Fact]
    public void Stage0_response_rejects_case_mismatched_json_fields()
    {
        const string json = """{ "SchemaVersion":"1.0", "segments":[] }""";

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage0CleanupResponse>(json));
    }

    [Fact]
    public void Stage2_response_rejects_invalid_statement_status()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "businessContext":[],
              "topics":[],
              "statements":[
                { "id":"st-1", "text":"A fact.", "status":"invalid", "sourceSegmentIds":["00000000-0000-0000-0000-000000000001"] }
              ],
              "relations":[]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage2ReviewResponse>(json));
    }

    [Fact]
    public void Stage2_response_rejects_numeric_statement_status()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "businessContext":[],
              "topics":[],
              "statements":[
                { "id":"st-1", "text":"A fact.", "status":0, "sourceSegmentIds":["00000000-0000-0000-0000-000000000001"] }
              ],
              "relations":[]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage2ReviewResponse>(json));
    }

    [Fact]
    public void Stage2_response_rejects_numeric_relation_type()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "businessContext":[],
              "topics":[],
              "statements":[],
              "relations":[
                { "id":"rel-1", "type":0, "sourceStatementIds":["st-1"], "targetStatementIds":["st-2"], "reason":"Same fact." }
              ]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage2ReviewResponse>(json));
    }

    [Fact]
    public void Stage3_response_rejects_numeric_requirement_priority()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "function": { "title":"Authentication", "description":"Corporate sign-in.", "sourceStatementIds":["st-1"] },
              "roles":[],
              "functionalRequirements":[
                { "id":"req-1", "title":"Sign in", "description":"Use an account.", "priority":0, "sourceStatementIds":["st-1"] }
              ],
              "userScenarios":[],
              "constraints":[],
              "conditions":[],
              "agreements":[],
              "keyQuestions":[]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage3FunctionResponse>(json));
    }

    [Fact]
    public void Stage3_response_rejects_an_unsupported_schema_version()
    {
        const string json = """
            {
              "schemaVersion":"2.0",
              "function": { "title":"Authentication", "description":"Corporate sign-in.", "sourceStatementIds":["st-1"] },
              "roles":[],
              "functionalRequirements":[],
              "userScenarios":[],
              "constraints":[],
              "conditions":[],
              "agreements":[],
              "keyQuestions":[]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage3FunctionResponse>(json));
    }

    [Fact]
    public void Stage3_response_deserializes_required_empty_arrays()
    {
        var response = SpecificationJson.Deserialize<Stage3FunctionResponse>(ValidStage3Json);

        Assert.Empty(response.Roles);
        Assert.Empty(response.FunctionalRequirements);
        Assert.Empty(response.UserScenarios);
        Assert.Empty(response.Constraints);
        Assert.Empty(response.Conditions);
        Assert.Empty(response.Agreements);
        Assert.Empty(response.KeyQuestions);
    }

    [Fact]
    public void Stage3_response_accepts_legacy_function_name_as_title()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "function": { "name":"Authentication", "description":"Corporate sign-in.", "sourceStatementIds":["st-1"] },
              "roles":[], "functionalRequirements":[], "userScenarios":[], "constraints":[], "conditions":[], "agreements":[], "keyQuestions":[]
            }
            """;

        var response = SpecificationJson.Deserialize<Stage3FunctionResponse>(json);

        Assert.Equal("Authentication", response.Function.Title);
    }

    [Fact]
    public void Stage3_response_rejects_an_absent_required_collection()
    {
        const string json = """
            {
              "schemaVersion":"1.0",
              "function": { "title":"Authentication", "description":"Corporate sign-in.", "sourceStatementIds":["st-1"] },
              "roles":[],
              "functionalRequirements":[],
              "userScenarios":[],
              "constraints":[],
              "conditions":[],
              "agreements":[]
            }
            """;

        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage3FunctionResponse>(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public void Deserialize_rejects_empty_or_null_json(string json)
    {
        Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage0CleanupResponse>(json));
    }

    private const string ValidStage3Json = """
        {
          "schemaVersion":"1.0",
          "function": { "title":"Authentication", "description":"Corporate sign-in.", "sourceStatementIds":["st-1"] },
          "roles":[],
          "functionalRequirements":[],
          "userScenarios":[],
          "constraints":[],
          "conditions":[],
          "agreements":[],
          "keyQuestions":[]
        }
        """;
}
