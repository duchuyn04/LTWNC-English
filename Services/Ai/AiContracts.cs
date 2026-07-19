using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public sealed record AiCompletionRequest(string SystemPrompt, string UserPrompt, int MaxTokens = 1200);

public sealed record AiCompletionResult(
    string Content,
    int ProviderId,
    string ProviderName,
    string ModelId);

public sealed class AiProviderUnavailableException : Exception
{
    public AiProviderUnavailableException(string message) : base(message) { }
}

public sealed class AiProviderConfigurationException : Exception
{
    public AiProviderConfigurationException(string message) : base(message) { }
}

public sealed class AiProviderInput
{
    public string Name { get; set; } = string.Empty;
    public string AdapterType { get; set; } = "OpenAICompatible";
    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool ClearApiKey { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}

public interface IAiCompletionRouter
{
    Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default);
}

public interface IAiProviderAdapter
{
    string AdapterType { get; }
    Task<IReadOnlyList<string>> GetModelsAsync(AiProvider provider, string? apiKey, CancellationToken cancellationToken);
    Task<string> CompleteAsync(AiProvider provider, string? apiKey, AiCompletionRequest request, CancellationToken cancellationToken);
}

public interface IAiProviderService
{
    Task<List<AiProvider>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AiProvider?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AiProvider> SaveAsync(int? id, AiProviderInput input, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> DiscoverModelsAsync(int id, CancellationToken cancellationToken = default);
    Task TestAsync(int id, CancellationToken cancellationToken = default);
}
