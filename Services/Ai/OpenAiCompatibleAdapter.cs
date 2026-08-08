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
            _client.ValidateConfiguration(ToClientConfiguration(connection));
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

    public async Task<string> CompleteAsync(
        AiProviderConnection connection,
        string? apiKey,
        AiCompletionRequest request,
        CancellationToken cancellationToken)
    {
        bool isXiaomiMimo = IsXiaomiMimo(connection.BaseUrl);
        var openAiRequest = new OpenAiChatRequest(
            connection.ModelId,
            [
                new OpenAiChatMessage("system", request.SystemPrompt),
                new OpenAiChatMessage("user", request.UserPrompt)
            ],
            isXiaomiMimo ? null : request.MaxTokens,
            isXiaomiMimo ? request.MaxTokens : null,
            0.3m,
            new OpenAiResponseFormat("json_object"));

        try
        {
            OpenAiChatResponse response = await _client.CompleteAsync(
                ToClientConfiguration(connection),
                apiKey,
                openAiRequest,
                cancellationToken);
            string? content = response.Choices?.FirstOrDefault()?.Message?.Content;
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

    private static bool IsXiaomiMimo(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Host.Equals("xiaomimimo.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".xiaomimimo.com", StringComparison.OrdinalIgnoreCase));
    }

    // Chuyển Target configuration sang cấu hình riêng của Adaptee, không mang theo API key.
    private static OpenAiClientConfiguration ToClientConfiguration(AiProviderConnection connection)
    {
        return new OpenAiClientConfiguration(
            connection.Name,
            connection.BaseUrl,
            connection.TimeoutSeconds);
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
