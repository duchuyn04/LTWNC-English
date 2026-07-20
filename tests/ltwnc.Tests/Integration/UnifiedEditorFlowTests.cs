using ltwnc.Controllers;
using ltwnc.Data;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Integration;

public class UnifiedEditorFlowTests
{
    private sealed class TestContext : IDisposable
    {
        public AppDbContext Context { get; }
        private readonly SqliteConnection _connection;

        public TestContext()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }

    private static IWebHostEnvironment MockEnvironment()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(x => x.WebRootPath).Returns(Path.GetTempPath());
        return env.Object;
    }

    private static FlashcardsApiController CreateApiController(AppDbContext context, string userId)
    {
        var service = new FlashcardSetService(context, MockEnvironment());
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsAuthenticated).Returns(true);
        return new FlashcardsApiController(service, currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task FullFlow_CreateSet_AddCards_Update_Reorder_Delete()
    {
        using var testContext = new TestContext();
        var context = testContext.Context;
        var controller = CreateApiController(context, "user-1");

        // Create set
        var setResult = await controller.CreateSet(new CreateSetRequest { Title = "Flow Test" });
        var set = Assert.IsType<CreatedAtActionResult>(setResult).Value as dynamic;
        int setId = set!.Id;

        // Add two cards
        var card1Result = await controller.CreateCard(new CreateCardRequest
        {
            SetId = setId,
            FrontText = "one",
            BackText = "một"
        });
        var card1 = Assert.IsType<CreatedAtActionResult>(card1Result).Value as CardResponse;

        var card2Result = await controller.CreateCard(new CreateCardRequest
        {
            SetId = setId,
            FrontText = "two",
            BackText = "hai"
        });
        var card2 = Assert.IsType<CreatedAtActionResult>(card2Result).Value as CardResponse;

        // Update first card
        await controller.UpdateCard(card1!.Id, new UpdateCardRequest
        {
            Id = card1.Id,
            SetId = setId,
            FrontText = "one updated",
            BackText = "một"
        });

        // Reorder
        await controller.Reorder(new ReorderRequest
        {
            SetId = setId,
            OrderedCardIds = new[] { card2!.Id, card1.Id }
        });

        // Delete second card
        await controller.DeleteCard(card2.Id);

        // Verify final state
        var setService = new FlashcardSetService(context, MockEnvironment());
        var finalSet = await setService.GetSetWithCardsAsync(setId, "user-1");
        Assert.Single(finalSet!.Flashcards);
        Assert.Equal("one updated", finalSet.Flashcards.First().FrontText);
        Assert.Equal(1, finalSet.Flashcards.First().OrderIndex);
    }
}
