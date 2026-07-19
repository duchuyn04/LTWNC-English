using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminEnglishMissions;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminEnglishMissionTests
{
    // Danh sách Admin chỉ hiển thị summary, không chứa transcript hoặc metadata vận hành AI.
    [Fact]
    public async Task Admin_List_ShowsSummaryWithoutConversationText()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "mission-admin-list@example.com";
        const string learnerEmail = "mission-learner-list@example.com";
        await factory.SeedUserAsync("mission_admin_list", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("mission_learner_list", learnerEmail);
        await SeedMissionAsync(
            factory,
            learnerEmail,
            "Summary only mission",
            factory.Clock.GetUtcNow().UtcDateTime);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await adminClient.GetAsync(
            "/Admin/EnglishMissions?search=Summary%20only");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Summary only mission", html);
        Assert.DoesNotContain("learner private utterance", html);
        Assert.DoesNotContain("InternalProvider", html);
        Assert.DoesNotContain("model-internal", html);

        HttpResponseMessage transcriptSearchResponse = await adminClient.GetAsync(
            "/Admin/EnglishMissions?search=private%20utterance");
        string transcriptSearchHtml =
            WebUtility.HtmlDecode(await transcriptSearchResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, transcriptSearchResponse.StatusCode);
        Assert.DoesNotContain("Summary only mission", transcriptSearchHtml);
    }

    // Admin chưa nhập loại vụ việc và lý do thì chỉ nhận form gate, audit chưa ghi và transcript chưa được trả.
    [Fact]
    public async Task Admin_DetailsWithoutReason_DoesNotReturnConversation()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "mission-admin-gate@example.com";
        const string learnerEmail = "mission-learner-gate@example.com";
        await factory.SeedUserAsync("mission_admin_gate", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("mission_learner_gate", learnerEmail);
        int missionId = await SeedMissionAsync(
            factory,
            learnerEmail,
            "Gate mission",
            factory.Clock.GetUtcNow().UtcDateTime);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await adminClient.GetAsync($"/Admin/EnglishMissions/{missionId}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("incidentType", html);
        Assert.DoesNotContain("learner private utterance", html);
        Assert.False(await AuditExistsAsync(factory, missionId));
    }

    // Admin có vụ việc hợp lệ thì audit được ghi trước khi trang chi tiết trả transcript đã lọc metadata provider.
    [Fact]
    public async Task Admin_DetailsWithReason_ReturnsFilteredConversationAndWritesAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "mission-admin-details@example.com";
        const string learnerEmail = "mission-learner-details@example.com";
        await factory.SeedUserAsync("mission_admin_details", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("mission_learner_details", learnerEmail);
        int missionId = await SeedMissionAsync(
            factory,
            learnerEmail,
            "Detailed mission",
            factory.Clock.GetUtcNow().UtcDateTime);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await adminClient.GetAsync(
            $"/Admin/EnglishMissions/{missionId}?incidentType=support&caseReference=SUP-09&reason=Support%20audit%20reason");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("learner private utterance", html);
        Assert.Contains("npc filtered answer", html);
        Assert.DoesNotContain("InternalProvider", html);
        Assert.DoesNotContain("model-internal", html);
        Assert.True(await AuditExistsAsync(factory, missionId));
    }

    // Người không phải Admin không được mở trang mission Admin.
    [Fact]
    public async Task NonAdmin_CannotAccessEnglishMissionAdminPage()
    {
        using var factory = new AdminWebApplicationFactory();
        const string learnerEmail = "mission-learner-forbidden@example.com";
        await factory.SeedUserAsync("mission_learner_forbidden", learnerEmail);

        using HttpClient learnerClient = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(learnerClient, learnerEmail);
        HttpResponseMessage response = await learnerClient.GetAsync("/Admin/EnglishMissions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Cleanup xóa nội dung sau 90 ngày nhưng vẫn giữ trạng thái, điểm, số lượt và aggregate JSON.
    [Fact]
    public async Task CleanupExpiredConversationContent_AfterNinetyDays_ClearsContentAndKeepsAggregates()
    {
        using var factory = new AdminWebApplicationFactory();
        const string learnerEmail = "mission-learner-cleanup@example.com";
        await factory.SeedUserAsync("mission_learner_cleanup", learnerEmail);
        DateTime createdAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddDays(-91);
        int missionId = await SeedMissionAsync(factory, learnerEmail, "Expired cleanup mission", createdAtUtc);

        AdminEnglishMissionCleanupResult result = await RunCleanupAsync(factory, batchSize: 10);
        EnglishMission mission = await LoadMissionAsync(factory, missionId);
        EnglishMissionTurn turn = mission.Turns.Single();

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.ClearedCount);
        Assert.NotNull(mission.ConversationContentDeletedAtUtc);
        Assert.Equal(string.Empty, mission.Situation);
        Assert.Equal(string.Empty, mission.OpeningLine);
        Assert.Equal("Completed", mission.Status);
        Assert.Equal(1, mission.TurnCount);
        Assert.Equal(88, mission.Score);
        Assert.Equal("[\"goal-retained\"]", mission.GoalsJson);
        Assert.Equal(string.Empty, turn.UserText);
        Assert.Equal(string.Empty, turn.NpcText);
        Assert.Equal("[\"term-retained\"]", turn.UsedWordsJson);
        Assert.Equal("[\"goal-retained\"]", turn.AchievedGoalsJson);
        Assert.Null(turn.ProviderName);
        Assert.Null(turn.ModelId);
    }

    // Mission có vụ việc đang mở được tạm giữ qua mốc 90 ngày.
    [Fact]
    public async Task CleanupExpiredConversationContent_OpenCaseHold_KeepsContentBeforeHoldDeadline()
    {
        using var factory = new AdminWebApplicationFactory();
        const string learnerEmail = "mission-learner-held@example.com";
        await factory.SeedUserAsync("mission_learner_held", learnerEmail);
        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        int missionId = await SeedMissionAsync(
            factory,
            learnerEmail,
            "Held mission",
            nowUtc.AddDays(-100),
            retentionHoldUntilUtc: nowUtc.AddDays(15));

        AdminEnglishMissionCleanupResult result = await RunCleanupAsync(factory, batchSize: 10);
        EnglishMission mission = await LoadMissionAsync(factory, missionId);
        EnglishMissionTurn turn = mission.Turns.Single();

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(0, result.ClearedCount);
        Assert.Null(mission.ConversationContentDeletedAtUtc);
        Assert.Equal("Mission situation details", mission.Situation);
        Assert.Equal("learner private utterance", turn.UserText);
    }

    // Hold không được vượt quá trần 12 tháng tính từ ngày mission được tạo.
    [Fact]
    public async Task CleanupExpiredConversationContent_HoldPastTwelveMonthCap_ClearsContent()
    {
        using var factory = new AdminWebApplicationFactory();
        const string learnerEmail = "mission-learner-cap@example.com";
        await factory.SeedUserAsync("mission_learner_cap", learnerEmail);
        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        int missionId = await SeedMissionAsync(
            factory,
            learnerEmail,
            "Cap mission",
            nowUtc.AddDays(-366),
            retentionHoldUntilUtc: nowUtc.AddDays(120));

        AdminEnglishMissionCleanupResult firstRun = await RunCleanupAsync(factory, batchSize: 10);
        AdminEnglishMissionCleanupResult secondRun = await RunCleanupAsync(factory, batchSize: 10);
        EnglishMission mission = await LoadMissionAsync(factory, missionId);

        Assert.Equal(1, firstRun.ClearedCount);
        Assert.Equal(0, secondRun.ScannedCount);
        Assert.Equal(0, secondRun.ClearedCount);
        Assert.NotNull(mission.ConversationContentDeletedAtUtc);
    }

    // Seed một English Mission đầy đủ relation để test HTTP/service bằng stack thật.
    private static async Task<int> SeedMissionAsync(
        AdminWebApplicationFactory factory,
        string learnerEmail,
        string title,
        DateTime createdAtUtc,
        DateTime? retentionHoldUntilUtc = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser learner = await userManager.FindByEmailAsync(learnerEmail)
            ?? throw new InvalidOperationException("Không tìm thấy learner test.");

        var set = new FlashcardSet
        {
            Title = "Mission seed set",
            Description = "Seed data for mission tests",
            UserId = learner.Id,
            IsPublic = false,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
        set.Flashcards.Add(new Flashcard
        {
            FrontText = "term-retained",
            BackText = "aggregate definition",
            PartOfSpeech = "noun",
            Pronunciation = "/test/",
            ExampleSentence = "Example sentence.",
            ExampleMeaning = "Example meaning.",
            OrderIndex = 0
        });
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();

        Flashcard card = set.Flashcards.Single();
        var session = new StudySession
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.EnglishMission,
            Score = 88,
            StartedAt = createdAtUtc,
            CompletedAt = createdAtUtc.AddMinutes(12),
            DurationSeconds = 720,
            PlannedItemCount = 1
        };
        context.StudySessions.Add(session);
        await context.SaveChangesAsync();

        DateTime completedAtUtc = createdAtUtc.AddMinutes(12);
        byte[] rowVersion = [1];
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""EnglishMissions""
                (""StudySessionId"", ""Topic"", ""Title"", ""Situation"", ""NpcName"", ""NpcRole"",
                 ""OpeningLine"", ""GoalsJson"", ""Status"", ""TurnCount"", ""Score"", ""CreatedAt"",
                 ""CompletedAt"", ""ConversationContentDeletedAtUtc"", ""ConversationRetentionHoldUntilUtc"",
                 ""ConversationRetentionCaseType"", ""ConversationRetentionCaseReference"", ""RowVersion"")
            VALUES
                ({session.Id}, {"travel"}, {title}, {"Mission situation details"}, {"Alex"}, {"Support agent"},
                 {"Mission opening line"}, {"[\"goal-retained\"]"}, {"Completed"}, {1}, {88}, {createdAtUtc},
                 {completedAtUtc}, {null}, {retentionHoldUntilUtc}, {"support"}, {"SUP-SEED"}, {rowVersion})");
        int missionId = await context.EnglishMissions
            .Where(mission => mission.StudySessionId == session.Id)
            .Select(mission => mission.Id)
            .SingleAsync();

        context.EnglishMissionTargetWords.Add(new EnglishMissionTargetWord
        {
            EnglishMissionId = missionId,
            FlashcardId = card.Id,
            Term = card.FrontText,
            Definition = card.BackText,
            PartOfSpeech = card.PartOfSpeech,
            IsUsed = true,
            FirstUsedTurn = 1
        });
        context.EnglishMissionTurns.Add(new EnglishMissionTurn
        {
            EnglishMissionId = missionId,
            TurnNumber = 1,
            ClientTurnId = $"turn-{missionId}",
            UserText = "learner private utterance",
            NpcText = "npc filtered answer",
            FeedbackVi = "feedback kept until cleanup",
            CorrectionEn = "corrected phrase",
            CorrectionExplanationVi = "correction explanation",
            UsedWordsJson = "[\"term-retained\"]",
            AchievedGoalsJson = "[\"goal-retained\"]",
            ProviderName = "InternalProvider",
            ModelId = "model-internal",
            CreatedAt = createdAtUtc.AddMinutes(1)
        });
        await context.SaveChangesAsync();

        return missionId;
    }

    // Gọi cleanup qua DI để test đúng service production.
    private static async Task<AdminEnglishMissionCleanupResult> RunCleanupAsync(
        AdminWebApplicationFactory factory,
        int batchSize)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IAdminEnglishMissionService service =
            scope.ServiceProvider.GetRequiredService<IAdminEnglishMissionService>();
        return await service.CleanupExpiredConversationContentAsync(batchSize);
    }

    // Load mission không tracking để kiểm tra dữ liệu đã commit sau cleanup.
    private static async Task<EnglishMission> LoadMissionAsync(
        AdminWebApplicationFactory factory,
        int missionId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EnglishMissions
            .AsNoTracking()
            .Include(mission => mission.Turns)
            .SingleAsync(mission => mission.Id == missionId);
    }

    // Kiểm tra audit truy cập hội thoại theo target mission.
    private static async Task<bool> AuditExistsAsync(
        AdminWebApplicationFactory factory,
        int missionId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string targetId = missionId.ToString();
        return await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == AdminAuditActions.EnglishMissionsViewConversation
            && log.Outcome == AdminAuditOutcome.Success
            && log.TargetId == targetId);
    }

    // Tạo client không tự follow redirect để kiểm tra đúng status HTTP.
    private static HttpClient CreateClient(AdminWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
