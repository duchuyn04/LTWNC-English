using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminAuditRetention;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Audit;

public sealed class AdminAuditRetentionServiceTests
{
    // Kiểm tra đúng ranh 12 tháng: chỉ bản ghi cũ hơn cutoff mới bị xóa.
    [Fact]
    public async Task CleanupExpiredAuditLogsAsync_DeletesOnlyOlderThanTwelveMonthBoundary()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        var service = new AdminAuditRetentionService(context, clock);
        DateTime cutoffUtc = clock.GetUtcNow().UtcDateTime.AddMonths(-12);
        context.AdminAuditLogs.AddRange(
            Log("expired", cutoffUtc.AddTicks(-1)),
            Log("boundary", cutoffUtc),
            Log("fresh", cutoffUtc.AddTicks(1)));
        await context.SaveChangesAsync();

        AdminAuditRetentionCleanupResult result =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 10);

        string[] remainingActions = await context.AdminAuditLogs
            .OrderBy(log => log.Action)
            .Select(log => log.Action)
            .ToArrayAsync();
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(cutoffUtc, result.CutoffUtc);
        Assert.DoesNotContain("expired", remainingActions);
        Assert.Contains("boundary", remainingActions);
        Assert.Contains("fresh", remainingActions);
    }

    // Kiểm tra batch limit và khả năng chạy lại cho đến khi hết dữ liệu quá hạn.
    [Fact]
    public async Task CleanupExpiredAuditLogsAsync_RespectsBatchLimitAndCanRerun()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        var service = new AdminAuditRetentionService(context, clock);
        DateTime cutoffUtc = clock.GetUtcNow().UtcDateTime.AddMonths(-12);
        context.AdminAuditLogs.AddRange(
            Log("old-1", cutoffUtc.AddDays(-3)),
            Log("old-2", cutoffUtc.AddDays(-2)),
            Log("old-3", cutoffUtc.AddDays(-1)));
        await context.SaveChangesAsync();

        AdminAuditRetentionCleanupResult firstRun =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 2);
        AdminAuditRetentionCleanupResult secondRun =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 2);
        AdminAuditRetentionCleanupResult thirdRun =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 2);

        Assert.Equal(2, firstRun.DeletedCount);
        Assert.Equal(1, secondRun.DeletedCount);
        Assert.Equal(0, thirdRun.DeletedCount);
        Assert.Equal(0, await context.AdminAuditLogs.CountAsync());
    }

    // Kiểm tra dữ liệu chưa quá hạn không bị xóa dù cleanup chạy lặp lại.
    [Fact]
    public async Task CleanupExpiredAuditLogsAsync_KeepsNonExpiredDataAcrossRepeatedRuns()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        var service = new AdminAuditRetentionService(context, clock);
        DateTime cutoffUtc = clock.GetUtcNow().UtcDateTime.AddMonths(-12);
        context.AdminAuditLogs.Add(Log("not-expired", cutoffUtc.AddMinutes(1)));
        await context.SaveChangesAsync();

        AdminAuditRetentionCleanupResult firstRun =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 10);
        AdminAuditRetentionCleanupResult secondRun =
            await service.CleanupExpiredAuditLogsAsync(batchSize: 10);

        Assert.Equal(0, firstRun.DeletedCount);
        Assert.Equal(0, secondRun.DeletedCount);
        Assert.Equal("not-expired", await context.AdminAuditLogs
            .Select(log => log.Action)
            .SingleAsync());
    }

    // Tạo audit tối thiểu để test retention không phụ thuộc dữ liệu nhạy cảm.
    private static AdminAuditLog Log(string action, DateTime occurredAtUtc)
    {
        return new AdminAuditLog
        {
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = "admin-id",
            ActorDisplay = "admin@example.com",
            Action = action,
            Outcome = AdminAuditOutcome.Success
        };
    }

    // Dùng database in-memory riêng cho từng test để kiểm tra batch idempotent đơn giản.
    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }
}
