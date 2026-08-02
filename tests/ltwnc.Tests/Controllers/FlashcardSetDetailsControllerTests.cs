using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.ContentReports;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public sealed class FlashcardSetDetailsControllerTests
{
    [Fact]
    public async Task DetailsMapsAuthorUsernameAndInitials()
    {
        Mock<IFlashcardSetService> sets = new();
        sets.Setup(service => service.GetAccessibleSetWithCardsAsync(12, null))
            .ReturnsAsync(new FlashcardSet
            {
                Id = 12,
                UserId = "owner-1",
                IsPublic = true,
                ModerationStatus = FlashcardSetModerationStatus.Active
            });

        Mock<ICurrentUser> currentUser = new();
        currentUser.SetupGet(service => service.UserId).Returns((string?)null);

        Mock<IAuthService> auth = new();
        auth.Setup(service => service.FindByIdAsync(
                "owner-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser
            {
                Id = "owner-1",
                UserName = "Nguyen An"
            });

        Mock<IContentReportService> reports = new();
        reports.Setup(service => service.GetReasonOptions())
            .Returns(Array.Empty<ContentReportReasonOption>());

        FlashcardSetController controller = new(
            sets.Object,
            currentUser.Object,
            new Mock<IFlashcardImportService>().Object,
            reports.Object,
            auth.Object);

        IActionResult result = await controller.Details(12);

        ViewResult view = Assert.IsType<ViewResult>(result);
        SetDetailViewModel model = Assert.IsType<SetDetailViewModel>(view.Model);
        Assert.Equal("Nguyen An", model.AuthorUsername);
        Assert.Equal("NA", model.AuthorInitials);
    }
}
