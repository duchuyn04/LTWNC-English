using System.Net;
using System.Text;
using System.Text.Json;
using ltwnc.Services.Ai;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Ai;

public sealed class OpenAiCompatibleAdapterTests
{
    [Fact]
    public async Task CompleteAsync_SendsOpenAiRequestAndExtractsContent()
    {
        (OpenAiCompatibleAdapter adapter, StubHttpMessageHandler handler) = CreateAdapter(
            _ => Response(HttpStatusCode.OK, """
                {"choices":[{"message":{"content":"hello from provider"}}]}
                """));

        string result = await adapter.CompleteAsync(
            Connection("https://127.0.0.1/v1", "test-model"),
            "secret-key",
            new AiCompletionRequest("system prompt", "user prompt", 800),
            CancellationToken.None);

        Assert.Equal("hello from provider", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("https://127.0.0.1/v1/chat/completions", handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret-key", handler.AuthorizationParameter);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = request.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal("system prompt", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("user prompt", root.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal(800, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
        Assert.Equal(0.3m, root.GetProperty("temperature").GetDecimal());
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task CompleteAsync_XiaomiMimoUsesMaxCompletionTokensOnly()
    {
        (OpenAiCompatibleAdapter adapter, StubHttpMessageHandler handler) = CreateAdapter(
            _ => Response(HttpStatusCode.OK, """
                {"choices":[{"message":{"content":"mimo response"}}]}
                """));

        string result = await adapter.CompleteAsync(
            Connection("https://api.xiaomimimo.com/v1", "mimo-model"),
            null,
            new AiCompletionRequest("system", "user", 640),
            CancellationToken.None);

        Assert.Equal("mimo response", result);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = request.RootElement;
        Assert.Equal(640, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_tokens", out _));
    }

    [Fact]
    public async Task CompleteAsync_InvalidOpenAiResponsesBecomeUnavailable()
    {
        string[] invalidResponses =
        [
            "",
            "{}",
            "{\"choices\":null}",
            "{\"choices\":[]}",
            "{\"choices\":[{}]}",
            "{\"choices\":[{\"message\":{}}]}",
            "{\"choices\":[{\"message\":{\"content\":\"\"}}]}",
            "not-json"
        ];

        foreach (string invalidResponse in invalidResponses)
        {
            (OpenAiCompatibleAdapter adapter, _) = CreateAdapter(
                _ => Response(HttpStatusCode.OK, invalidResponse));

            await Assert.ThrowsAsync<AiProviderUnavailableException>(() => adapter.CompleteAsync(
                Connection("https://127.0.0.1/v1"),
                null,
                new AiCompletionRequest("system", "user"),
                CancellationToken.None));
        }
    }

    [Fact]
    public async Task ValidateConfiguration_InvalidApiConfigurationBecomesConfigurationException()
    {
        (OpenAiCompatibleAdapter adapter, StubHttpMessageHandler handler) = CreateAdapter(
            _ => Response(HttpStatusCode.OK, "{}"));

        AiProviderConfigurationException exception = Assert.Throws<AiProviderConfigurationException>(
            () => adapter.ValidateConfiguration(Connection("ftp://provider.example/v1")));

        Assert.Contains("URL", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_HttpConfigurationFailureBecomesConfigurationException()
    {
        (OpenAiCompatibleAdapter adapter, _) = CreateAdapter(
            _ => Response(HttpStatusCode.Unauthorized, "{}"));

        await Assert.ThrowsAsync<AiProviderConfigurationException>(() => adapter.CompleteAsync(
            Connection("https://127.0.0.1/v1"),
            null,
            new AiCompletionRequest("system", "user"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_ConnectionFailureBecomesUnavailableException()
    {
        (OpenAiCompatibleAdapter adapter, _) = CreateAdapter(
            _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("connection failed")));

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => adapter.CompleteAsync(
            Connection("https://127.0.0.1/v1"),
            null,
            new AiCompletionRequest("system", "user"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_ProviderTimeoutBecomesUnavailableException()
    {
        (OpenAiCompatibleAdapter adapter, _) = CreateAdapter(
            _ => Task.FromException<HttpResponseMessage>(new TaskCanceledException("provider timeout")));

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => adapter.CompleteAsync(
            Connection("https://127.0.0.1/v1"),
            null,
            new AiCompletionRequest("system", "user"),
            CancellationToken.None));
    }

    [Fact]
    public void ValidateConfiguration_BlocksPrivateNetworkByDefault()
    {
        (OpenAiCompatibleAdapter adapter, StubHttpMessageHandler handler) = CreateAdapter(
            _ => Response(HttpStatusCode.OK, "{}"),
            allowPrivateNetworks: null);

        Assert.Throws<AiProviderConfigurationException>(
            () => adapter.ValidateConfiguration(Connection("http://localhost:20128/v1")));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void ValidateConfiguration_AllowsLocalhostOnlyWithExplicitOptIn()
    {
        (OpenAiCompatibleAdapter adapter, StubHttpMessageHandler handler) = CreateAdapter(
            _ => Response(HttpStatusCode.OK, "{}"),
            allowPrivateNetworks: true);

        adapter.ValidateConfiguration(Connection("http://localhost:20128/v1"));

        Assert.Equal(0, handler.CallCount);
    }

    private static (OpenAiCompatibleAdapter Adapter, StubHttpMessageHandler Handler) CreateAdapter(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> response,
        bool? allowPrivateNetworks = true)
    {
        StubHttpMessageHandler handler = new(response);
        HttpClient httpClient = new(handler);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(item => item.CreateClient("AiProvider")).Returns(httpClient);
        Dictionary<string, string?> values = [];
        if (allowPrivateNetworks.HasValue)
        {
            values["AiProviders:AllowPrivateNetworks"] = allowPrivateNetworks.Value.ToString();
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        OpenAiCompatibleApiClient client = new(factory.Object, configuration);
        return (new OpenAiCompatibleAdapter(client), handler);
    }

    private static AiProviderConnection Connection(
        string baseUrl,
        string modelId = "model-id")
        => new("Test provider", baseUrl, modelId, 30);

    private static Task<HttpResponseMessage> Response(HttpStatusCode statusCode, string body)
        => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public string? RequestBody { get; private set; }

        public string? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri?.ToString();
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return await response(request);
        }
    }
}
