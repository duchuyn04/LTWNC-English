using System.Text.Json.Serialization;

namespace ltwnc.Services.Ai;

// Contract riêng của OpenAI-compatible API; application không dùng trực tiếp các kiểu này.
internal sealed record OpenAiClientConfiguration(
    string Name,
    string BaseUrl,
    int TimeoutSeconds);

internal sealed record OpenAiChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiChatMessage> Messages,
    [property: JsonPropertyName("max_tokens"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxTokens,
    [property: JsonPropertyName("max_completion_tokens"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxCompletionTokens,
    [property: JsonPropertyName("temperature")] decimal Temperature);

internal sealed class OpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAiChatChoice> Choices { get; init; } = [];
}

internal sealed class OpenAiChatChoice
{
    [JsonPropertyName("message")]
    public OpenAiChatResponseMessage? Message { get; init; }
}

internal sealed class OpenAiChatResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
