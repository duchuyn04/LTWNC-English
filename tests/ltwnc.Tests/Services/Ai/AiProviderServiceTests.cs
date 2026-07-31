using System.Net;
using System.Text;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Ai;

public sealed class AiProviderServiceTests
{
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
    public async Task Save_RejectsMissingModelThroughAdapterValidation()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Missing model",
            BaseUrl = "https://example.test/v1",
            ModelId = " ",
            Reason = "Xác nhận model bắt buộc"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Equal("Model ID là bắt buộc.", result.Message);
    }

    [Fact]
    public async Task Save_RejectsXiaomiMimoWithoutApiKey()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "MiMo",
            BaseUrl = "https://api.xiaomimimo.com/v1",
            ModelId = "mimo-v2.5-pro",
            Reason = "Xác nhận MiMo bắt buộc có khóa"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("API key", result.Message);
        Assert.Empty(context.AiProviders);
    }

    [Fact]
    public async Task Save_RejectsXiaomiMimoTokenPlanForApplicationBackend()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "MiMo Token Plan",
            BaseUrl = "https://token-plan-sgp.xiaomimimo.com/v1",
            ModelId = "mimo-v2.5-pro",
            ApiKey = "tp-test-key",
            Reason = "Không dùng gói công cụ lập trình cho backend"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("pay-as-you-go", result.Message);
        Assert.Empty(context.AiProviders);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public async Task Save_RejectsTimeoutOutsideSupportedRangeThroughAdapterValidation(int timeoutSeconds)
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Invalid timeout",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            TimeoutSeconds = timeoutSeconds,
            Reason = "Xác nhận giới hạn timeout"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Equal("Timeout phải từ 5 đến 300 giây.", result.Message);
    }

    [Fact]
    public async Task Save_RejectsPriorityAlreadyUsedByAnotherProvider()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);
        context.AiProviders.Add(new AiProvider
        {
            Name = "Existing",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://existing.test/v1",
            ModelId = "model-existing",
            Priority = 1
        });
        await context.SaveChangesAsync();

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Duplicate priority",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Priority = 1,
            Reason = "Kiểm tra thứ tự ưu tiên không trùng"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Contains("provider khác sử dụng", result.Message);
        Assert.Single(context.AiProviders);
    }

    [Fact]
    public async Task Save_UsesSelectedAdapterToValidateConfiguration()
    {
        await using AppDbContext context = CreateContext();
        var adapter = new RejectingValidationAdapter();
        IConfiguration configuration = CreateConfiguration(allowPrivateNetworks: true);
        AiProviderService service = CreateService(context, [adapter], configuration);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Custom",
            AdapterType = adapter.AdapterType,
            BaseUrl = "https://example.test/v1",
            ModelId = "custom-model",
            Reason = "Xác nhận adapter tự kiểm tra cấu hình"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Equal("Cấu hình bị adapter từ chối.", result.Message);
        Assert.True(adapter.ValidateWasCalled);
        Assert.Empty(context.AiProviders);
    }

    [Fact]
    public async Task Save_RejectsUnregisteredAdapterType()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(null, new AiProviderInput
        {
            Name = "Unknown",
            AdapterType = "MissingAdapter",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-a",
            Reason = "Xác nhận adapter phải được đăng ký"
        }, Actor());

        Assert.False(result.Succeeded);
        Assert.Equal("Adapter MissingAdapter chưa được đăng ký.", result.Message);
        Assert.Empty(context.AiProviders);
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

    // Lỗi khi ghi audit phải rollback cả provider mới để cấu hình không tồn tại mà thiếu dấu vết.
    [Fact]
    public async Task Save_CreateWhenAuditWriteFails_RollsBackProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new FailOnSecondSaveInterceptor();
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var handler = new RecordingHandler("{}");
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            null,
            new AiProviderInput
            {
                Name = "Atomic provider",
                BaseUrl = "https://example.test/v1",
                ModelId = "model-a",
                Reason = "Kiểm tra rollback khi audit thất bại"
            },
            Actor()));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.AiProviders.ToListAsync());
        Assert.Empty(await context.AdminAuditLogs.ToListAsync());
    }

    // Audit tạo mới phải dùng Id do database cấp, không ghi giá trị mặc định trước lần lưu đầu.
    [Fact]
    public async Task Save_CreateOnRelationalDatabase_WritesGeneratedProviderIdToAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var handler = new RecordingHandler("{}");
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SaveAsync(
            null,
            new AiProviderInput
            {
                Name = "Generated id provider",
                BaseUrl = "https://example.test/v1",
                ModelId = "model-a",
                Reason = "Kiểm tra Id trong audit"
            },
            Actor());

        AiProvider provider = await context.AiProviders.SingleAsync();
        AdminAuditLog audit = await context.AdminAuditLogs.SingleAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(provider.Id.ToString(), audit.TargetId);
        Assert.NotEqual("0", audit.TargetId);
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
    public async Task SetPrimary_ReplacesCurrentPrimaryWithoutViolatingUniqueIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var currentPrimary = new AiProvider
        {
            Name = "Current primary",
            BaseUrl = "https://primary.test/v1",
            ModelId = "model-primary",
            IsPrimary = true,
            Priority = 1
        };
        var replacement = new AiProvider
        {
            Name = "Replacement",
            BaseUrl = "https://replacement.test/v1",
            ModelId = "model-replacement",
            Priority = 2
        };
        context.AiProviders.AddRange(currentPrimary, replacement);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context, new RecordingHandler("{}"), allowPrivateNetworks: true);

        AiProviderOperationResult result = await service.SetPrimaryAsync(
            replacement.Id,
            replacement.Version,
            "Thay nhà cung cấp chính",
            Actor());

        Assert.True(result.Succeeded);
        List<AiProvider> providers = await context.AiProviders.OrderBy(provider => provider.Id).ToListAsync();
        Assert.False(providers[0].IsPrimary);
        Assert.True(providers[1].IsPrimary);
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
    public async Task TestAsync_ThreeFailuresAndSuccessResetsCounter()
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
        await service.TestAsync(provider.Id);
        AiProvider afterSuccess = await context.AiProviders.SingleAsync();

        Assert.Equal(3, failureCountBeforeSuccess);
        Assert.Equal(0, afterSuccess.ConsecutiveFailureCount);
        Assert.True(afterSuccess.LastCheckSucceeded);
    }

    [Fact]
    public async Task TestAsync_XiaomiMimoWithoutApiKey_ReturnsConfigurationError()
    {
        var handler = new RecordingHandler("{}");
        await using AppDbContext context = CreateContext();
        AiProviderService service = CreateService(context, handler, allowPrivateNetworks: true);
        var provider = new AiProvider
        {
            Name = "MiMo",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://api.xiaomimimo.com/v1",
            ModelId = "mimo-v2.5-pro"
        };
        context.AiProviders.Add(provider);
        await context.SaveChangesAsync();

        AiProviderConfigurationException exception =
            await Assert.ThrowsAsync<AiProviderConfigurationException>(() => service.TestAsync(provider.Id));

        Assert.Contains("API key", exception.Message);
        Assert.False(provider.LastCheckSucceeded);
        Assert.Contains("API key", provider.LastError);
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
        IConfiguration configuration = CreateConfiguration(allowPrivateNetworks);
        var client = new OpenAiCompatibleApiClient(new FakeHttpClientFactory(handler), configuration);
        return CreateService(context, [new OpenAiCompatibleAdapter(client)], configuration, protection);
    }

    private static AiProviderService CreateService(
        AppDbContext context,
        IEnumerable<IAiProviderAdapter> adapters,
        IConfiguration configuration,
        IDataProtectionProvider? protection = null)
    {
        protection ??= DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ltwnc-provider-tests", Guid.NewGuid().ToString())));
        return new AiProviderService(
            context,
            protection,
            adapters,
            new AdminAuditService(context, TimeProvider.System));
    }

    private static IConfiguration CreateConfiguration(bool allowPrivateNetworks)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:AllowPrivateNetworks"] = allowPrivateNetworks.ToString()
            })
            .Build();
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RejectingValidationAdapter : IAiProviderAdapter
    {
        public string AdapterType => "Rejecting";
        public bool ValidateWasCalled { get; private set; }

        public void ValidateConfiguration(AiProviderConnection connection)
        {
            ValidateWasCalled = true;
            throw new AiProviderConfigurationException("Cấu hình bị adapter từ chối.");
        }

        public Task<string> CompleteAsync(
            AiProviderConnection connection,
            string? apiKey,
            AiCompletionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

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

    // Ném lỗi ở lần lưu thứ hai để mô phỏng provider đã có Id nhưng audit không ghi được.
    private sealed class FailOnSecondSaveInterceptor : SaveChangesInterceptor
    {
        private int _saveCount;

        // Đếm từng lần lưu bất đồng bộ và dừng đúng lần ghi provider cùng audit.
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 2)
            {
                throw new InvalidOperationException("Mô phỏng lỗi ghi audit.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
