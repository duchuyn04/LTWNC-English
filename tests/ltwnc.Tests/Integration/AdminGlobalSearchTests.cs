using System.Net;
using System.Text.RegularExpressions;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminSearch;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminGlobalSearchTests
{
    // Tìm kiếm HTTP trả kết quả đa loại với metadata an toàn và link Admin tương ứng.
    [Fact]
    public async Task Admin_Search_ReturnsMultiTypeSafeResults()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-global-multi@example.com";
        await factory.SeedUserAsync("admin_global_multi", adminEmail, isAdmin: true, twoFactorEnabled: true);
        int missionId = await SeedSearchGraphAsync(factory, "1-learner-global", "1global@example.com");

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await adminClient.GetAsync("/Admin/Search?q=1");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-search-result-type=\"user\"", html);
        Assert.Contains("data-search-result-type=\"flashcard-set\"", html);
        Assert.Contains("1global@example.com", html);
        Assert.Contains("1 shared operations set", html);
        Assert.DoesNotContain("data-search-result-type=\"english-mission\"", html);

        HttpResponseMessage missionResponse =
            await adminClient.GetAsync($"/Admin/Search?q=EM-{missionId}");
        string missionHtml = WebUtility.HtmlDecode(await missionResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, missionResponse.StatusCode);
        Assert.Contains("data-search-result-type=\"english-mission\"", missionHtml);
        Assert.Contains($"EM-{missionId}", missionHtml);
        Assert.Contains($"/Admin/EnglishMissions/{missionId}", missionHtml);
    }

    // HTTP search giới hạn số kết quả theo từng loại, hiện link xem thêm và báo khi query bị cắt độ dài.
    [Fact]
    public async Task Admin_Search_LimitsPerTypeAndReportsTruncatedQuery()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-global-limit@example.com";
        await factory.SeedUserAsync("admin_global_limit", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string ownerId = await factory.GetUserIdAsync(adminEmail);
        await SeedManyFlashcardSetsAsync(factory, ownerId);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage limitedResponse =
            await adminClient.GetAsync("/Admin/Search?q=limit-probe&perTypeLimit=2");
        string limitedHtml = WebUtility.HtmlDecode(await limitedResponse.Content.ReadAsStringAsync());
        string longQuery = new('a', AdminGlobalSearchService.MaxQueryLength + 5);
        HttpResponseMessage truncatedResponse =
            await adminClient.GetAsync("/Admin/Search?q=" + longQuery);
        string truncatedHtml = WebUtility.HtmlDecode(await truncatedResponse.Content.ReadAsStringAsync());

        int setResultCount = Regex.Matches(
            limitedHtml,
            "data-search-result-type=\"flashcard-set\"").Count;
        Assert.Equal(HttpStatusCode.OK, limitedResponse.StatusCode);
        Assert.Equal(2, setResultCount);
        Assert.Contains("/Admin/Content?search=limit-probe", limitedHtml);
        Assert.Contains("Xem thêm", limitedHtml);
        Assert.Equal(HttpStatusCode.OK, truncatedResponse.StatusCode);
        Assert.Contains("Từ khóa đã được rút gọn", truncatedHtml);
    }

    // Service và HTTP không tìm trong transcript, đáp án học tập hoặc nội dung thẻ riêng tư.
    [Fact]
    public async Task Admin_Search_DoesNotLeakPrivateLearningOrCardContent()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-global-privacy@example.com";
        await factory.SeedUserAsync("admin_global_privacy", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await SeedSearchGraphAsync(factory, "privacy-learner", "privacy-learner@example.com");

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await adminClient.GetAsync("/Admin/Search?q=needle-secret");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        AdminGlobalSearchResult serviceResult = await SearchWithServiceAsync(factory, "needle-secret");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("learner needle-secret utterance", html);
        Assert.DoesNotContain("dictation needle-secret answer", html);
        Assert.DoesNotContain("private needle-secret card front", html);
        Assert.False(serviceResult.HasAnyResult);
    }

    // Link từ global search đến kết quả nhạy cảm vẫn dừng ở reason gate và chưa ghi audit khi thiếu lý do.
    [Fact]
    public async Task Admin_SearchResult_ForSensitiveMission_StillRequiresReasonAndAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-global-gate@example.com";
        await factory.SeedUserAsync("admin_global_gate", adminEmail, isAdmin: true, twoFactorEnabled: true);
        int missionId = await SeedSearchGraphAsync(factory, "gate-learner", "gate-learner@example.com");

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage searchResponse = await adminClient.GetAsync($"/Admin/Search?q=EM-{missionId}");
        string searchHtml = WebUtility.HtmlDecode(await searchResponse.Content.ReadAsStringAsync());
        HttpResponseMessage detailsResponse = await adminClient.GetAsync($"/Admin/EnglishMissions/{missionId}");
        string detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        Assert.Contains($"/Admin/EnglishMissions/{missionId}", searchHtml);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("incidentType", detailsHtml);
        Assert.DoesNotContain("learner needle-secret utterance", detailsHtml);
        Assert.False(await MissionAuditExistsAsync(factory, missionId));
    }

    // Seed graph có user, bộ thẻ riêng tư, phiên học, dictation answer và mission transcript để kiểm tra privacy.
    private static async Task<int> SeedSearchGraphAsync(
        AdminWebApplicationFactory factory,
        string learnerUserName,
        string learnerEmail)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var learner = new IdentityUser
        {
            Id = learnerUserName,
            UserName = learnerUserName,
            Email = learnerEmail
        };
        IdentityResult createResult = await userManager.CreateAsync(learner, "Testpass1");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        var set = new FlashcardSet
        {
            Title = "1 shared operations set",
            Description = "Safe summary only",
            UserId = learner.Id,
            IsPublic = false,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        set.Flashcards.Add(new Flashcard
        {
            FrontText = "private needle-secret card front",
            BackText = "private needle-secret card back",
            OrderIndex = 0
        });
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        Flashcard card = set.Flashcards.Single();

        var dictationSession = new StudySession
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation,
            Score = 10,
            StartedAt = nowUtc.AddMinutes(-30),
            CompletedAt = nowUtc.AddMinutes(-20),
            DurationSeconds = 600,
            PlannedItemCount = 1
        };
        context.StudySessions.Add(dictationSession);
        await context.SaveChangesAsync();
        context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            StudySessionId = dictationSession.Id,
            FlashcardId = card.Id,
            AnsweredText = "dictation needle-secret answer",
            IsCorrect = false,
            CreatedAt = nowUtc.AddMinutes(-25)
        });

        var missionSession = new StudySession
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.EnglishMission,
            Score = 88,
            StartedAt = nowUtc.AddMinutes(-15),
            CompletedAt = nowUtc.AddMinutes(-5),
            DurationSeconds = 600,
            PlannedItemCount = 1
        };
        context.StudySessions.Add(missionSession);
        await context.SaveChangesAsync();

        byte[] rowVersion = [1];
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""EnglishMissions""
                (""StudySessionId"", ""Topic"", ""Title"", ""Situation"", ""NpcName"", ""NpcRole"",
                 ""OpeningLine"", ""GoalsJson"", ""Status"", ""TurnCount"", ""Score"", ""CreatedAt"",
                 ""CompletedAt"", ""ConversationContentDeletedAtUtc"", ""ConversationRetentionHoldUntilUtc"",
                 ""ConversationRetentionCaseType"", ""ConversationRetentionCaseReference"", ""RowVersion"")
            VALUES
                ({missionSession.Id}, {"travel"}, {"Global search mission"}, {"Mission situation needle-secret"},
                 {"Alex"}, {"Support agent"}, {"Opening needle-secret"}, {"[]"}, {"Completed"}, {1}, {88},
                 {nowUtc.AddMinutes(-15)}, {nowUtc.AddMinutes(-5)}, {null}, {null}, {null}, {null}, {rowVersion})");

        int missionId = await context.EnglishMissions
            .Where(mission => mission.StudySessionId == missionSession.Id)
            .Select(mission => mission.Id)
            .SingleAsync();
        context.EnglishMissionTurns.Add(new EnglishMissionTurn
        {
            EnglishMissionId = missionId,
            TurnNumber = 1,
            ClientTurnId = $"global-search-turn-{missionId}",
            UserText = "learner needle-secret utterance",
            NpcText = "npc needle-secret answer",
            UsedWordsJson = "[]",
            AchievedGoalsJson = "[]",
            CreatedAt = nowUtc.AddMinutes(-14)
        });
        await context.SaveChangesAsync();

        return missionId;
    }

    // Seed nhiều bộ cùng prefix để test giới hạn per-type và link xem thêm không cần scan trên bộ nhớ.
    private static async Task SeedManyFlashcardSetsAsync(
        AdminWebApplicationFactory factory,
        string ownerId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        for (int index = 1; index <= 6; index++)
        {
            context.FlashcardSets.Add(new FlashcardSet
            {
                Title = $"limit-probe set {index}",
                Description = "Safe summary only",
                UserId = ownerId,
                IsPublic = true,
                CreatedAt = nowUtc.AddMinutes(index),
                UpdatedAt = nowUtc.AddMinutes(index)
            });
        }

        await context.SaveChangesAsync();
    }

    // Gọi service qua DI để kiểm tra cùng implementation với endpoint HTTP.
    private static async Task<AdminGlobalSearchResult> SearchWithServiceAsync(
        AdminWebApplicationFactory factory,
        string query)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IAdminGlobalSearchService service =
            scope.ServiceProvider.GetRequiredService<IAdminGlobalSearchService>();
        return await service.SearchAsync(new AdminGlobalSearchQuery(query));
    }

    // Kiểm tra audit hội thoại chưa được ghi khi Admin mới đi tới gate.
    private static async Task<bool> MissionAuditExistsAsync(
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

    // Tạo client không tự follow redirect để test đúng status HTTP.
    private static HttpClient CreateClient(AdminWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
