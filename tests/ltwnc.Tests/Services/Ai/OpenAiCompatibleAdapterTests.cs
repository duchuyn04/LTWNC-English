using System.Net;
using System.Text;
using System.Text.Json;
using ltwnc.Services.Ai;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Ai;

public sealed class OpenAiCompatibleAdapterTests
{
    // Lỗi từ provider có thể phản chiếu prompt hoặc secret; Adapter không được đưa response body ra exception.
    [Fact]
    public async Task CompleteAsync_WhenProviderFails_DoesNotLeakResponseBody()
    {
        const string sensitiveBody = "system-secret user-secret api-secret";
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(sensitiveBody, Encoding.UTF8, "text/plain")
            }));
        IAiProviderAdapter adapter = CreateAdapter(handler);

        AiProviderUnavailableException exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            adapter.CompleteAsync(
                Connection(),
                "api-secret",
                new AiCompletionRequest("system-secret", "user-secret", 32),
                CancellationToken.None));

        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.DoesNotContain("system-secret", exception.Message);
        Assert.DoesNotContain("user-secret", exception.Message);
        Assert.DoesNotContain("api-secret", exception.Message);
    }

    // Target nhận contract của application; Adapter phải phát đúng contract OpenAI-compatible và đọc response tương ứng.
    [Fact]
    public async Task CompleteAsync_MapsApplicationRequestAndOpenAiResponse()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"adapter-ok\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        IAiProviderAdapter adapter = CreateAdapter(handler);

        string result = await adapter.CompleteAsync(
            Connection(),
            "api-secret",
            new AiCompletionRequest("system-instruction", "learner-message", 321),
            CancellationToken.None);

        Assert.Equal("adapter-ok", result);
        Assert.NotNull(requestBody);
        using JsonDocument document = JsonDocument.Parse(requestBody);
        JsonElement root = document.RootElement;
        Assert.Equal("model-test", root.GetProperty("model").GetString());
        Assert.Equal(321, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.3m, root.GetProperty("temperature").GetDecimal());
        JsonElement messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("system-instruction", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("learner-message", messages[1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CompleteAsync_WhenConfigurationIsRejected_ThrowsConfigurationError(
        HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("do-not-expose-this-body")
            }));
        IAiProviderAdapter adapter = CreateAdapter(handler);

        AiProviderConfigurationException exception =
            await Assert.ThrowsAsync<AiProviderConfigurationException>(() =>
                adapter.CompleteAsync(
                    Connection(),
                    "api-secret",
                    new AiCompletionRequest("system-secret", "user-secret"),
                    CancellationToken.None));

        Assert.Contains(((int)statusCode).ToString(), exception.Message);
        Assert.DoesNotContain("do-not-expose-this-body", exception.Message);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task CompleteAsync_WhenResponseHasNoContent_ThrowsUnavailableError(string responseBody)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            }));
        IAiProviderAdapter adapter = CreateAdapter(handler);

        AiProviderUnavailableException exception =
            await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
                adapter.CompleteAsync(
                    Connection(),
                    null,
                    new AiCompletionRequest("system", "user"),
                    CancellationToken.None));

        Assert.Contains("không đúng chuẩn OpenAI", exception.Message);
        Assert.DoesNotContain(responseBody, exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WhenTransportTimesOut_ThrowsUnavailableError()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("transport timeout")));
        IAiProviderAdapter adapter = CreateAdapter(handler);

        AiProviderUnavailableException exception =
            await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
                adapter.CompleteAsync(
                    Connection(),
                    null,
                    new AiCompletionRequest("system", "user"),
                    CancellationToken.None));

        Assert.Contains("hết thời gian chờ", exception.Message);
        Assert.DoesNotContain("transport timeout", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WhenTransportFails_ThrowsUnavailableError()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("network-secret")));
        IAiProviderAdapter adapter = CreateAdapter(handler);

        AiProviderUnavailableException exception =
            await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
                adapter.CompleteAsync(
                    Connection(),
                    null,
                    new AiCompletionRequest("system", "user"),
                    CancellationToken.None));

        Assert.Contains("Không thể kết nối", exception.Message);
        Assert.DoesNotContain("network-secret", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WhenCallerCancels_PreservesCancellation()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        IAiProviderAdapter adapter = CreateAdapter(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.CompleteAsync(
                Connection(),
                null,
                new AiCompletionRequest("system", "user"),
                cancellation.Token));
    }

    private static IAiProviderAdapter CreateAdapter(HttpMessageHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:AllowPrivateNetworks"] = "true"
            })
            .Build();
        var client = new OpenAiCompatibleApiClient(new FakeHttpClientFactory(handler), configuration);
        return new OpenAiCompatibleAdapter(client);
    }

    private static AiProviderConnection Connection()
    {
        return new AiProviderConnection(
            "Provider Test",
            "https://example.test/v1",
            "model-test",
            30);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return respond(request, cancellationToken);
        }
    }
}
