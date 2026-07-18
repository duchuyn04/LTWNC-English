using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public class FlashcardsApiControllerTests
{
    private static (FlashcardsApiController Controller, Mock<IFlashcardSetService> Service) Create(string? userId)
    {
        var service = new Mock<IFlashcardSetService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsAuthenticated).Returns(userId is not null);
        var controller = new FlashcardsApiController(service.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, service);
    }

    [Fact]
    public async Task CreateSet_Unauthenticated_ReturnsChallenge()
    {
        var (controller, service) = Create(null);

        var result = await controller.CreateSet(new CreateSetRequest { Title = "Test" });

        Assert.IsType<ChallengeResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSet_Authenticated_ReturnsCreatedSet()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.CreateSetAsync("Test", null, false, "owner"))
            .ReturnsAsync(new FlashcardSet { Id = 7, Title = "Test" });

        var result = await controller.CreateSet(new CreateSetRequest { Title = "Test" });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(7, ((FlashcardSet)created.Value!).Id);
    }

    [Fact]
    public async Task GetSet_Unauthenticated_ReturnsChallenge()
    {
        var (controller, service) = Create(null);

        var result = await controller.GetSet(1);

        Assert.IsType<ChallengeResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetSet_Owned_ReturnsSet()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.GetOwnedSetAsync(1, "owner"))
            .ReturnsAsync(new FlashcardSet { Id = 1, Title = "Test" });

        var result = await controller.GetSet(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, ((FlashcardSet)ok.Value!).Id);
    }

    [Fact]
    public async Task GetSet_NotFound_ReturnsNotFound()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.GetOwnedSetAsync(1, "owner"))
            .ReturnsAsync((FlashcardSet?)null);

        var result = await controller.GetSet(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateSet_Authenticated_ReturnsNoContent()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.UpdateSetAsync(1, "Updated", "desc", true, "owner"))
            .Returns(Task.CompletedTask);

        var result = await controller.UpdateSet(1, new UpdateSetRequest
        {
            Title = "Updated",
            Description = "desc",
            IsPublic = true
        });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task CreateCard_Authenticated_ReturnsCreatedCard()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.AddCardAsync(1, "front", "back", null, null, null, null, null, null, null, false, "owner"))
            .ReturnsAsync(new Flashcard
            {
                Id = 5,
                FlashcardSetId = 1,
                FrontText = "front",
                BackText = "back",
                OrderIndex = 0
            });

        var result = await controller.CreateCard(new CreateCardRequest
        {
            SetId = 1,
            FrontText = "front",
            BackText = "back"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var card = Assert.IsType<CardResponse>(created.Value);
        Assert.Equal(5, card.Id);
        Assert.Equal(1, card.SetId);
    }

    [Fact]
    public async Task UpdateCard_Authenticated_ReturnsNoContent()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.UpdateCardAsync(5, "front", "back", null, null, null, null, null, null, null, false, false, "owner"))
            .ReturnsAsync(1);

        var result = await controller.UpdateCard(5, new UpdateCardRequest
        {
            Id = 5,
            SetId = 1,
            FrontText = "front",
            BackText = "back"
        });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteCard_Authenticated_ReturnsNoContent()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.DeleteCardAsync(5, "owner"))
            .ReturnsAsync(1);

        var result = await controller.DeleteCard(5);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ToggleStar_Authenticated_ReturnsStarStatus()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.ToggleStarAsync(5, "owner"))
            .ReturnsAsync(true);

        var result = await controller.ToggleStar(5);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var isStarred = (bool)value.GetType().GetProperty("isStarred")!.GetValue(value)!;
        Assert.True(isStarred);
    }

    [Fact]
    public async Task BatchImport_ReplaceAll_DeletesExistingThenCreates()
    {
        var (controller, service) = Create("owner");
        var existingSet = new FlashcardSet
        {
            Flashcards = new List<Flashcard>
            {
                new() { Id = 1, FlashcardSetId = 1, FrontText = "old", BackText = "old" }
            }
        };
        service.Setup(x => x.GetSetWithCardsAsync(1, "owner"))
            .ReturnsAsync(existingSet);
        service.Setup(x => x.DeleteCardAsync(1, "owner"))
            .ReturnsAsync(1);
        service.Setup(x => x.AddCardAsync(1, "new", "new", null, null, null, null, null, null, null, false, "owner"))
            .ReturnsAsync(new Flashcard { Id = 2, FlashcardSetId = 1, FrontText = "new", BackText = "new" });

        var result = await controller.BatchImport(new BatchImportRequest
        {
            SetId = 1,
            ReplaceAll = true,
            Cards = new List<CreateCardRequest>
            {
                new() { SetId = 1, FrontText = "new", BackText = "new" }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var cards = Assert.IsType<List<CardResponse>>(ok.Value);
        Assert.Single(cards);
        service.Verify(x => x.DeleteCardAsync(1, "owner"), Times.Once);
    }

    [Fact]
    public async Task Reorder_Authenticated_ReturnsNoContent()
    {
        var (controller, service) = Create("owner");
        service.Setup(x => x.ReorderCardsAsync(1, new[] { 3, 2, 1 }, "owner"))
            .Returns(Task.CompletedTask);

        var result = await controller.Reorder(new ReorderRequest
        {
            SetId = 1,
            OrderedCardIds = new[] { 3, 2, 1 }
        });

        Assert.IsType<NoContentResult>(result);
    }
}
