using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.CardActions;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.CardActions;

public sealed class CardActionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsCrossSetTargetBeforeMutation()
    {
        await using AppDbContext context = CreateContext();
        await SeedAsync(context);
        CardActionService service = CreateService(context);
        ICardActionCommand command = new DeleteCardsCommand(
            context,
            setId: 1,
            userId: "owner-1",
            cardIds: [1, 2]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(command));

        Assert.Equal(2, await context.Flashcards.CountAsync());
        Assert.Equal(2, await context.UserProgresses.CountAsync());
        Assert.Equal(2, await context.DictationSessionDetails.CountAsync());
        Assert.Equal(2, await context.EnglishMissionTargetWords.CountAsync());
        Assert.Empty(await context.CardActionLogs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNonOwnerBeforeMutation()
    {
        await using AppDbContext context = CreateContext();
        await SeedAsync(context);
        CardActionService service = CreateService(context);
        ICardActionCommand command = new StarCardsCommand(
            context,
            setId: 1,
            userId: "intruder",
            cardIds: [1]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteAsync(command));

        Assert.False((await context.Flashcards.SingleAsync(card => card.Id == 1)).IsStarred);
        Assert.Empty(await context.CardActionLogs.ToListAsync());
    }

    [Theory]
    [MemberData(nameof(InvalidTargetSets))]
    public async Task ExecuteAsync_RejectsMalformedTargetSetBeforeMutation(int[] cardIds)
    {
        await using AppDbContext context = CreateContext();
        await SeedAsync(context);
        CardActionService service = CreateService(context);
        ICardActionCommand command = new StarCardsCommand(
            context,
            setId: 1,
            userId: "owner-1",
            cardIds);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(command));

        Assert.Equal(2, await context.Flashcards.CountAsync());
        Assert.Empty(await context.CardActionLogs.ToListAsync());
    }

    [Theory]
    [InlineData("Star", false, true, false)]
    [InlineData("Unstar", true, false, false)]
    [InlineData("Star", false, true, true)]
    [InlineData("Unstar", true, false, true)]
    public async Task UndoAsync_RejectsMissingOrMovedTargetWithoutPartialRestore(
        string actionType,
        bool initialFirstCardStarred,
        bool actionState,
        bool moveTarget)
    {
        await using AppDbContext context = CreateContext();
        await SeedAsync(
            context,
            initialFirstCardStarred,
            !initialFirstCardStarred,
            secondCardSetId: 1);
        CardActionService service = CreateService(context);
        ICardActionCommand command = actionType == "Star"
            ? new StarCardsCommand(context, 1, "owner-1", [1, 2])
            : new UnstarCardsCommand(context, 1, "owner-1", [1, 2]);

        CardActionLog log = await service.ExecuteAsync(command);
        Assert.All(
            await context.Flashcards.Where(card => new[] { 1, 2 }.Contains(card.Id)).ToListAsync(),
            card => Assert.Equal(actionState, card.IsStarred));

        Flashcard changedCard = await context.Flashcards.SingleAsync(card => card.Id == 2);
        if (moveTarget)
        {
            changedCard.FlashcardSetId = 2;
        }
        else
        {
            context.Flashcards.Remove(changedCard);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UndoAsync(log.Id, "owner-1"));

        Assert.Equal(
            actionState,
            (await context.Flashcards.SingleAsync(card => card.Id == 1)).IsStarred);
        Assert.Null((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    public static IEnumerable<object[]> InvalidTargetSets()
    {
        yield return [Array.Empty<int>()];
        yield return [new[] { 0 }];
        yield return [new[] { -1 }];
        yield return [new[] { 1, 1 }];
        yield return [new[] { 999 }];
    }

    private static CardActionService CreateService(AppDbContext context)
    {
        CardActionCommandFactory factory = new(
        [
            new DeleteCardsCommandCreator(context),
            new StarCardsCommandCreator(context),
            new UnstarCardsCommandCreator(context)
        ]);
        return new CardActionService(context, factory);
    }

    private static async Task SeedAsync(
        AppDbContext context,
        bool firstCardStarred = false,
        bool secondCardStarred = false,
        int secondCardSetId = 2)
    {
        FlashcardSet ownedSet = new()
        {
            Id = 1,
            UserId = "owner-1",
            Title = "Owned set"
        };
        FlashcardSet foreignSet = new()
        {
            Id = 2,
            UserId = "other-owner",
            Title = "Foreign set"
        };
        Flashcard ownedCard = CreateCard(1, 1, "owned", firstCardStarred);
        Flashcard foreignCard = CreateCard(2, secondCardSetId, "foreign", secondCardStarred);
        context.FlashcardSets.AddRange(ownedSet, foreignSet);
        context.Flashcards.AddRange(ownedCard, foreignCard);
        context.StudySessions.AddRange(
            new StudySession
            {
                Id = 1,
                UserId = "owner-1",
                FlashcardSetId = 1
            },
            new StudySession
            {
                Id = 2,
                UserId = "other-owner",
                FlashcardSetId = secondCardSetId
            });
        context.UserProgresses.AddRange(
            new UserProgress { Id = 1, UserId = "owner-1", FlashcardId = 1 },
            new UserProgress { Id = 2, UserId = "other-owner", FlashcardId = 2 });
        context.DictationSessionDetails.AddRange(
            new DictationSessionDetail { Id = 1, StudySessionId = 1, FlashcardId = 1 },
            new DictationSessionDetail { Id = 2, StudySessionId = 2, FlashcardId = 2 });
        context.EnglishMissions.AddRange(
            CreateMission(1, 1),
            CreateMission(2, 2));
        context.EnglishMissionTargetWords.AddRange(
            new EnglishMissionTargetWord
            {
                Id = 1,
                EnglishMissionId = 1,
                FlashcardId = 1,
                Term = "owned",
                Definition = "sở hữu"
            },
            new EnglishMissionTargetWord
            {
                Id = 2,
                EnglishMissionId = 2,
                FlashcardId = 2,
                Term = "foreign",
                Definition = "ngoài bộ"
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static Flashcard CreateCard(
        int id,
        int setId,
        string text,
        bool isStarred)
        => new()
        {
            Id = id,
            FlashcardSetId = setId,
            FrontText = text,
            BackText = $"{text} meaning",
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = $"{text} sentence",
            ExampleMeaning = $"{text} example",
            IsStarred = isStarred,
            OrderIndex = id
        };

    private static EnglishMission CreateMission(int id, int studySessionId)
        => new()
        {
            Id = id,
            StudySessionId = studySessionId,
            Topic = "Topic",
            Title = "Title",
            Situation = "Situation",
            NpcName = "NPC",
            NpcRole = "Teacher",
            OpeningLine = "Hello",
            GoalsJson = "[]",
            Status = "Active"
        };

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
