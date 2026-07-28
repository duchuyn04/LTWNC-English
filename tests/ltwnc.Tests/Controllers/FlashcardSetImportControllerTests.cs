using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

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
        var controller = new FlashcardSetController(
            setService.Object,
            currentUser.Object,
            import.Object,
            new Mock<ltwnc.Services.ContentReports.IContentReportService>().Object)
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

    private static (
        FlashcardSetController Controller,
        Mock<IFlashcardImportService> Import,
        Mock<IFlashcardSetService> Sets) CreateForFileUpload(string? userId)
    {
        var import = new Mock<IFlashcardImportService>();
        var setService = new Mock<IFlashcardSetService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsAuthenticated).Returns(userId is not null);
        var controller = new FlashcardSetController(
            setService.Object,
            currentUser.Object,
            import.Object,
            new Mock<ltwnc.Services.ContentReports.IContentReportService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new Mock<ITempDataProvider>().Object)
        };
        return (controller, import, setService);
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
    public async Task Import_ResultErrors_AreCappedForTempDataAndReportOmittedCount()
    {
        var (controller, import) = Create("owner");
        FlashcardImportError[] errors = Enumerable.Range(1, 105)
            .Select(row => new FlashcardImportError { RowNumber = row, Reason = $"error {row}" })
            .ToArray();
        import.Setup(x => x.ImportAsync(4, "owner", It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardImportResult
            {
                SkippedCount = errors.Length,
                Errors = errors
            });

        await controller.Import(4, File());

        var json = Assert.IsType<string>(controller.TempData["ImportErrors"]);
        FlashcardImportError[] displayed = JsonSerializer.Deserialize<FlashcardImportError[]>(json)!;
        Assert.Equal(100, displayed.Length);
        Assert.Equal(100, displayed[^1].RowNumber);
        Assert.Equal(5, controller.TempData["ImportErrorsOmittedCount"]);
        Assert.Equal(105, controller.TempData["ImportSkippedCount"]);
    }

    [Fact]
    public void Import_HasAntiforgeryAttribute()
    {
        var method = typeof(FlashcardSetController).GetMethod(nameof(FlashcardSetController.Import));
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public async Task ImportFile_Unauthenticated_ReturnsChallenge()
    {
        var (controller, import, sets) = CreateForFileUpload(null);

        var result = await controller.ImportFile(4, File());

        Assert.IsType<ChallengeResult>(result);
        import.VerifyNoOtherCalls();
        sets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportFile_ValidRows_UsesAtomicBatchImportAndReturnsCreatedCards()
    {
        var (controller, import, sets) = CreateForFileUpload("owner");
        var file = File();
        import.Setup(x => x.ParseAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardFileParseResult
            {
                Rows = new[]
                {
                    new FlashcardImportRow
                    {
                        RowNumber = 2,
                        FrontText = "resilience",
                        BackText = "khả năng phục hồi",
                        Pronunciation = "/rɪˈzɪliəns/",
                        PartOfSpeech = "noun",
                        ExampleSentence = "Resilience takes practice.",
                        ExampleMeaning = "Sự kiên cường cần rèn luyện.",
                        Synonyms = "strength",
                        ImageUrl = "https://example.com/image.jpg"
                    }
                }
            });
        sets.Setup(x => x.BatchImportCardsAsync(
                4,
                It.Is<IReadOnlyList<BatchImportCardItem>>(cards =>
                    cards.Count == 1
                    && cards[0].FrontText == "resilience"
                    && cards[0].ImageUrl == "https://example.com/image.jpg"),
                true,
                "owner"))
            .ReturnsAsync(new List<Flashcard>
            {
                new()
                {
                    Id = 22,
                    FlashcardSetId = 4,
                    FrontText = "resilience",
                    BackText = "khả năng phục hồi",
                    OrderIndex = 0
                }
            });

        var result = await controller.ImportFile(4, file, replaceAll: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, ok.Value!.GetType().GetProperty("importedCount")!.GetValue(ok.Value));
        import.VerifyAll();
        sets.VerifyAll();
    }

    [Fact]
    public async Task ImportFile_FileLevelValidationError_DoesNotMutateCards()
    {
        var (controller, import, sets) = CreateForFileUpload("owner");
        import.Setup(x => x.ParseAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardFileParseResult
            {
                FileError = "Tệp thiếu cột bắt buộc."
            });

        var result = await controller.ImportFile(4, File());

        Assert.IsType<BadRequestObjectResult>(result);
        sets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportFile_InvalidFile_ReturnsBadRequestMessage()
    {
        var (controller, import, sets) = CreateForFileUpload("owner");
        import.Setup(x => x.ParseAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlashcardImportException("Chỉ hỗ trợ tệp CSV hoặc XLSX."));

        var result = await controller.ImportFile(4, File("cards.txt"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(
            "CSV",
            badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value)!.ToString());
        sets.VerifyNoOtherCalls();
    }

    [Fact]
    public void ImportFile_HasAntiforgeryAttribute()
    {
        var method = typeof(FlashcardSetController).GetMethod(nameof(FlashcardSetController.ImportFile));
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }
}
