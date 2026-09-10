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
