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

    public void ValidateConfiguration(AiProviderConnection connection)
    {
        try
        {
            _client.ValidateConfiguration(connection);
        }
        catch (OpenAiClientException exception)
        {
            throw ToApplicationException(exception);
        }

        if (string.IsNullOrWhiteSpace(connection.ModelId))
        {
            throw new AiProviderConfigurationException("Model ID là bắt buộc.");
        }

        if (connection.TimeoutSeconds is < 5 or > 300)
        {
            throw new AiProviderConfigurationException("Timeout phải từ 5 đến 300 giây.");
        }
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(
        AiProviderConnection connection,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenAiModelListResponse response = await _client.GetModelsAsync(
                connection,
                apiKey,
                cancellationToken);
            if (response.Data == null)
            {
                throw new AiProviderUnavailableException(
                    $"{connection.Name} trả danh sách model không hợp lệ.");
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
        AiProviderConnection connection,
        string? apiKey,
        AiCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var openAiRequest = new OpenAiChatRequest(
            connection.ModelId,
            [
                new OpenAiChatMessage("system", request.SystemPrompt),
                new OpenAiChatMessage("user", request.UserPrompt)
            ],
            request.MaxTokens,
            0.3m);

        try
        {
            OpenAiChatResponse response = await _client.CompleteAsync(
                connection,
                apiKey,
                openAiRequest,
                cancellationToken);
            string? content = response.Choices.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiProviderUnavailableException(
                    $"{connection.Name} trả response không đúng chuẩn OpenAI.");
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
