using ltwnc.Controllers;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void NotFoundAction_ReturnsViewWith404Status()
    {
        var controller = new HomeController(new Mock<IFlashcardSetService>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        ViewResult result = Assert.IsType<ViewResult>(controller.NotFoundPage());

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal("NotFound", result.ViewName);
    }
}
