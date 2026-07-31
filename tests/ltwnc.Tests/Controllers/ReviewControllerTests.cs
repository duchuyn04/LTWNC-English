using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Auth;
using ltwnc.Services.Review;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Tests.Controllers;

public sealed class ReviewControllerTests
{
    [Fact]
    public async Task Start_AuthenticatedUser_RedirectsToCreatedSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.StartAsync("user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17 });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start();

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task Session_ExistingSession_ReturnsReviewViewModel()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel
            {
                SessionId = 17,
                TotalCards = 1,
                Cards = new[]
                {
                    new ReviewCardViewModel
                    {
                        FlashcardId = 3,
                        FrontText = "hello",
                        BackText = "xin chào"
                    }
                }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Session(17);

        var view = Assert.IsType<ViewResult>(actual);
        var model = Assert.IsType<ReviewSessionViewModel>(view.Model);
        Assert.Equal("hello", Assert.Single(model.Cards).FrontText);
    }

    [Fact]
    public async Task Session_CompletedSession_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsCompleted = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Session(17);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
    }

    [Fact]
    public async Task Result_EndedSession_ReturnsSummaryView()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsEnded = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Result(17);

        var view = Assert.IsType<ViewResult>(actual);
        Assert.True(Assert.IsType<ReviewSessionViewModel>(view.Model).IsEnded);
    }

    [Fact]
    public async Task Rate_WithoutAnswerReveal_ReturnsConflict()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, false))
            .ThrowsAsync(new InvalidOperationException("Bạn cần hiện đáp án trước khi chọn mức nhớ."));
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, false);

        var conflict = Assert.IsType<ConflictObjectResult>(actual);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Rate_AuthenticatedUser_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, true))
            .ReturnsAsync(new ReviewRatingResult
            {
                Session = new ReviewSessionViewModel { SessionId = 17, IsCompleted = true },
                Progress = new ReviewProgressViewModel { Stage = ReviewStage.Reviewing }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task Rate_WhileSessionHasCardsRemaining_RedirectsBackToSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, true))
            .ReturnsAsync(new ReviewRatingResult
            {
                Session = new ReviewSessionViewModel { SessionId = 17, IsCompleted = false },
                Progress = new ReviewProgressViewModel { Stage = ReviewStage.Reviewing }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task End_ExistingSession_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.EndAsync("user-1", 17))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsEnded = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.End(17);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    private static ReviewController CreateController(
        string? userId,
        Mock<IReviewService> review)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        return new ReviewController(review.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
