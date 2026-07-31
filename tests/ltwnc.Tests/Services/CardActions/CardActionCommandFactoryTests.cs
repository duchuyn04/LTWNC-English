using ltwnc.Data;
using ltwnc.Services.CardActions;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.CardActions;

public sealed class CardActionCommandFactoryTests
{
    [Theory]
    [InlineData("Delete", typeof(DeleteCardsCommand))]
    [InlineData("Star", typeof(StarCardsCommand))]
    [InlineData("Unstar", typeof(UnstarCardsCommand))]
    public async Task Create_UsesMatchingConcreteCreator(
        string actionType,
        Type expectedCommandType)
    {
        await using AppDbContext context = CreateContext();
        var factory = new CardActionCommandFactory(
        [
            new DeleteCardsCommandCreator(context),
            new StarCardsCommandCreator(context),
            new UnstarCardsCommandCreator(context)
        ]);

        ICardActionCommand command = factory.Create(actionType, 12, "user-1", [3, 7]);

        Assert.IsType(expectedCommandType, command);
        Assert.Equal(actionType, command.ActionType);
        Assert.Equal(12, command.SetId);
        Assert.Equal("user-1", command.UserId);
        Assert.Equal([3, 7], command.CardIds);
    }

    [Fact]
    public async Task Create_UnknownActionType_Throws()
    {
        await using AppDbContext context = CreateContext();
        var factory = new CardActionCommandFactory(
        [
            new DeleteCardsCommandCreator(context),
            new StarCardsCommandCreator(context),
            new UnstarCardsCommandCreator(context)
        ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => factory.Create("Move", 12, "user-1", [3]));

        Assert.Equal("Unknown action type: Move.", exception.Message);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
