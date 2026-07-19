using System.Net;
using System.Text;
using ltwnc.Data;
using ltwnc.Services.Ai;
using ltwnc.Services.Audit;
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
        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Local",
            BaseUrl = "http://localhost:1234/v1",
            ModelId = "model-a",
            Reason = "Tạo provider local để test models"
        }, Actor());
        Assert.True(result.Succeeded);

        var provider = await context.AiProviders.SingleAsync();

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
        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Remote",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            ApiKey = "secret-key",
            Reason = "Tạo provider remote có API key"
        }, Actor());
        Assert.True(result.Succeeded);

        var provider = await context.AiProviders.SingleAsync();

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

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Unsafe",
            BaseUrl = "http://example.com/v1",
            ModelId = "model-a",
            Reason = "Test URL không an toàn"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("HTTPS", result.Message);
    }

    [Fact]
    public async Task Save_RejectsPrivateOrMetadataNetworkAddresses()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: false);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Metadata",
            BaseUrl = "https://169.254.169.254/v1",
            ModelId = "model-a",
            Reason = "Test địa chỉ metadata"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("mạng nội bộ", result.Message);
    }

    [Fact]
    public async Task Save_WritesAuditWithoutPlainApiKey()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Audit",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            ApiKey = "secret-key",
            Reason = "Ghi audit khi tạo provider"
        }, Actor());

        Assert.True(result.Succeeded);
        var audit = await context.AdminAuditLogs.SingleAsync();
        Assert.Equal(AdminAuditActions.AiProvidersCreate, audit.Action);
        Assert.Equal(AdminAuditOutcome.Success, audit.Outcome);
        Assert.NotEqual("0", audit.TargetId);
        Assert.Equal("Ghi audit khi tạo provider", audit.Reason);
        Assert.DoesNotContain("secret-key", audit.MetadataJson ?? string.Empty);
    }

    [Fact]
    public async Task SetPrimary_RejectsStaleVersion()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        AiProviderOperationResult saveResult = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Primary",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Reason = "Tạo provider để chọn chính"
        }, Actor());
        Assert.True(saveResult.Succeeded);
        var provider = await context.AiProviders.SingleAsync();

        AiProviderOperationResult result = await service.SetPrimaryAsync(
            provider.Id,
            version: provider.Version - 1,
            "Dùng version cũ",
            Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("tải lại", result.Message);
    }

    [Fact]
    public async Task Disable_ReplacesHardDeleteAndWritesAudit()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        AiProviderOperationResult saveResult = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Disable",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Reason = "Tạo provider để vô hiệu hóa"
        }, Actor());
        Assert.True(saveResult.Succeeded);
        var provider = await context.AiProviders.SingleAsync();

        AiProviderOperationResult result = await service.SetEnabledAsync(
            provider.Id,
            enable: false,
            provider.Version,
            "Ngừng dùng provider này",
            Actor());

        Assert.True(result.Succeeded);
        Assert.Equal(1, await context.AiProviders.CountAsync());
        Assert.False(provider.IsEnabled);
        Assert.Contains(context.AdminAuditLogs, log => log.Action == AdminAuditActions.AiProvidersDisable);
    }

    private static AiProviderActorContext Actor()
    {
        return new AiProviderActorContext("admin-1", "Admin Test", "trace-1");
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
        var auditService = new AdminAuditService(context, TimeProvider.System);
        return new AiProviderService(
            context,
            protection,
            [new OpenAiCompatibleAdapter(client)],
            auditService,
            configuration);
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
