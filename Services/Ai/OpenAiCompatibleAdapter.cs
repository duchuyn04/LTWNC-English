using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

// Adapter: chuyển contract của application sang contract riêng của OpenAI-compatible API client.
public sealed class OpenAiCompatibleAdapter : IAiProviderAdapter
{
    private readonly OpenAiCompatibleApiClient _client;

    public OpenAiCompatibleAdapter(OpenAiCompatibleApiClient client)
    {
        _client = client;
    }

    public string AdapterType => "OpenAICompatible";

    public void ValidateConfiguration(AiProvider provider)
    {
        try
        {
            _client.ValidateConfiguration(provider);
        }
        catch (OpenAiClientException exception)
        {
            throw ToApplicationException(exception);
        }

        if (string.IsNullOrWhiteSpace(provider.ModelId))
        {
            throw new AiProviderConfigurationException("Model ID là bắt buộc.");
        }

        if (provider.TimeoutSeconds is < 5 or > 300)
        {
            throw new AiProviderConfigurationException("Timeout phải từ 5 đến 300 giây.");
        }
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(
        AiProvider provider,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenAiModelListResponse response = await _client.GetModelsAsync(
                provider,
                apiKey,
                cancellationToken);
            if (response.Data == null)
            {
                throw new AiProviderUnavailableException(
                    $"{provider.Name} trả danh sách model không hợp lệ.");
            }

            return response.Data
                .Select(model => model.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
        catch (OpenAiClientException exception)
        {
            throw ToApplicationException(exception);
        }
    }

    public async Task<string> CompleteAsync(
        AiProvider provider,
        string? apiKey,
        AiCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var openAiRequest = new OpenAiChatRequest(
            provider.ModelId,
            [
                new OpenAiChatMessage("system", request.SystemPrompt),
                new OpenAiChatMessage("user", request.UserPrompt)
            ],
            request.MaxTokens,
            0.3m);

        try
        {
            OpenAiChatResponse response = await _client.CompleteAsync(
                provider,
                apiKey,
                openAiRequest,
                cancellationToken);
            string? content = response.Choices.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiProviderUnavailableException(
                    $"{provider.Name} trả response không đúng chuẩn OpenAI.");
            }

            return content;
        }
        catch (OpenAiClientException exception)
        {
            throw ToApplicationException(exception);
        }
    }

    private static Exception ToApplicationException(OpenAiClientException exception)
    {
        if (exception.FailureKind == OpenAiClientFailureKind.Configuration)
        {
            return new AiProviderConfigurationException(exception.Message);
        }

        return new AiProviderUnavailableException(exception.Message);
    }
}
