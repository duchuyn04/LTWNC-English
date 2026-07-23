using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using ltwnc.Services.EnglishMission;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;
using EnglishMissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Tests.Services.EnglishMission;

public sealed class EnglishMissionServiceTests
{
    // Người học không phải tác giả không được bắt đầu Nhiệm vụ tiếng Anh từ bộ thẻ đang bị cách ly.
    [Fact]
    public async Task StartAsync_QuarantinedPublicSet_DeniesNonOwner()
    {
        await using AppDbContext context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "owner",
            Title = "Quarantined set",
            IsPublic = true,
            ModerationStatus = FlashcardSetModerationStatus.Quarantined
        });
        await context.SaveChangesAsync();

        EnglishMissionService service = CreateService(context, new QueueRouter());

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.StartAsync("learner", 1, "airport"));

        Assert.Contains("cách ly", exception.Message);
    }

    // Người học không được tiếp tục nhiệm vụ nếu bộ thẻ bị cách ly sau lúc bắt đầu.
    [Fact]
    public async Task RespondAsync_SetQuarantinedAfterStart_DeniesNonOwner()
    {
        await using AppDbContext context = CreateContext();
        var set = new FlashcardSet
        {
            Id = 1,
            UserId = "owner",
            Title = "Public travel set",
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        for (int i = 1; i <= 3; i++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = i,
                FlashcardSetId = 1,
                FrontText = $"word{i}",
                BackText = $"nghĩa {i}",
                OrderIndex = i
            });
        }

        await context.SaveChangesAsync();

        var router = new QueueRouter(
            "{\"title\":\"At the airport\",\"situation\":\"Lost bag\",\"npcName\":\"Alex\",\"npcRole\":\"Staff\",\"openingLine\":\"How can I help?\",\"goals\":[{\"id\":\"report\",\"descriptionVi\":\"Báo sự cố\"}]}");
        EnglishMissionService service = CreateService(context, router);
        EnglishMissionStartResult started = await service.StartAsync("learner", 1, "airport");

        set.ModerationStatus = FlashcardSetModerationStatus.Quarantined;
        await context.SaveChangesAsync();

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RespondAsync(
                "learner",
                1,
                started.Mission.StudySessionId,
                "turn-after-quarantine",
                "I lost my bag"));

        Assert.Contains("cách ly", exception.Message);
    }

    // Người học không được hoàn thành nhiệm vụ nếu tác giả đã chuyển bộ thẻ về riêng tư.
    [Fact]
    public async Task CompleteAsync_PublicSetChangedToPrivate_DeniesNonOwner()
    {
        await using AppDbContext context = CreateContext();
        FlashcardSet set = await SeedPublicSetAsync(context);
        EnglishMissionService service = CreateService(
            context,
            new QueueRouter(StartPayload));
        EnglishMissionStartResult started = await service.StartAsync("learner", 1, "airport");

        set.IsPublic = false;
        await context.SaveChangesAsync();

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CompleteAsync("learner", 1, started.Mission.StudySessionId));

        Assert.Contains("quyền học", exception.Message);
    }

    // Trạng thái cách ly thay đổi trong lúc chờ AI phải được kiểm tra lại trước khi ghi lượt học.
    [Fact]
    public async Task RespondAsync_SetQuarantinedWhileWaitingForAi_DoesNotPersistTurn()
    {
        await using AppDbContext context = CreateContext();
        await SeedPublicSetAsync(context);
        var router = new QuarantineOnSecondRequestRouter(context, StartPayload, TurnPayload);
        EnglishMissionService service = CreateService(context, router);
        EnglishMissionStartResult started = await service.StartAsync("learner", 1, "airport");

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RespondAsync(
                "learner",
                1,
                started.Mission.StudySessionId,
                "concurrent-quarantine",
                "I lost my bag"));

        Assert.Contains("cách ly", exception.Message);
        Assert.Empty(await context.EnglishMissionTurns.ToListAsync());
    }

    // Bắt đầu và trả lời nhiệm vụ phải chụp lại từ mục tiêu rồi lưu lượt hội thoại.
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

        var router = new QueueRouter(
            "{\"title\":\"At the airport\",\"situation\":\"Lost bag\",\"npcName\":\"Alex\",\"npcRole\":\"Staff\",\"openingLine\":\"How can I help?\",\"goals\":[{\"id\":\"report\",\"descriptionVi\":\"Báo sự cố\"}]}",
            "{\"npcReply\":\"What color is it?\",\"feedbackVi\":\"Rõ ý\",\"correctionEn\":null,\"correctionExplanationVi\":null,\"usedTargetWords\":[\"word1\"],\"achievedGoalIds\":[\"report\"],\"missionCompleted\":true}");
        EnglishMissionService service = CreateService(context, router);

        EnglishMissionStartResult started = await service.StartAsync("user", 1, "airport");
        EnglishMissionRespondResult response = await service.RespondAsync("user", 1, started.Mission.StudySessionId, "turn-1", "I lost word1");

        Assert.Equal(3, started.TargetWords.Count);
        Assert.Equal(3, started.Mission.StudySession!.PlannedItemCount);
        Assert.True(response.TargetWords.Single(word => word.Term == "word1").IsUsed);
        Assert.Equal("What color is it?", response.Turn.NpcText);
        Assert.Equal("Completed", response.Mission.Status);
        Assert.Single(await context.EnglishMissionTurns.ToListAsync());

        EnglishMissionRespondResult retried = await service.RespondAsync(
            "user", 1, started.Mission.StudySessionId, "turn-1", "I lost word1");
        Assert.Equal(response.Turn.Id, retried.Turn.Id);
        Assert.Single(await context.EnglishMissionTurns.ToListAsync());
    }

    [Fact]
    public async Task CompleteAsync_counts_goals_achieved_in_previous_turns()
    {
        await using AppDbContext context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user",
            Title = "Travel",
            IsPublic = false
        });
        for (int i = 1; i <= 3; i++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = i,
                FlashcardSetId = 1,
                FrontText = $"word{i}",
                BackText = $"nghĩa {i}",
                OrderIndex = i
            });
        }

        await context.SaveChangesAsync();
        EnglishMissionService service = CreateService(
            context,
            new QueueRouter(StartPayload, TurnPayload));
        EnglishMissionStartResult started = await service.StartAsync("user", 1, "airport");

        await service.RespondAsync(
            "user",
            1,
            started.Mission.StudySessionId,
            "turn-1",
            "I lost word1");
        await service.CompleteAsync("user", 1, started.Mission.StudySessionId);

        EnglishMissionEntity mission = await context.EnglishMissions
            .Include(item => item.StudySession)
            .SingleAsync();
        Assert.Equal("Completed", mission.Status);
        Assert.Equal(76, mission.Score);
        Assert.Equal(3, mission.StudySession!.PlannedItemCount);
    }

    // Tạo service với StudyService thật để test quyền truy cập tại đúng public service seam.
    private static EnglishMissionService CreateService(
        AppDbContext context,
        IAiCompletionRouter router)
    {
        var query = new StudyCardQueryService(context);
        IStudyModeStrategy[] strategies = [new EnglishMissionModeStrategy(query)];
        var study = new StudyService(
            context,
            strategies,
            new StudyModeStrategyResolver(strategies),
            TestStudyEvents.NoOpPublisher());

        return new EnglishMissionService(
            context,
            study,
            router,
            TestStudyEvents.NoOpPublisher(),
            TimeProvider.System);
    }

    // Tạo database InMemory riêng để mỗi test không dùng chung dữ liệu.
    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    // Tạo bộ thẻ công khai đủ dữ liệu để bắt đầu một nhiệm vụ trong các test quyền truy cập.
    private static async Task<FlashcardSet> SeedPublicSetAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Id = 1,
            UserId = "owner",
            Title = "Public travel set",
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        for (int i = 1; i <= 3; i++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = i,
                FlashcardSetId = 1,
                FrontText = $"word{i}",
                BackText = $"nghĩa {i}",
                OrderIndex = i
            });
        }

        await context.SaveChangesAsync();
        return set;
    }

    // Trả lần lượt payload AI đã chuẩn bị sẵn và kiểm tra validator công khai của router.
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

    private const string StartPayload =
        "{\"title\":\"At the airport\",\"situation\":\"Lost bag\",\"npcName\":\"Alex\",\"npcRole\":\"Staff\",\"openingLine\":\"How can I help?\",\"goals\":[{\"id\":\"report\",\"descriptionVi\":\"Báo sự cố\"}]}";

    private const string TurnPayload =
        "{\"npcReply\":\"What color is it?\",\"feedbackVi\":\"Rõ ý\",\"correctionEn\":null,\"correctionExplanationVi\":null,\"usedTargetWords\":[\"word1\"],\"achievedGoalIds\":[\"report\"],\"missionCompleted\":false}";

    // Cách ly bộ thẻ ngay trước phản hồi AI thứ hai để mô phỏng thao tác Admin đồng thời.
    private sealed class QuarantineOnSecondRequestRouter(
        AppDbContext context,
        params string[] responses) : IAiCompletionRouter
    {
        private readonly Queue<string> _responses = new(responses);
        private int _requestCount;

        // Trả payload chuẩn bị sẵn và thay đổi trạng thái bộ thẻ khi request trả lời đang chạy.
        public async Task<AiCompletionResult> CompleteAsync(
            AiCompletionRequest request,
            Func<string, bool>? responseValidator = null,
            CancellationToken cancellationToken = default)
        {
            _requestCount++;
            if (_requestCount == 2)
            {
                FlashcardSet set = await context.FlashcardSets.SingleAsync(
                    item => item.Id == 1,
                    cancellationToken);
                set.ModerationStatus = FlashcardSetModerationStatus.Quarantined;
                await context.SaveChangesAsync(cancellationToken);
            }

            string content = _responses.Dequeue();
            Assert.True(responseValidator?.Invoke(content) ?? true);
            return new AiCompletionResult(content, 1, "Fake", "fake-model");
        }
    }
}
