using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.Enums;
using ltwnc.Services.Auth;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ltwnc.Tests.Controllers;

public class CardActionsControllerTests
{
    [Fact]
    public async Task BatchAction_ajax_executes_factory_command_and_returns_json()
    {
        Fixture fixture = CreateController(ajax: true);
        ICardActionCommand command = Mock.Of<ICardActionCommand>();
        fixture.SetService.Setup(service => service.GetSetByIdAsync(9))
            .ReturnsAsync(new FlashcardSet { Id = 9, UserId = "user-1" });
        fixture.Factory.Setup(factory => factory.Create(
                "Star",
                9,
                "user-1",
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 3, 5 }))))
            .Returns(command);
        fixture.ActionService.Setup(service => service.ExecuteAsync(command))
            .ReturnsAsync(new CardActionLog { Id = 41 });

        IActionResult result = await fixture.Controller.BatchAction(
            9,
            BatchActionType.Star,
            new List<int> { 3, 5 });

        JsonResult json = Assert.IsType<JsonResult>(result);
        AssertProperty(json.Value, "success", true);
        AssertProperty(json.Value, "action", "Star");
        AssertProperty(json.Value, "undoLogId", 41);
        Assert.Equal(
            new[] { 3, 5 },
            Assert.IsAssignableFrom<IEnumerable<int>>(Property(json.Value, "cardIds")));
        fixture.Factory.VerifyAll();
        fixture.ActionService.VerifyAll();
    }

    [Fact]
    public async Task BatchAction_ajax_empty_selection_returns_bad_request_without_command()
    {
        Fixture fixture = CreateController(ajax: true);
        fixture.SetService.Setup(service => service.GetSetByIdAsync(9))
            .ReturnsAsync(new FlashcardSet { Id = 9, UserId = "user-1" });

        IActionResult result = await fixture.Controller.BatchAction(
            9,
            BatchActionType.Delete,
            new List<int>());

        BadRequestObjectResult error = Assert.IsType<BadRequestObjectResult>(result);
        AssertProperty(error.Value, "success", false);
        fixture.Factory.Verify(factory => factory.Create(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<int>>()), Times.Never);
        fixture.ActionService.Verify(
            service => service.ExecuteAsync(It.IsAny<ICardActionCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task BatchAction_ajax_service_error_returns_json_error()
    {
        Fixture fixture = CreateController(ajax: true);
        ICardActionCommand command = Mock.Of<ICardActionCommand>();
        fixture.SetService.Setup(service => service.GetSetByIdAsync(9))
            .ReturnsAsync(new FlashcardSet { Id = 9, UserId = "user-1" });
        fixture.Factory.Setup(factory => factory.Create(
                "Delete",
                9,
                "user-1",
                It.IsAny<IReadOnlyList<int>>()))
            .Returns(command);
        fixture.ActionService.Setup(service => service.ExecuteAsync(command))
            .ThrowsAsync(new InvalidOperationException("Batch failed"));

        IActionResult result = await fixture.Controller.BatchAction(
            9,
            BatchActionType.Delete,
            new List<int> { 3 });

        ObjectResult error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        AssertProperty(error.Value, "success", false);
        AssertProperty(error.Value, "message", "Batch failed");
        fixture.Factory.VerifyAll();
        fixture.ActionService.VerifyAll();
    }

    [Fact]
    public async Task BatchAction_normal_form_keeps_tempdata_redirect_fallback()
    {
        Fixture fixture = CreateController(ajax: false);
        ICardActionCommand command = Mock.Of<ICardActionCommand>();
        fixture.SetService.Setup(service => service.GetSetByIdAsync(9))
            .ReturnsAsync(new FlashcardSet { Id = 9, UserId = "user-1" });
        fixture.Factory.Setup(factory => factory.Create(
                "Unstar",
                9,
                "user-1",
                It.IsAny<IReadOnlyList<int>>()))
            .Returns(command);
        fixture.ActionService.Setup(service => service.ExecuteAsync(command))
            .ReturnsAsync(new CardActionLog { Id = 42 });

        IActionResult result = await fixture.Controller.BatchAction(
            9,
            BatchActionType.Unstar,
            new List<int> { 3 });

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal("FlashcardSet", redirect.ControllerName);
        Assert.Equal(42, fixture.Controller.TempData["UndoLogId"]);
        fixture.Factory.VerifyAll();
        fixture.ActionService.VerifyAll();
    }

    private static Fixture CreateController(bool ajax)
    {
        var actionService = new Mock<ICardActionService>();
        var factory = new Mock<ICardActionCommandFactory>();
        var setService = new Mock<IFlashcardSetService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns("user-1");

        var controller = new CardActionsController(
            actionService.Object,
            factory.Object,
            setService.Object,
            currentUser.Object);
        var httpContext = new DefaultHttpContext();
        if (ajax)
        {
            httpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(
            httpContext,
            Mock.Of<ITempDataProvider>());

        return new Fixture(controller, actionService, factory, setService);
    }

    private static void AssertProperty(object? value, string name, object expected) =>
        Assert.Equal(expected, Property(value, name));

    private static object? Property(object? value, string name) =>
        value?.GetType().GetProperty(name)?.GetValue(value);

    private sealed record Fixture(
        CardActionsController Controller,
        Mock<ICardActionService> ActionService,
        Mock<ICardActionCommandFactory> Factory,
        Mock<IFlashcardSetService> SetService);
}
