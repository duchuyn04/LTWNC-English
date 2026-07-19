namespace ltwnc.Services.AdminAuditRetention;

public sealed class AdminAuditRetentionCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminAuditRetentionCleanupHostedService> _logger;

    // Hosted service chạy retention trong scope riêng để DbContext sống ngắn và dễ quan sát qua log vận hành.
    public AdminAuditRetentionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminAuditRetentionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Vòng lặp nền chỉ ghi cutoff, batch và số dòng đã xóa; không ghi nội dung audit hoặc metadata.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DelayBeforeFirstRunAsync(stoppingToken);
        using var timer = new PeriodicTimer(RunInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupBatchAsync(stoppingToken);
            bool shouldContinue = await WaitForNextRunAsync(timer, stoppingToken);
            if (!shouldContinue)
            {
                break;
            }
        }
    }

    // Chờ app ổn định sau khởi động để tránh tranh tài nguyên với migration/seed.
    private static async Task DelayBeforeFirstRunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Chạy một batch và log trạng thái đủ để phát hiện job fail hoặc không tiến triển.
    private async Task RunCleanupBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAdminAuditRetentionService service =
                scope.ServiceProvider.GetRequiredService<IAdminAuditRetentionService>();
            AdminAuditRetentionCleanupResult result =
                await service.CleanupExpiredAuditLogsAsync(
                    AdminAuditRetentionService.DefaultBatchSize,
                    stoppingToken);

            _logger.LogInformation(
                "Admin audit retention cleanup deleted {DeletedCount} logs before {CutoffUtc} with batch size {BatchSize}.",
                result.DeletedCount,
                result.CutoffUtc,
                result.BatchSize);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Admin audit retention cleanup failed before completing the current batch.");
        }
    }

    // Đợi lịch kế tiếp, trả false khi ứng dụng đang dừng.
    private static async Task<bool> WaitForNextRunAsync(
        PeriodicTimer timer,
        CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
