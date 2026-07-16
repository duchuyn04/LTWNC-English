using ltwnc.Controllers;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ltwnc.Tests.Controllers;

public class FlashcardSetImportControllerTests
{
    private static (FlashcardSetController Controller, Mock<IFlashcardImportService> Import) Create(string? userId)
    {
        var import = new Mock<IFlashcardImportService>();
        var setService = new Mock<IFlashcardSetService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsAuthenticated).Returns(userId is not null);
        var controller = new FlashcardSetController(setService.Object, currentUser.Object, import.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new Mock<ITempDataProvider>().Object)
        };
        return (controller, import);
    }

    private static IFormFile File(string name = "cards.csv")
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("data"));
        return new FormFile(stream, 0, stream.Length, "file", name);
    }

    [Fact]
    public async Task Import_Unauthenticated_ReturnsChallenge()
    {
        var (controller, import) = Create(null);

        var result = await controller.Import(4, File());

        Assert.IsType<ChallengeResult>(result);
        import.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Import_Authenticated_DelegatesAndRedirects()
    {
        var (controller, import) = Create("owner");
        var file = File();
        import.Setup(x => x.ImportAsync(4, "owner", file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardImportResult { ImportedCount = 2, SkippedCount = 1 });

        var result = await controller.Import(4, file);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(4, redirect.RouteValues!["id"]);
        Assert.Equal(2, controller.TempData["ImportImportedCount"]);
        import.VerifyAll();
    }

    [Fact]
    public async Task Import_FileException_SetsErrorAndRedirects()
    {
        var (controller, import) = Create("owner");
        import.Setup(x => x.ImportAsync(4, "owner", It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlashcardImportException("bad file"));

        var result = await controller.Import(4, File());

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("bad file", controller.TempData["Error"]);
    }

    [Fact]
    public async Task Import_ResultErrors_AreSerializedForView()
    {
        var (controller, import) = Create("owner");
        import.Setup(x => x.ImportAsync(4, "owner", It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardImportResult
            {
                ImportedCount = 1,
                SkippedCount = 1,
                Errors = new[] { new FlashcardImportError { RowNumber = 3, Reason = "missing" } }
            });

        await controller.Import(4, File());

        var json = Assert.IsType<string>(controller.TempData["ImportErrors"]);
        Assert.Contains("\"RowNumber\":3", json);
        Assert.Contains("missing", json);
    }

    [Fact]
    public void Import_HasAntiforgeryAttribute()
    {
        var method = typeof(FlashcardSetController).GetMethod(nameof(FlashcardSetController.Import));
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }
}
