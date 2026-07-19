using ltwnc.Services.AdminAchievements;

namespace ltwnc.Tests.Services.AdminAchievements;

public sealed class AdminAchievementSyncCoordinatorTests
{
    // Khóa user chặn lần chạy thứ hai trên cùng một người dùng.
    [Fact]
    public void TryStartUser_WhenSameUserAlreadyRunning_ReturnsNull()
    {
        var coordinator = new AdminAchievementSyncCoordinator();

        using IDisposable? first = coordinator.TryStartUser("user-1");
        IDisposable? second = coordinator.TryStartUser("user-1");

        Assert.NotNull(first);
        Assert.Null(second);
    }

    // Khóa toàn hệ thống chặn đồng bộ user và ngược lại để tránh kết quả chồng chéo.
    [Fact]
    public void TryStartSystem_WhenUserAlreadyRunning_ReturnsNull()
    {
        var coordinator = new AdminAchievementSyncCoordinator();

        using IDisposable? userLease = coordinator.TryStartUser("user-1");
        IDisposable? systemLease = coordinator.TryStartSystem();

        Assert.NotNull(userLease);
        Assert.Null(systemLease);
    }

    // Hai request bắt đầu cùng lúc không bao giờ được cùng giữ khóa user và khóa toàn hệ thống.
    [Fact]
    public async Task TryStartUserAndSystem_WhenStartedConcurrently_NeverBothAcquireLease()
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var coordinator = new AdminAchievementSyncCoordinator();
            using var startGate = new Barrier(2);
            IDisposable? userLease = null;
            IDisposable? systemLease = null;

            Task userTask = Task.Run(() =>
            {
                startGate.SignalAndWait();
                userLease = coordinator.TryStartUser("user-1");
            });
            Task systemTask = Task.Run(() =>
            {
                startGate.SignalAndWait();
                systemLease = coordinator.TryStartSystem();
            });

            await Task.WhenAll(userTask, systemTask);
            bool bothAcquiredLease = userLease != null && systemLease != null;
            userLease?.Dispose();
            systemLease?.Dispose();

            Assert.False(bothAcquiredLease);
        }
    }

    // Dispose giải phóng khóa để request sau có thể chạy tiếp.
    [Fact]
    public void Dispose_ReleasesScope()
    {
        var coordinator = new AdminAchievementSyncCoordinator();

        IDisposable? first = coordinator.TryStartUser("user-1");
        first?.Dispose();
        using IDisposable? second = coordinator.TryStartUser("user-1");

        Assert.NotNull(second);
    }
}
