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
        // 1. Lưu dependency `_scopeFactory` để các phương thức khác sử dụng.
        _scopeFactory = scopeFactory;
        // 2. Lưu dependency `_logger` để các phương thức khác sử dụng.
        _logger = logger;
    }

    // Vòng lặp nền chỉ ghi cutoff, batch và số dòng đã xóa; không ghi nội dung audit hoặc metadata.
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

    // Chờ app ổn định sau khởi động để tránh tranh tài nguyên với migration/seed.
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

    // Chạy một batch và log trạng thái đủ để phát hiện job fail hoặc không tiến triển.
    private async Task RunCleanupBatchAsync(CancellationToken stoppingToken)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `CreateScope` và lưu kết quả vào `scope`.
            using IServiceScope scope = _scopeFactory.CreateScope();
            // 3. Gọi `GetRequiredService` và lưu kết quả vào `service`.
            IAdminAuditRetentionService service =
                scope.ServiceProvider.GetRequiredService<IAdminAuditRetentionService>();
            // 4. Gọi `CleanupExpiredAuditLogsAsync` và lưu kết quả vào `result`.
            AdminAuditRetentionCleanupResult result =
                await service.CleanupExpiredAuditLogsAsync(
                    AdminAuditRetentionService.DefaultBatchSize,
                    stoppingToken);

            // 5. Gọi `LogInformation` để thực hiện bước nghiệp vụ này.
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
            // 6. Gọi `LogError` để thực hiện bước nghiệp vụ này.
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
