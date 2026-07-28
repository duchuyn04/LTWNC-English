using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public sealed class OpenAiCompatibleAdapter : IAiProviderAdapter
{
    private readonly OpenAiCompatibleClient _client;

    public OpenAiCompatibleAdapter(OpenAiCompatibleClient client)
    {
        // 1. Lưu dependency `_client` để các phương thức khác sử dụng.
        _client = client;
    }

    public string AdapterType => "OpenAICompatible";

    public Task<IReadOnlyList<string>> GetModelsAsync(AiProvider provider, string? apiKey, CancellationToken cancellationToken)
    {
        // 1. Trả kết quả từ `GetModelsAsync` cho nơi gọi.
        return _client.GetModelsAsync(provider, apiKey, cancellationToken);
    }

    public Task<string> CompleteAsync(AiProvider provider, string? apiKey, AiCompletionRequest request, CancellationToken cancellationToken)
    {
        // 1. Trả kết quả từ `CompleteAsync` cho nơi gọi.
        return _client.CompleteAsync(provider, apiKey, request, cancellationToken);
    }
}
