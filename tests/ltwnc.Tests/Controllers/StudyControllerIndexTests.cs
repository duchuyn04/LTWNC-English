using System.Security.Claims;
using ltwnc.Controllers;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services;
using ltwnc.Services.StudyModes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Controllers;

// Kiểm tra StudyController: trang chọn chế độ học, lưu bộ lọc, xóa bộ lọc
public class StudyControllerIndexTests
{
    private ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth"));
    }

    private (List<IStudyModeStrategy> Strategies, IStudyModeStrategyResolver Resolver) CreateStrategies(AppDbContext context)
    {
        var queryService = new StudyCardQueryService(context);
        var strategies = new List<IStudyModeStrategy>
        {
            new FlashcardModeStrategy(queryService),
            new DictationModeStrategy(queryService)
        };
        var resolver = new StudyModeStrategyResolver(strategies);
        return (strategies, resolver);
    }

    private StudyController CreateController(AppDbContext context, string? userId)
    {
        var userStore = new Mock<IUserStore<IdentityUser>>();
        var userManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        IdentityUser? user = userId == null ? null : new IdentityUser { Id = userId, UserName = "test@example.com" };
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var environment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environment.Setup(e => e.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), "ltwnc-tests"));

        var (strategies, resolver) = CreateStrategies(context);
        var setService = new FlashcardSetService(context, environment.Object);
        var studyService = new StudyService(context, strategies, resolver, TestStudyEvents.NoOpPublisher());
        var dictationService = new DictationService(context, resolver, TestStudyEvents.NoOpPublisher());

        // Tạo HttpContext tương ứng với user đã đăng nhập hoặc ẩn danh
        var httpContext = userId == null
            ? new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            : new DefaultHttpContext { User = CreateUser(userId) };

        var controller = new StudyController(studyService, dictationService, setService, userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
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
    // Index trả về view với StudyModeSelectorViewModel chứa thông tin bộ thẻ
    public async Task Index_ReturnsViewWithStudyModeSelectorViewModel()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var result = await controller.Index(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StudyModeSelectorViewModel>(viewResult.Model);
        Assert.Equal(1, model.SetId);
        Assert.Equal("Test Set", model.SetTitle);
        Assert.Equal(1, model.TotalCards);
    }

    [Fact]
    // Bộ thẻ không tồn tại hoặc không thuộc về user: redirect về trang chi tiết
    public async Task Index_UnknownSet_RedirectsToDetails()
    {
        await using var context = CreateContext();

        var controller = CreateController(context, "user-1");
        var result = await controller.Index(999);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("FlashcardSet", redirect.ControllerName);
    }

    [Fact]
    // User đăng nhập: tham số bộ lọc được lưu vào cài đặt
    public async Task Index_WithFilterParams_PersistsSettingsForAuthenticatedUser()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        await controller.Index(1, starredOnly: true, unlearnedOnly: false);

        var settings = await context.UserStudySettings.FirstAsync(s => s.UserId == "user-1");
        Assert.True(settings.StarredOnly);
        Assert.False(settings.UnlearnedOnly);
    }

    [Fact]
    // User ẩn danh: tham số bộ lọc không được lưu
    public async Task Index_WithFilterParams_DoesNotPersistForAnonymousUser()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, userId: null);
        await controller.Index(1, starredOnly: true, unlearnedOnly: false);

        Assert.Empty(context.UserStudySettings);
    }

    [Fact]
    // ClearFilters xóa bộ lọc và redirect về Index
    public async Task ClearFilters_ResetsFiltersAndRedirectsToIndex()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            StarredOnly = true,
            UnlearnedOnly = true
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");
        var result = await controller.ClearFilters(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var settings = await context.UserStudySettings.FirstAsync(s => s.UserId == "user-1");
        Assert.False(settings.StarredOnly);
        Assert.False(settings.UnlearnedOnly);
    }
}
