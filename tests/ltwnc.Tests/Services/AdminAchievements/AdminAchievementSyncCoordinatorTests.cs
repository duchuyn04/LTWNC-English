using ltwnc.Services.AdminAchievements;

namespace ltwnc.Tests.Services.AdminAchievements;

public sealed class AdminAchievementSyncCoordinatorTests
{
    // Khoa user chan lan chay thu hai tren cung mot user.
    [Fact]
    public void TryStartUser_WhenSameUserAlreadyRunning_ReturnsNull()
    {
        var coordinator = new AdminAchievementSyncCoordinator();

        using IDisposable? first = coordinator.TryStartUser("user-1");
        IDisposable? second = coordinator.TryStartUser("user-1");

        Assert.NotNull(first);
        Assert.Null(second);
    }

    // Khoa toan he thong chan sync user va nguoc lai de tranh ket qua chong cheo.
    [Fact]
    public void TryStartSystem_WhenUserAlreadyRunning_ReturnsNull()
    {
        var coordinator = new AdminAchievementSyncCoordinator();

        using IDisposable? userLease = coordinator.TryStartUser("user-1");
        IDisposable? systemLease = coordinator.TryStartSystem();

        Assert.NotNull(userLease);
        Assert.Null(systemLease);
    }

    // Dispose giai phong khoa de request sau co the chay tiep.
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
