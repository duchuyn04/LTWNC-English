using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;

namespace ltwnc.Tests.Services.Ai;

public sealed class AiCompletionRouterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly RecordingAiAdapter _adapter = new();

    // Tạo SQLite in-memory để test được ExecuteSqlInterpolated và schema relational.
    public AiCompletionRouterTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _context.Database.EnsureCreated();
    }

    // Chỉ provider đang bật, đã test thành công mới được thử và thử theo thứ tự ưu tiên Admin cấu hình.
    [Fact]
    public async Task CompleteAsync_TriesOnlyEnabledHealthyProvidersInPriorityOrder()
    {
        await SeedProviderAsync("Disabled", priority: 0, isEnabled: false, lastCheckSucceeded: true);
        await SeedProviderAsync("Unhealthy", priority: 1, isEnabled: true, lastCheckSucceeded: false);
        await SeedProviderAsync("Healthy B", priority: 20, isEnabled: true, lastCheckSucceeded: true);
        await SeedProviderAsync("Healthy A", priority: 10, isEnabled: true, lastCheckSucceeded: true);
        _adapter.Responses["Healthy A"] = "ok";

        AiCompletionResult result = await CreateRouter().CompleteAsync(Request());

        Assert.Equal("ok", result.Content);
        Assert.Equal(["Healthy A"], _adapter.Calls);
    }

    // Provider đầu lỗi tạm thời thì router chuyển sang provider kế tiếp và ghi fallback attempt an toàn.
    [Fact]
    public async Task CompleteAsync_WhenFirstProviderFails_UsesNextProviderAndLogsFallbackAttempt()
    {
        await SeedProviderAsync("Primary", priority: 0, isEnabled: true, lastCheckSucceeded: true);
        await SeedProviderAsync("Backup", priority: 1, isEnabled: true, lastCheckSucceeded: true);
        _adapter.Failures["Primary"] = new AiProviderUnavailableException("lỗi tạm thời");
        _adapter.Responses["Backup"] = "backup-ok";

        AiCompletionResult result = await CreateRouter().CompleteAsync(Request());
        List<AiOperationLog> logs = await _context.AiOperationLogs
            .OrderBy(log => log.Id)
            .ToListAsync();

        Assert.Equal("backup-ok", result.Content);
        Assert.Equal(["Primary", "Backup"], _adapter.Calls);
        Assert.False(logs[0].Succeeded);
        Assert.Equal(0, logs[0].FallbackAttempt);
        Assert.True(logs[1].Succeeded);
        Assert.Equal(1, logs[1].FallbackAttempt);
    }

    // Không có provider đủ điều kiện thì người học chỉ nhận thông báo chung, không lộ tên provider.
    [Fact]
    public async Task CompleteAsync_WhenNoEligibleProvider_ThrowsGenericMessageWithoutProviderNames()
    {
        await SeedProviderAsync("Provider Bi Mat", priority: 0, isEnabled: true, lastCheckSucceeded: false);

        AiProviderUnavailableException exception =
            await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
                CreateRouter().CompleteAsync(Request()));

        Assert.Contains("tạm thời không sẵn sàng", exception.Message);
        Assert.DoesNotContain("Provider Bi Mat", exception.Message);
    }

    // Timeout tổng thể dừng request, không thử tiếp vô hạn và vẫn ghi log vận hành không có prompt.
    [Fact]
    public async Task CompleteAsync_WhenOverallTimeoutExpires_StopsAndLogsSafeFailure()
    {
        await SeedProviderAsync("Slow", priority: 0, isEnabled: true, lastCheckSucceeded: true);
        await SeedProviderAsync("Backup", priority: 1, isEnabled: true, lastCheckSucceeded: true);
        _adapter.DelayUntilCancelledFor.Add("Slow");

        AiProviderUnavailableException exception =
            await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
                CreateRouter(overallTimeoutSeconds: 1).CompleteAsync(Request()));
        List<AiOperationLog> logs = await _context.AiOperationLogs.ToListAsync();

        Assert.Contains("tạm thời không sẵn sàng", exception.Message);
        Assert.Equal(["Slow"], _adapter.Calls);
        Assert.Single(logs);
        Assert.Equal("TotalTimeout", logs[0].FailureKind);
        Assert.Equal(0, logs[0].FallbackAttempt);
    }

    // Router lấy snapshot provider lúc bắt đầu nên request đang chạy vẫn hoàn tất nếu provider bị tắt giữa chừng.
    [Fact]
    public async Task CompleteAsync_AllowsRunningRequestToFinishWhenProviderIsDisabledMidFlight()
    {
        AiProvider provider = await SeedProviderAsync("Running", priority: 0, isEnabled: true, lastCheckSucceeded: true);
        _adapter.OnCallAsync = async calledProvider =>
        {
            AiProvider stored = await _context.AiProviders.SingleAsync(item => item.Id == calledProvider.Id);
            stored.IsEnabled = false;
            await _context.SaveChangesAsync();
        };
        _adapter.Responses["Running"] = "finished";

        AiCompletionResult result = await CreateRouter().CompleteAsync(Request());
        AiProvider storedProvider = await _context.AiProviders.SingleAsync(item => item.Id == provider.Id);

        Assert.Equal("finished", result.Content);
        Assert.False(storedProvider.IsEnabled);
    }

    // Thêm provider test vào database với health state rõ ràng.
    private async Task<AiProvider> SeedProviderAsync(
        string name,
        int priority,
        bool isEnabled,
        bool? lastCheckSucceeded)
    {
        var provider = new AiProvider
        {
            Name = name,
            AdapterType = _adapter.AdapterType,
            BaseUrl = "https://example.test/v1",
            ModelId = $"{name}-model",
            IsEnabled = isEnabled,
            Priority = priority,
            LastCheckSucceeded = lastCheckSucceeded,
            LastCheckedAt = DateTime.UtcNow
        };
        _context.AiProviders.Add(provider);
        await _context.SaveChangesAsync();
        return provider;
    }

    // Tạo router với cấu hình timeout tổng thể tùy biến cho test.
    private AiCompletionRouter CreateRouter(int overallTimeoutSeconds = 30)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:Routing:OverallTimeoutSeconds"] = overallTimeoutSeconds.ToString()
            })
            .Build();
        IDataProtectionProvider protection =
            DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
        return new AiCompletionRouter(
            _context,
            protection,
            [_adapter],
            TimeProvider.System,
            configuration);
    }

    // Request có prompt nhạy cảm để đảm bảo log vận hành không có nội dung prompt.
    private static AiCompletionRequest Request()
    {
        return new AiCompletionRequest("system-secret", "user-conversation", 32);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class RecordingAiAdapter : IAiProviderAdapter
    {
        public string AdapterType => "Fake";
        public List<string> Calls { get; } = new();
        public Dictionary<string, string> Responses { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Exception> Failures { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DelayUntilCancelledFor { get; } = new(StringComparer.Ordinal);
        public Func<AiProvider, Task>? OnCallAsync { get; set; }

        // Test router không dùng danh sách model.
        public Task<IReadOnlyList<string>> GetModelsAsync(
            AiProvider provider,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        // Giả lập completion theo tên provider và tôn trọng cancellation token của router.
        public async Task<string> CompleteAsync(
            AiProvider provider,
            string? apiKey,
            AiCompletionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(provider.Name);
            if (OnCallAsync != null)
            {
                await OnCallAsync(provider);
            }

            if (DelayUntilCancelledFor.Contains(provider.Name))
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            if (Failures.TryGetValue(provider.Name, out Exception? exception))
            {
                throw exception;
            }

            if (Responses.TryGetValue(provider.Name, out string? response))
            {
                return response;
            }

            return $"{provider.Name}-ok";
        }
    }
}
