using System.Security.Claims;
using System.Text.Json;
using ltwnc.Controllers;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Controllers;

// Fake IUrlHelper đơn giản để test không cần routing thật
public class FakeUrlHelper : IUrlHelper
{
    public string Action(UrlActionContext actionContext) => "/Study/1/Dictation/Result/1?action=DictationResult";

    public string? Content(string? contentPath) => contentPath;

    public bool IsLocalUrl(string? url) => true;

    public string RouteUrl(UrlRouteContext routeContext) => "/";

    public string? Link(string? routeName, object? values) => "/";

    public ActionContext ActionContext { get; } = new ActionContext();
}

public class StudyControllerDictationTests
{
    // Tạo user giả lập cho Controller
    private ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth"));
    }

    // Tạo controller với các dependency in-memory
    private StudyController CreateController(AppDbContext context, string userId)
    {
        var userStore = new Mock<IUserStore<IdentityUser>>();
        var userManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var user = new IdentityUser { Id = userId, UserName = "test@example.com" };
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        // Mock IWebHostEnvironment để FlashcardSetService không cần web root thật
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), "ltwnc-tests"));

        var setService = new FlashcardSetService(context, environment.Object);
        var studyService = new StudyService(context);
        var dictationService = new DictationService(context);

        var controller = new StudyController(studyService, dictationService, setService, userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(userId) }
            },
            Url = new FakeUrlHelper()
        };
        return controller;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedSetAndCardAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Id = 1,
            Title = "Test Set",
            UserId = "user-1",
            IsPublic = true
        };
        await context.FlashcardSets.AddAsync(set);

        var card = new Flashcard
        {
            Id = 1,
            FlashcardSetId = 1,
            FrontText = "hello",
            BackText = "xin chào",
            Pronunciation = "/həˈloʊ/",
            PartOfSpeech = "exclamation",
            ExampleSentence = "Hello, world!",
            ExampleMeaning = "Xin chào, thế giớ!",
            OrderIndex = 0
        };
        await context.Flashcards.AddAsync(card);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Dictation_Get_ReturnsViewWithModel()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var result = await controller.Dictation(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<DictationStudyViewModel>(viewResult.Model);
    }

    [Fact]
    public async Task DictationCheck_Post_CorrectAnswer_ReturnsSuccess()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);

        var result = await controller.DictationCheck(1, viewModel.SessionId, 1, "hello");

        var jsonResult = Assert.IsType<JsonResult>(result);
        var element = JsonSerializer.SerializeToElement(jsonResult.Value);
        Assert.True(element.GetProperty("success").GetBoolean());
        Assert.True(element.GetProperty("isCorrect").GetBoolean());
    }

    [Fact]
    public async Task DictationComplete_Post_ReturnsRedirectUrl()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);

        var result = await controller.DictationComplete(1, viewModel.SessionId, 100);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var element = JsonSerializer.SerializeToElement(jsonResult.Value);
        Assert.True(element.GetProperty("success").GetBoolean());
        Assert.Contains("DictationResult", element.GetProperty("redirectUrl").GetString());
    }

    [Fact]
    public async Task DictationResult_Get_ReturnsViewWithModel()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);
        await controller.DictationCheck(1, viewModel.SessionId, 1, "hello");
        await controller.DictationComplete(1, viewModel.SessionId, 100);

        var result = await controller.DictationResult(1, viewModel.SessionId);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DictationResultViewModel>(viewResult.Model);
        Assert.Equal(1, model.TotalCards);
        Assert.Equal(1, model.CorrectCount);
    }
}
