using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminAchievementTests
{
    // Trang Admin hien catalog tu source code, so nguoi da nhan va ket qua theo user.
    [Fact]
    public async Task Index_RendersCatalogRecipientCountsAndUserResults()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-achievements-list@example.com";
        const string learnerEmail = "learner-achievements-list@example.com";
        await factory.SeedUserAsync("admin_achievements_list", adminEmail, isAdmin: true);
        await factory.SeedUserAsync("learner_achievements_list", learnerEmail);
        string learnerId = await factory.GetUserIdAsync(learnerEmail);
        await SeedMasteredCardsAsync(factory, learnerId, 1);
        await SeedAchievementAsync(factory, learnerId, AchievementCatalog.FirstCardMastered);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Achievements?search=learner-achievements-list");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Danh muc thanh tich", html);
        Assert.Contains(AchievementCatalog.FirstCardMastered, html);
        Assert.Contains("1 nguoi dung", html);
        Assert.Contains(learnerEmail, html);
        Assert.Contains("Day du", html);
    }

    // Trang khong de lo form sua dinh nghia, cap thu cong hoac thu hoi thanh tich.
    [Fact]
    public async Task Index_DoesNotExposeManualGrantRevokeOrDefinitionEditActions()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-achievements-readonly@example.com";
        await factory.SeedUserAsync("admin_achievements_readonly", adminEmail, isAdmin: true);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Achievements");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("/Grant", html);
        Assert.DoesNotContain("/Revoke", html);
        Assert.DoesNotContain("/Delete", html);
        Assert.DoesNotContain("/Edit", html);
        Assert.DoesNotContain("Cap thu cong", html);
        Assert.DoesNotContain("Thu hoi", html);
    }

    // Dong bo user khoi phuc thanh tich thieu tu UserProgress va chay lai khong tao trung.
    [Fact]
    public async Task ResyncUser_RestoresMissingAchievementsAndIsIdempotent()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-achievements-resync@example.com";
        const string learnerEmail = "learner-achievements-resync@example.com";
        await factory.SeedUserAsync("admin_achievements_resync", adminEmail, isAdmin: true);
        await factory.SeedUserAsync("learner_achievements_resync", learnerEmail);
        string learnerId = await factory.GetUserIdAsync(learnerEmail);
        await SeedMasteredCardsAsync(factory, learnerId, 10);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        await PostUserResyncAsync(client, learnerId);
        await PostUserResyncAsync(client, learnerId);

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<string> codes = await context.UserAchievements
            .Where(achievement => achievement.UserId == learnerId)
            .Select(achievement => achievement.Code)
            .ToListAsync();
        int firstCardRows = await context.UserAchievements.CountAsync(achievement =>
            achievement.UserId == learnerId
            && achievement.Code == AchievementCatalog.FirstCardMastered);
        bool auditExists = await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == AdminAuditActions.AchievementsResyncUser
            && log.Outcome == AdminAuditOutcome.Success
            && log.TargetId == learnerId);

        Assert.Contains(AchievementCatalog.FirstCardMastered, codes);
        Assert.Contains(AchievementCatalog.CardsMastered10, codes);
        Assert.Equal(1, firstCardRows);
        Assert.True(auditExists);
    }

    // Dong bo toan he thong xu ly nhieu user, co ly do/xac nhan/antiforgery va ghi audit tong hop.
    [Fact]
    public async Task ResyncAll_ProcessesUsersInBatchesAndAuditsSummary()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-achievements-all@example.com";
        const string learnerOneEmail = "learner-achievements-all-one@example.com";
        const string learnerTwoEmail = "learner-achievements-all-two@example.com";
        await factory.SeedUserAsync("admin_achievements_all", adminEmail, isAdmin: true);
        await factory.SeedUserAsync("learner_achievements_all_one", learnerOneEmail);
        await factory.SeedUserAsync("learner_achievements_all_two", learnerTwoEmail);
        string learnerOneId = await factory.GetUserIdAsync(learnerOneEmail);
        string learnerTwoId = await factory.GetUserIdAsync(learnerTwoEmail);
        await SeedMasteredCardsAsync(factory, learnerOneId, 1);
        await SeedMasteredCardsAsync(factory, learnerTwoId, 1);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Admin/Achievements",
            "/Admin/Achievements/ResyncAll",
            new Dictionary<string, string>
            {
                ["Reason"] = "Kiem tra dong bo theo lo.",
                ["Confirmed"] = "true",
                ["BatchSize"] = "1"
            });

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int learnerOneCount = await context.UserAchievements.CountAsync(achievement =>
            achievement.UserId == learnerOneId);
        int learnerTwoCount = await context.UserAchievements.CountAsync(achievement =>
            achievement.UserId == learnerTwoId);
        bool summaryAuditExists = await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == AdminAuditActions.AchievementsResyncAll
            && log.Outcome == AdminAuditOutcome.Success
            && log.TargetId == "system"
            && log.MetadataJson != null
            && log.MetadataJson.Contains("processedCount"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(learnerOneCount > 0);
        Assert.True(learnerTwoCount > 0);
        Assert.True(summaryAuditExists);
    }

    // Thieu xac nhan thi service tu choi va khong ghi thanh tich moi.
    [Fact]
    public async Task ResyncUser_WithoutConfirmation_DoesNotChangeAchievements()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-achievements-confirm@example.com";
        const string learnerEmail = "learner-achievements-confirm@example.com";
        await factory.SeedUserAsync("admin_achievements_confirm", adminEmail, isAdmin: true);
        await factory.SeedUserAsync("learner_achievements_confirm", learnerEmail);
        string learnerId = await factory.GetUserIdAsync(learnerEmail);
        await SeedMasteredCardsAsync(factory, learnerId, 1);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Admin/Achievements",
            "/Admin/Achievements/ResyncUser",
            new Dictionary<string, string>
            {
                ["TargetUserId"] = learnerId,
                ["Reason"] = "Kiem tra thieu xac nhan."
            });

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int achievementCount = await context.UserAchievements.CountAsync(achievement =>
            achievement.UserId == learnerId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(0, achievementCount);
    }

    // Tao du lieu mastered card du dieu kien cho cac achievement theo so the da thuoc.
    private static async Task SeedMasteredCardsAsync(
        AdminWebApplicationFactory factory,
        string userId,
        int count)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var set = new FlashcardSet
        {
            Title = $"Achievement seed {userId}",
            UserId = userId,
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();

        for (int index = 0; index < count; index++)
        {
            var card = new Flashcard
            {
                FlashcardSetId = set.Id,
                FrontText = $"front-{index}",
                BackText = $"back-{index}",
                Pronunciation = "/a/",
                PartOfSpeech = "noun",
                ExampleSentence = "Example sentence.",
                ExampleMeaning = "Cau vi du.",
                OrderIndex = index
            };
            context.Flashcards.Add(card);
            await context.SaveChangesAsync();
            context.UserProgresses.Add(new UserProgress
            {
                UserId = userId,
                FlashcardId = card.Id,
                IsLearned = true,
                Status = UserProgressStatus.Mastered,
                LastReviewed = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    // Ghi san mot thanh tich de test so nguoi da nhan trong catalog.
    private static async Task SeedAchievementAsync(
        AdminWebApplicationFactory factory,
        string userId,
        string code)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AchievementCatalog.Definition definition = AchievementCatalog.Find(code)
            ?? throw new InvalidOperationException("Ma thanh tich test khong ton tai.");
        context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            Code = definition.Code,
            Title = definition.Title,
            Description = definition.Description,
            UnlockedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    // Goi form dong bo user voi day du token, ly do va xac nhan.
    private static async Task<HttpResponseMessage> PostUserResyncAsync(
        HttpClient client,
        string learnerId)
    {
        return await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Admin/Achievements",
            "/Admin/Achievements/ResyncUser",
            new Dictionary<string, string>
            {
                ["TargetUserId"] = learnerId,
                ["Reason"] = "Kiem tra khoi phuc thanh tich.",
                ["Confirmed"] = "true"
            });
    }

    // Tao HttpClient khong follow redirect de test dung contract POST-redirect.
    private static HttpClient CreateClient(AdminWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
