using System.Text.Json.Serialization;

namespace Arbiter.Api.Http;

public class ValidationProblemDetails : ProblemDetails
{
    public ValidationProblemDetails()
    {
        Title = "One or more validation errors occurred.";
    }

    public ValidationProblemDetails(ModelStateDictionary modelState)
    {
        Title = "One or more validation errors occurred.";
        Status = 400;
        Errors = modelState.ToSerializableDictionary();
    }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors
    {
        get;
        set;
    }
}

public class ProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type
    {
        get;
        set;
    }

    [JsonPropertyName("title")]
    public string? Title
    {
        get;
        set;
    }

    [JsonPropertyName("status")]
    public int? Status
    {
        get;
        set;
    }

    [JsonPropertyName("detail")]
    public string? Detail
    {
        get;
        set;
    }

    [JsonPropertyName("instance")]
    public string? Instance
    {
        get;
        set;
    }

    [JsonPropertyName("extensions")]
    public Dictionary<string, object?>? Extensions
    {
        get;
        set;
    }
}