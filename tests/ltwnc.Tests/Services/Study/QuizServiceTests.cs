using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Study;

public sealed class QuizServiceTests
{
    [Fact]
    public async Task GetSetupAsync_ReturnsAvailableQuestionCount()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAndCardsAsync(context, cardCount: 5);
        QuizService service = CreateService(context);

        QuizSetupState actual = await service.GetSetupAsync(1, "user-1");

        Assert.Equal(5, actual.AvailableQuestionCount);
    }

    [Fact]
    public async Task StartNewAsync_WhenQuestionCountExceedsAvailableCards_ThrowsUnavailableException()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAndCardsAsync(context, cardCount: 5);
        QuizService service = CreateService(context);

        QuizUnavailableException exception = await Assert.ThrowsAsync<QuizUnavailableException>(() =>
            service.StartNewAsync(
                setId: 1,
                userId: "user-1",
                settings: new UserStudySettings(),
                timeLimitMinutes: 10,
                questionCount: 10));

        Assert.Equal(
            "Bộ thẻ hiện chỉ có 5 câu hỏi phù hợp. Vui lòng chọn tối đa 5 câu.",
            exception.Message);
        Assert.Empty(await context.StudySessions.ToListAsync());
    }

    private static QuizService CreateService(AppDbContext context)
    {
        var queryService = new StudyCardQueryService(context);
        var questionFactory = new QuizQuestionFactory(context, new Random(1));
        var strategy = new QuizModeStrategy(queryService, questionFactory);
        var resolver = new StudyModeStrategyResolver(new IStudyModeStrategy[] { strategy });

        return new QuizService(
            context,
            resolver,
            questionFactory,
            new RecordingPublisher());
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSetAndCardsAsync(AppDbContext context, int cardCount)
    {
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user-1",
            Title = "Quiz set"
        });

        context.Flashcards.AddRange(Enumerable.Range(1, cardCount).Select(index => new Flashcard
        {
            Id = index,
            FlashcardSetId = 1,
            FrontText = $"term-{index}",
            BackText = $"definition-{index}",
            Pronunciation = $"/{index}/",
            PartOfSpeech = "noun",
            OrderIndex = index
        }));

        await context.SaveChangesAsync();
    }

    private sealed class RecordingPublisher : IStudyEventPublisher
    {
        public Task PublishAsync(
            StudyEvent studyEvent,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
