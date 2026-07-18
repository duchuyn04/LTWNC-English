using ltwnc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace ltwnc.Tests.Controllers;

public sealed class PrototypeControllerTests
{
    [Fact]
    public void Leaderboard_IsUnavailableOutsideDevelopment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Production");
        var controller = new PrototypeController(environment.Object);

        var result = controller.Leaderboard();

        Assert.IsType<NotFoundResult>(result);
    }
}
