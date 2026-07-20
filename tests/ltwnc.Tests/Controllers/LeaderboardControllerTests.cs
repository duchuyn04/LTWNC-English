using ltwnc.Controllers;
using ltwnc.Models.ViewModels.Leaderboard;
using ltwnc.Services.Auth;
using ltwnc.Services.Leaderboard;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Tests.Controllers;

public sealed class LeaderboardControllerTests
{
    [Fact]
    public async Task Index_UsesViewerAndReturnsLeaderboardView()
    {
        var service = new Mock<ILeaderboardService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns("viewer-1");
        var model = new LeaderboardPageViewModel { PeriodDays = 7 };
        service
            .Setup(item => item.GetPageAsync(30, "viewer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        var controller = new LeaderboardController(service.Object, currentUser.Object);

        var result = await controller.Index(30);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        service.Verify(item => item.GetPageAsync(30, "viewer-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Index_DefaultsToSevenDaysForAnonymousViewer()
    {
        var service = new Mock<ILeaderboardService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns((string?)null);
        service
            .Setup(item => item.GetPageAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaderboardPageViewModel { PeriodDays = 7 });

        var controller = new LeaderboardController(service.Object, currentUser.Object);

        await controller.Index();

        service.Verify(item => item.GetPageAsync(7, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
