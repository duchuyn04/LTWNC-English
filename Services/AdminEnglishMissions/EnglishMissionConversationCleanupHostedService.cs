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
        // 1. Lưu dependency `_scopeFactory` để các phương thức khác sử dụng.
        _scopeFactory = scopeFactory;
        // 2. Lưu dependency `_logger` để các phương thức khác sử dụng.
        _logger = logger;
    }

    // Vòng lặp nền chỉ ghi số lượng đã quét/xóa, không ghi nội dung hội thoại bị dọn.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Gọi `DelayBeforeFirstRunAsync` để thực hiện bước nghiệp vụ này.
        await DelayBeforeFirstRunAsync(stoppingToken);
        // 2. Khởi tạo `timer` với dữ liệu ban đầu cần thiết.
        using var timer = new PeriodicTimer(RunInterval);

        // 3. Tiếp tục lặp khi `!stoppingToken.IsCancellationRequested` còn đúng.
        while (!stoppingToken.IsCancellationRequested)
        {
            // 4. Gọi `RunCleanupBatchAsync` để thực hiện bước nghiệp vụ này.
            await RunCleanupBatchAsync(stoppingToken);
            // 5. Gọi `WaitForNextRunAsync` và lưu kết quả vào `shouldContinue`.
            bool shouldContinue = await WaitForNextRunAsync(timer, stoppingToken);
            // 6. Kiểm tra `!shouldContinue` để chọn nhánh xử lý phù hợp.
            if (!shouldContinue)
            {
                // 7. Thoát khỏi vòng lặp hoặc nhánh xử lý hiện tại.
                break;
            }
        }
    }

    // Chờ một khoảng ngắn sau khi app khởi động để migration/seed hoàn tất trước cleanup đầu tiên.
    private static async Task DelayBeforeFirstRunAsync(CancellationToken stoppingToken)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `Delay` để thực hiện bước nghiệp vụ này.
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Chạy đúng một batch để giới hạn tải database và cho phép lần chạy sau tiếp tục an toàn.
    private async Task RunCleanupBatchAsync(CancellationToken stoppingToken)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `CreateScope` và lưu kết quả vào `scope`.
            using IServiceScope scope = _scopeFactory.CreateScope();
            // 3. Gọi `GetRequiredService` và lưu kết quả vào `service`.
            IAdminEnglishMissionService service =
                scope.ServiceProvider.GetRequiredService<IAdminEnglishMissionService>();
            // 4. Gọi `CleanupExpiredConversationContentAsync` và lưu kết quả vào `result`.
            AdminEnglishMissionCleanupResult result =
                await service.CleanupExpiredConversationContentAsync(
                    AdminEnglishMissionService.DefaultCleanupBatchSize,
                    stoppingToken);

            // 5. Gọi `LogInformation` để thực hiện bước nghiệp vụ này.
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
            // 6. Gọi `LogError` để thực hiện bước nghiệp vụ này.
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
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Trả kết quả từ `WaitForNextTickAsync` cho nơi gọi.
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 3. Trả `false` cho nơi gọi.
            return false;
        }
    }
}
