using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STranslate.Tests;

public class OpenAIProtocolTests
{
    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void LegacySettings_DefaultToChatCompletions()
    {
        var settings = JsonSerializer.Deserialize<Settings>("{}", StorageJsonOptions);

        Assert.NotNull(settings);
        Assert.Equal(OpenAIApiMode.ChatCompletions, settings.ApiMode);
    }

    [Theory]
    [InlineData(OpenAIApiMode.ChatCompletions, "https://api.openai.com/", "https://api.openai.com/v1/chat/completions")]
    [InlineData(OpenAIApiMode.ChatCompletions, "https://api.openai.com/v1", "https://api.openai.com/v1/chat/completions")]
    [InlineData(OpenAIApiMode.Responses, "https://api.openai.com/", "https://api.openai.com/v1/responses")]
    [InlineData(OpenAIApiMode.Responses, "https://api.openai.com/v1", "https://api.openai.com/v1/responses")]
    [InlineData(OpenAIApiMode.Responses, "https://example.com/custom", "https://example.com/custom")]
    [InlineData(OpenAIApiMode.Responses, "https://example.com/custom#", "https://example.com/custom")]
    public void BuildFinalUrl_UsesSelectedDefaultPathAndPreservesCustomPaths(
        OpenAIApiMode apiMode,
        string url,
        string expected)
    {
        Assert.Equal(expected, OpenAIProtocol.BuildFinalUrl(url, apiMode));
    }

    [Fact]
    public void CreateRequest_UsesChatCompletionsShape()
    {
        var request = OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "test-model",
            CreateMessages(),
            0.7);

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Equal("test-model", json["model"]?.ToString());
        Assert.NotNull(json["messages"]);
        Assert.Null(json["input"]);
        Assert.Null(json["store"]);
        Assert.True(json["stream"]?.GetValue<bool>());
        Assert.Equal("system", json["messages"]?[0]?["role"]?.ToString());
    }

    [Fact]
    public void CreateRequest_UsesResponsesShapeAndDisablesStorage()
    {
        var request = OpenAIProtocol.CreateRequest(
            OpenAIApiMode.Responses,
            "test-model",
            CreateMessages(),
            0.7);

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Equal("test-model", json["model"]?.ToString());
        Assert.NotNull(json["input"]);
        Assert.Null(json["messages"]);
        Assert.False(json["store"]?.GetValue<bool>());
        Assert.True(json["stream"]?.GetValue<bool>());
        Assert.Equal("system", json["input"]?[0]?["role"]?.ToString());
    }

    [Theory]
    [InlineData(OpenAIApiMode.ChatCompletions)]
    [InlineData(OpenAIApiMode.Responses)]
    public void CreateRequest_AppendsAdditionalParameters(OpenAIApiMode apiMode)
    {
        var request = OpenAIProtocol.CreateRequest(
            apiMode,
            "test-model",
            CreateMessages(),
            0.7,
            """{"thinking":{"type":"enabled"},"top_k":40}""");

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Equal("enabled", json["thinking"]?["type"]?.ToString());
        Assert.Equal(40, json["top_k"]?.GetValue<int>());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"stream\":false}")]
    [InlineData("{\"Model\":\"other-model\"}")]
    public void CreateRequest_RejectsInvalidOrConflictingAdditionalParameters(string additionalParametersJson)
    {
        Assert.Throws<FormatException>(() => OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "test-model",
            CreateMessages(),
            0.7,
            additionalParametersJson));
    }

    [Fact]
    public void ParseStreamLine_ReadsChatCompletionsDeltaWithoutRemovingContentDataPrefix()
    {
        const string line = "data: {\"choices\":[{\"delta\":{\"content\":\"data: value\"}}]}";

        var streamEvent = OpenAIProtocol.ParseStreamLine(OpenAIApiMode.ChatCompletions, line);

        Assert.Equal("data: value", streamEvent.TextDelta);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void ParseStreamLine_ReadsResponsesTextDelta()
    {
        const string line = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"translated\"}";

        var streamEvent = OpenAIProtocol.ParseStreamLine(OpenAIApiMode.Responses, line);

        Assert.Equal("translated", streamEvent.TextDelta);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void ParseStreamLine_PreservesCompatibleRawJsonStreams()
    {
        const string line = "{\"choices\":[{\"delta\":{\"content\":\"translated\"}}]}";

        var streamEvent = OpenAIProtocol.ParseStreamLine(OpenAIApiMode.ChatCompletions, line);

        Assert.Equal("translated", streamEvent.TextDelta);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Theory]
    [InlineData("event: response.created")]
    [InlineData("data: [DONE]")]
    [InlineData("data: {\"type\":\"response.reasoning_text.delta\",\"delta\":\"hidden\"}")]
    [InlineData("data: not-json")]
    [InlineData(": OPENROUTER PROCESSING")]
    public void ParseStreamLine_IgnoresNonTextEvents(string line)
    {
        var streamEvent = OpenAIProtocol.ParseStreamLine(OpenAIApiMode.Responses, line);

        Assert.Null(streamEvent.TextDelta);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Theory]
    [InlineData("data: {\"type\":\"error\",\"message\":\"request failed\"}")]
    [InlineData("data: {\"type\":\"response.failed\",\"response\":{\"error\":{\"message\":\"request failed\"}}}")]
    [InlineData("data: {\"error\":{\"message\":\"request failed\"}}")]
    public void ParseStreamLine_ReturnsStandardApiErrors(string line)
    {
        var streamEvent = OpenAIProtocol.ParseStreamLine(OpenAIApiMode.Responses, line);

        Assert.Null(streamEvent.TextDelta);
        Assert.Equal("request failed", streamEvent.ErrorMessage);
    }

    private static List<PromptItem> CreateMessages() =>
    [
        new("system", "Translate text."),
        new("user", "Hello")
    ];
}
