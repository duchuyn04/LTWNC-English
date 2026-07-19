using System.Net;
using System.Text;
using ltwnc.Data;
using ltwnc.Services.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Ai;

public sealed class AiProviderServiceTests
{
    [Fact]
    public async Task Discovery_OmitsAuthorizationWhenApiKeyIsEmpty()
    {
        var handler = new RecordingHandler("{\"data\":[{\"id\":\"model-a\"}]}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        var provider = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Local",
            BaseUrl = "http://localhost:1234/v1",
            ModelId = "model-a"
        });

        var models = await service.DiscoverModelsAsync(provider.Id);

        Assert.Equal(["model-a"], models);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task Discovery_SendsBearerWhenApiKeyIsConfigured()
    {
        var handler = new RecordingHandler("{\"data\":[{\"id\":\"model-a\"}]}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        var provider = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Remote",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            ApiKey = "secret-key"
        });

        await service.DiscoverModelsAsync(provider.Id);

        Assert.Equal("Bearer secret-key", handler.Authorization);
        Assert.NotEqual("secret-key", provider.EncryptedApiKey);
        Assert.Equal("-key", provider.ApiKeyLastFour);
    }

    [Fact]
    public async Task Save_RejectsPlainHttpForRemoteProvider()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, new AiProviderInput
        {
            Name = "Unsafe",
            BaseUrl = "http://example.com/v1",
            ModelId = "model-a"
        }));

        Assert.Contains("HTTPS", exception.Message);
    }

    [Fact]
    public async Task Save_RejectsPrivateOrMetadataNetworkAddresses()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: false);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, new AiProviderInput
        {
            Name = "Metadata",
            BaseUrl = "https://169.254.169.254/v1",
            ModelId = "model-a"
        }));

        Assert.Contains("mạng nội bộ", exception.Message);
    }

    private static AiProviderService CreateService(
        AppDbContext context,
        HttpMessageHandler handler,
        bool allowPrivateNetworks)
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ltwnc-provider-tests", Guid.NewGuid().ToString()));
        directory.Create();
        IDataProtectionProvider protection = DataProtectionProvider.Create(directory);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:AllowPrivateNetworks"] = allowPrivateNetworks.ToString()
            })
            .Build();
        var client = new OpenAiCompatibleClient(new FakeHttpClientFactory(handler), configuration);
        return new AiProviderService(context, protection, [new OpenAiCompatibleAdapter(client)], configuration);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
