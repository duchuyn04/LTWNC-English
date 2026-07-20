using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public sealed class OpenAiCompatibleAdapter : IAiProviderAdapter
{
    private readonly OpenAiCompatibleClient _client;

    public OpenAiCompatibleAdapter(OpenAiCompatibleClient client)
    {
        _client = client;
    }

    public string AdapterType => "OpenAICompatible";

    public Task<IReadOnlyList<string>> GetModelsAsync(AiProvider provider, string? apiKey, CancellationToken cancellationToken) =>
        _client.GetModelsAsync(provider, apiKey, cancellationToken);

    public Task<string> CompleteAsync(AiProvider provider, string? apiKey, AiCompletionRequest request, CancellationToken cancellationToken) =>
        _client.CompleteAsync(provider, apiKey, request, cancellationToken);
}
