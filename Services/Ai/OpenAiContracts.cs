using System.Text.Json.Serialization;

namespace ltwnc.Services.Ai;

// Contract riêng của OpenAI-compatible API; application không dùng trực tiếp các kiểu này.
public sealed record OpenAiChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiChatMessage> Messages,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("temperature")] decimal Temperature);

public sealed class OpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAiChatChoice> Choices { get; init; } = [];
}

public sealed class OpenAiChatChoice
{
    [JsonPropertyName("message")]
    public OpenAiChatResponseMessage? Message { get; init; }
}

public sealed class OpenAiChatResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
