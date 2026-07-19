namespace ltwnc.Services.AdminEnglishMissions;

public sealed class EnglishMissionConversationCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnglishMissionConversationCleanupHostedService> _logger;

    // Hosted service chạy cleanup định kỳ bằng scope riêng để không giữ DbContext lâu.
    public EnglishMissionConversationCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EnglishMissionConversationCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Vòng lặp nền chỉ ghi số lượng đã quét/xóa, không ghi nội dung hội thoại bị dọn.
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

    // Chờ một khoảng ngắn sau khi app khởi động để migration/seed hoàn tất trước cleanup đầu tiên.
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

    // Chạy đúng một batch để giới hạn tải database và cho phép lần chạy sau tiếp tục an toàn.
    private async Task RunCleanupBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAdminEnglishMissionService service =
                scope.ServiceProvider.GetRequiredService<IAdminEnglishMissionService>();
            AdminEnglishMissionCleanupResult result =
                await service.CleanupExpiredConversationContentAsync(
                    AdminEnglishMissionService.DefaultCleanupBatchSize,
                    stoppingToken);

            _logger.LogInformation(
                "English mission conversation cleanup scanned {ScannedCount} missions and cleared {ClearedCount} missions.",
                result.ScannedCount,
                result.ClearedCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "English mission conversation cleanup failed before completing the current batch.");
        }
    }

    // Đợi lịch chạy kế tiếp, trả false khi ứng dụng đang dừng.
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
