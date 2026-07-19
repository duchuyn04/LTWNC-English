using System.Net;
using System.Text;
using ltwnc.Data;
using ltwnc.Models.Entities;
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

    // Test thất bại ba lần liên tiếp phải đánh dấu provider không ổn định; test thành công reset bộ đếm.
    [Fact]
    public async Task TestAsync_ThreeFailuresMarksProviderUnstableAndSuccessResetsCounter()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("fail-1")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("fail-2")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("fail-3")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}", Encoding.UTF8, "application/json")
            });
        await using AppDbContext context = CreateContext();
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        AiProviderOperationResult saveResult = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Health",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Reason = "Tao provider de test health"
        }, Actor());
        Assert.True(saveResult.Succeeded);
        AiProvider provider = await context.AiProviders.SingleAsync();

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => service.TestAsync(provider.Id));
        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => service.TestAsync(provider.Id));
        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => service.TestAsync(provider.Id));
        AiProvider afterFailures = await context.AiProviders.SingleAsync();
        int failureCountBeforeSuccess = afterFailures.ConsecutiveFailureCount;
        AiProviderHealthSnapshot failureSnapshot =
            (await service.GetHealthSnapshotsAsync()).Single(item => item.ProviderId == provider.Id);

        await service.TestAsync(provider.Id);
        AiProvider afterSuccess = await context.AiProviders.SingleAsync();

        Assert.Equal(3, failureCountBeforeSuccess);
        Assert.True(failureSnapshot.IsUnstable);
        Assert.Equal(0, afterSuccess.ConsecutiveFailureCount);
        Assert.True(afterSuccess.LastCheckSucceeded);
    }

    // Tỷ lệ lỗi chỉ tính trong cửa sổ 5 phút và chỉ kết luận khi đủ số mẫu tối thiểu.
    [Fact]
    public async Task GetHealthSnapshotsAsync_UsesFiveMinuteWindowMinimumSampleAndConfiguredThreshold()
    {
        await using AppDbContext context = CreateContext();
        var handler = new RecordingHandler("{}");
        var service = CreateService(context, handler, allowPrivateNetworks: true);
        AiProviderOperationResult saveResult = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Rate",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Reason = "Tao provider de test error rate"
        }, Actor());
        Assert.True(saveResult.Succeeded);
        AiProvider provider = await context.AiProviders.SingleAsync();
        DateTime now = DateTime.UtcNow;
        for (int index = 0; index < 20; index++)
        {
            bool succeeded = index >= 3;
            string? failureKind = null;
            if (!succeeded)
            {
                failureKind = "Timeout";
            }

            context.AiOperationLogs.Add(new AiOperationLog
            {
                ProviderId = provider.Id,
                ProviderName = provider.Name,
                ModelId = provider.ModelId,
                OccurredAtUtc = now.AddMinutes(-1),
                Operation = "Completion",
                Succeeded = succeeded,
                FailureKind = failureKind,
                LatencyMs = 25,
                FallbackAttempt = 0
            });
        }

        context.AiOperationLogs.Add(new AiOperationLog
        {
            ProviderId = provider.Id,
            ProviderName = provider.Name,
            ModelId = provider.ModelId,
            OccurredAtUtc = now.AddMinutes(-10),
            Operation = "Completion",
            Succeeded = false,
            FailureKind = "OldFailure",
            LatencyMs = 25,
            FallbackAttempt = 0
        });
        await context.SaveChangesAsync();

        AiProviderHealthSnapshot snapshot =
            (await service.GetHealthSnapshotsAsync()).Single(item => item.ProviderId == provider.Id);

        Assert.Equal(20, snapshot.SampleSize);
        Assert.Equal(15m, snapshot.ErrorRatePercent);
        Assert.True(snapshot.ErrorRateExceeded);
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

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Không còn response giả lập.");
            }

            // Trả lần lượt từng response để mô phỏng nhiều lần test cùng một provider.
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
