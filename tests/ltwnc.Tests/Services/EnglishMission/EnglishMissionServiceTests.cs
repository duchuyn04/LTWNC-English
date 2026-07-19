using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using ltwnc.Services.EnglishMission;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.EnglishMission;

public sealed class EnglishMissionServiceTests
{
    [Fact]
    public async Task StartAndRespond_SnapshotsWordsAndPersistsTurn()
    {
        await using AppDbContext context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet { Id = 1, UserId = "user", Title = "Travel", IsPublic = false });
        for (int i = 1; i <= 3; i++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = i, FlashcardSetId = 1, FrontText = $"word{i}", BackText = $"nghĩa {i}",
                Pronunciation = "/x/", PartOfSpeech = "noun", ExampleSentence = $"Example {i}", ExampleMeaning = $"Ví dụ {i}", OrderIndex = i
            });
        }
        await context.SaveChangesAsync();

        var query = new StudyCardQueryService(context);
        var strategies = new IStudyModeStrategy[] { new EnglishMissionModeStrategy(query) };
        var study = new StudyService(context, strategies, new StudyModeStrategyResolver(strategies), TestStudyEvents.NoOpPublisher());
        var router = new QueueRouter(
            "{\"title\":\"At the airport\",\"situation\":\"Lost bag\",\"npcName\":\"Alex\",\"npcRole\":\"Staff\",\"openingLine\":\"How can I help?\",\"goals\":[{\"id\":\"report\",\"descriptionVi\":\"Báo sự cố\"}]}",
            "{\"npcReply\":\"What color is it?\",\"feedbackVi\":\"Rõ ý\",\"correctionEn\":null,\"correctionExplanationVi\":null,\"usedTargetWords\":[\"word1\"],\"achievedGoalIds\":[\"report\"],\"missionCompleted\":true}");
        var service = new EnglishMissionService(context, study, router, TestStudyEvents.NoOpPublisher(), TimeProvider.System);

        EnglishMissionStartResult started = await service.StartAsync("user", 1, "airport");
        EnglishMissionRespondResult response = await service.RespondAsync("user", 1, started.Mission.StudySessionId, "turn-1", "I lost word1");

        Assert.Equal(3, started.TargetWords.Count);
        Assert.True(response.TargetWords.Single(word => word.Term == "word1").IsUsed);
        Assert.Equal("What color is it?", response.Turn.NpcText);
        Assert.Equal("Completed", response.Mission.Status);
        Assert.Single(await context.EnglishMissionTurns.ToListAsync());

        EnglishMissionRespondResult retried = await service.RespondAsync(
            "user", 1, started.Mission.StudySessionId, "turn-1", "I lost word1");
        Assert.Equal(response.Turn.Id, retried.Turn.Id);
        Assert.Single(await context.EnglishMissionTurns.ToListAsync());
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class QueueRouter(params string[] responses) : IAiCompletionRouter
    {
        private readonly Queue<string> _responses = new(responses);
        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, Func<string, bool>? responseValidator = null, CancellationToken cancellationToken = default)
        {
            string content = _responses.Dequeue();
            Assert.True(responseValidator?.Invoke(content) ?? true);
            return Task.FromResult(new AiCompletionResult(content, 1, "Fake", "fake-model"));
        }
    }
}
