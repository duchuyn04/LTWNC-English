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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
        var studyService = new StudyService(context, Enumerable.Empty<IStudyModeStrategy>());
        var dictationService = new DictationService(context);

        var controller = new StudyController(studyService, dictationService, setService, userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(userId) }
            },
            Url = new FakeUrlHelper(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new Mock<ITempDataProvider>().Object)
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
            ExampleMeaning = "Xin chào, thế giới!",
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

    [Fact]
    public async Task Dictation_Get_ExampleSentenceMode_UsesSentencePromptAndSnapshotsMode()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var result = await controller.Dictation(1);

        var model = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Hello, world!", Assert.Single(model.Cards).PromptText);
        Assert.Equal(DictationContentMode.ExampleSentence, model.ContentMode);

        var session = await context.StudySessions.FindAsync(model.SessionId);
        Assert.Equal(DictationContentMode.ExampleSentence, session!.DictationContentMode);
    }

    [Fact]
    public async Task DictationCheck_ExampleSentenceSession_UsesSnapshotAfterSettingChanges()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        var settings = new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        };
        context.UserStudySettings.Add(settings);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var dictation = await controller.Dictation(1);
        var model = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictation).Model);

        settings.DictationContentMode = DictationContentMode.Vocabulary;
        await context.SaveChangesAsync();

        var result = await controller.DictationCheck(1, model.SessionId, 1, "hello world");

        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        Assert.True(json.GetProperty("isCorrect").GetBoolean());
        Assert.Equal("Hello, world!", json.GetProperty("correctAnswer").GetString());
        Assert.Equal("Xin chào, thế giới!", json.GetProperty("exampleMeaning").GetString());
        Assert.Equal(2, json.GetProperty("wordComparison").GetArrayLength());
    }

    [Fact]
    public async Task DictationResult_ExampleSentenceSession_UsesSnapshotAfterSettingChanges()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        var settings = new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        };
        context.UserStudySettings.Add(settings);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var dictation = await controller.Dictation(1);
        var studyModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictation).Model);
        await controller.DictationCheck(1, studyModel.SessionId, 1, "wrong answer");
        await controller.DictationComplete(1, studyModel.SessionId, 0);

        settings.DictationContentMode = DictationContentMode.Vocabulary;
        await context.SaveChangesAsync();

        var result = await controller.DictationResult(1, studyModel.SessionId);

        var model = Assert.IsType<DictationResultViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(DictationContentMode.ExampleSentence, model.ContentMode);
        var wrongCard = Assert.Single(model.WrongCards);
        Assert.Equal("Hello, world!", wrongCard.ExampleSentence);
        Assert.Equal("Xin chào, thế giới!", wrongCard.ExampleMeaning);
        Assert.Equal("hello", wrongCard.Term);
    }

    [Fact]
    public async Task Dictation_Get_ExampleSentenceMode_WithFilters_ShowsFilterMessageNotMissingSentenceMessage()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence,
            StarredOnly = true
        });
        await context.SaveChangesAsync();

        // Card has example sentence but is not starred
        var card = await context.Flashcards.FindAsync(1);
        card!.IsStarred = false;
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var result = await controller.Dictation(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Không có thẻ phù hợp với bộ lọc hiện tại.", controller.TempData["Message"]);
    }

    [Fact]
    public async Task Dictation_Get_ExampleSentenceMode_NoSentences_ShowsMissingSentenceMessage()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            DictationContentMode = DictationContentMode.ExampleSentence
        });
        await context.SaveChangesAsync();

        var card = await context.Flashcards.FindAsync(1);
        card!.ExampleSentence = "";
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var result = await controller.Dictation(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Bộ thẻ chưa có câu ví dụ để nghe chép.", controller.TempData["Message"]);
    }
}
