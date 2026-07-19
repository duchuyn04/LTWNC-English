using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Services.Study;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminContentModerationTests
{
    // Admin cách ly từ báo cáo đang chờ: bộ biến mất khỏi public surfaces, report đóng và audit được ghi.
    [Fact]
    public async Task Admin_QuarantinesFromReport_HidesSetFromPublicSurfaces()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "content-admin-report@example.com";
        const string ownerEmail = "content-owner-report@example.com";
        const string reporterEmail = "content-reporter-report@example.com";
        await factory.SeedUserAsync("content_admin_report", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("content_owner_report", ownerEmail);
        await factory.SeedUserAsync("content_reporter_report", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, true, "Unique quarantine report set");
        int tokenSetId = await SeedSetAsync(factory, ownerEmail, true, "Token source for quarantine");
        long reportId = await SeedReportAsync(factory, setId, reporterEmail);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage quarantineResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            adminClient,
            "/Admin/ContentReports",
            $"/Admin/ContentReports/{reportId}/Quarantine",
            new Dictionary<string, string>
            {
                ["ReportVersion"] = "1",
                ["FlashcardSetVersion"] = "1",
                ["PublicReason"] = "Nội dung vi phạm chính sách công khai.",
                ["InternalNote"] = "Ghi chú nội bộ không được lộ.",
                ["Evidence"] = "Bằng chứng nội bộ không được lộ.",
                ["Confirmed"] = "true"
            });

        FlashcardSet set = await FindSetAsync(factory, setId);
        ContentReport report = await FindReportAsync(factory, reportId);
        Assert.Equal(HttpStatusCode.Redirect, quarantineResponse.StatusCode);
        Assert.Equal(FlashcardSetModerationStatus.Quarantined, set.ModerationStatus);
        Assert.Equal("Nội dung vi phạm chính sách công khai.", set.ModerationPublicReason);
        Assert.Equal(ContentReportStatus.Quarantined, report.Status);
        Assert.True(await AuditExistsAsync(factory, AdminAuditActions.ContentReportsQuarantine, AdminAuditOutcome.Success, setId));

        using HttpClient guestClient = CreateClient(factory);
        HttpResponseMessage searchResponse = await guestClient.GetAsync("/?q=Unique%20quarantine%20report");
        string searchHtml = WebUtility.HtmlDecode(await searchResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Unique quarantine report set", searchHtml);

        using HttpClient reporterClient = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(reporterClient, reporterEmail);
        HttpResponseMessage detailsResponse = await reporterClient.GetAsync($"/Set/{setId}");
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);

        HttpResponseMessage copyResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            reporterClient,
            $"/Set/{tokenSetId}",
            $"/Set/{setId}/Copy",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.NotFound, copyResponse.StatusCode);

        HttpResponseMessage reportResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            reporterClient,
            $"/Set/{tokenSetId}",
            $"/Set/{setId}/Report",
            new Dictionary<string, string>
            {
                ["Reason"] = "spam",
                ["Description"] = "Thử báo cáo bộ đã cách ly."
            });
        Assert.Equal(HttpStatusCode.NotFound, reportResponse.StatusCode);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await StartPublicStudyAsync(factory, reporterEmail, setId));
    }

    // Tác giả vẫn xem và sửa được bộ bị cách ly, nhưng chỉ thấy lý do công khai.
    [Fact]
    public async Task Owner_CanViewQuarantinedSet_WithoutSeeingInternalModerationData()
    {
        using var factory = new AdminWebApplicationFactory();
        const string ownerEmail = "content-owner-private-reason@example.com";
        await factory.SeedUserAsync("content_owner_private_reason", ownerEmail);
        int setId = await SeedSetAsync(
            factory,
            ownerEmail,
            true,
            "Owner quarantine visible",
            FlashcardSetModerationStatus.Quarantined,
            "Lý do công khai cho tác giả.",
            "Ghi chú nội bộ tuyệt mật.",
            "Bằng chứng nội bộ tuyệt mật.");

        using HttpClient ownerClient = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(ownerClient, ownerEmail);
        HttpResponseMessage detailsResponse = await ownerClient.GetAsync($"/Set/{setId}");
        string detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        HttpResponseMessage editorResponse = await ownerClient.GetAsync($"/flashcardset/editor/{setId}");
        string editorHtml = WebUtility.HtmlDecode(await editorResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Lý do công khai cho tác giả.", detailsHtml);
        Assert.DoesNotContain("Ghi chú nội bộ tuyệt mật.", detailsHtml);
        Assert.DoesNotContain("Bằng chứng nội bộ tuyệt mật.", detailsHtml);
        Assert.Equal(HttpStatusCode.OK, editorResponse.StatusCode);
        Assert.Contains("Lý do công khai cho tác giả.", editorHtml);
        Assert.Contains("id=\"set-is-public\" checked disabled", editorHtml);
        Assert.DoesNotContain("Ghi chú nội bộ tuyệt mật.", editorHtml);
    }

    // Admin mở chi tiết bộ riêng tư phải nhập lý do; có lý do thì audit rồi mới trả nội dung thẻ.
    [Fact]
    public async Task Admin_PrivateSetDetails_RequiresReasonAndWritesAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "content-admin-private-details@example.com";
        const string ownerEmail = "content-owner-private-details@example.com";
        await factory.SeedUserAsync("content_admin_private_details", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("content_owner_private_details", ownerEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, false, "Private moderation set");

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage gateResponse = await adminClient.GetAsync($"/Admin/Content/{setId}");
        string gateHtml = WebUtility.HtmlDecode(await gateResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, gateResponse.StatusCode);
        Assert.Contains("Xem nội dung bộ riêng tư", gateHtml);
        Assert.DoesNotContain("front-private", gateHtml);
        Assert.False(await AuditExistsAsync(factory, AdminAuditActions.ContentSetsViewPrivateDetails, AdminAuditOutcome.Success, setId));

        HttpResponseMessage detailsResponse = await adminClient.GetAsync(
            $"/Admin/Content/{setId}?reason=H%E1%BB%97%20tr%E1%BB%A3%20t%C3%A1c%20gi%E1%BA%A3");
        string detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("front-private", detailsHtml);
        Assert.True(await AuditExistsAsync(factory, AdminAuditActions.ContentSetsViewPrivateDetails, AdminAuditOutcome.Success, setId));
    }

    // Admin khôi phục bộ bị cách ly để bộ công khai xuất hiện lại, không cần tác giả tự publish.
    [Fact]
    public async Task Admin_RestoresQuarantinedSet_ReexposesPublicSet()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "content-admin-restore@example.com";
        const string ownerEmail = "content-owner-restore@example.com";
        await factory.SeedUserAsync("content_admin_restore", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("content_owner_restore", ownerEmail);
        int setId = await SeedSetAsync(
            factory,
            ownerEmail,
            true,
            "Restored public moderation set",
            FlashcardSetModerationStatus.Quarantined,
            "Đang bị cách ly.");

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage restoreResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            adminClient,
            "/Admin/Content",
            $"/Admin/Content/{setId}/Restore",
            new Dictionary<string, string>
            {
                ["Version"] = "1",
                ["Reason"] = "Đã xác minh và cho phép khôi phục.",
                ["Confirmed"] = "true"
            });

        FlashcardSet set = await FindSetAsync(factory, setId);
        Assert.Equal(HttpStatusCode.Redirect, restoreResponse.StatusCode);
        Assert.Equal(FlashcardSetModerationStatus.Active, set.ModerationStatus);
        Assert.Null(set.ModerationPublicReason);
        Assert.True(await AuditExistsAsync(factory, AdminAuditActions.ContentSetsRestore, AdminAuditOutcome.Success, setId));

        using HttpClient guestClient = CreateClient(factory);
        HttpResponseMessage searchResponse = await guestClient.GetAsync("/?q=Restored%20public%20moderation");
        string searchHtml = WebUtility.HtmlDecode(await searchResponse.Content.ReadAsStringAsync());
        Assert.Contains("Restored public moderation set", searchHtml);
    }

    // Form cũ với version sai bị từ chối và không đổi trạng thái bộ.
    [Fact]
    public async Task Admin_QuarantineWithStaleSetVersion_IsDeniedAndAudited()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "content-admin-stale@example.com";
        const string ownerEmail = "content-owner-stale@example.com";
        await factory.SeedUserAsync("content_admin_stale", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("content_owner_stale", ownerEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, true, "Stale moderation set");
        await BumpSetVersionAsync(factory, setId);

        using HttpClient adminClient = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            adminClient,
            "/Admin/Content",
            $"/Admin/Content/{setId}/Quarantine",
            new Dictionary<string, string>
            {
                ["Version"] = "1",
                ["PublicReason"] = "Form cũ.",
                ["Confirmed"] = "true"
            });

        FlashcardSet set = await FindSetAsync(factory, setId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(FlashcardSetModerationStatus.Active, set.ModerationStatus);
        Assert.True(await AuditExistsAsync(factory, AdminAuditActions.ContentSetsQuarantine, AdminAuditOutcome.Denied, setId));
    }

    // Tạo bộ flashcard có một thẻ để các trang detail và study có dữ liệu thực tế.
    private static async Task<int> SeedSetAsync(
        AdminWebApplicationFactory factory,
        string ownerEmail,
        bool isPublic,
        string title,
        string moderationStatus = FlashcardSetModerationStatus.Active,
        string? publicReason = null,
        string? internalNote = null,
        string? evidence = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser owner = await userManager.FindByEmailAsync(ownerEmail)
            ?? throw new InvalidOperationException("Không tìm thấy owner test.");

        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        DateTime? moderatedAtUtc = null;
        if (publicReason != null)
        {
            moderatedAtUtc = nowUtc;
        }

        var set = new FlashcardSet
        {
            Title = title,
            Description = "Mô tả test",
            UserId = owner.Id,
            IsPublic = isPublic,
            ModerationStatus = moderationStatus,
            ModerationPublicReason = publicReason,
            ModerationInternalNote = internalNote,
            ModerationEvidence = evidence,
            ModeratedAtUtc = moderatedAtUtc,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            ModerationVersion = 1
        };
        set.Flashcards.Add(new Flashcard
        {
            FrontText = "front-private",
            BackText = "back-private",
            PartOfSpeech = "noun",
            Pronunciation = "/test/",
            ExampleSentence = "A test sentence.",
            ExampleMeaning = "Một câu test.",
            OrderIndex = 0
        });

        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        return set.Id;
    }

    // Tạo báo cáo đang chờ để kiểm tra nhánh cách ly từ hàng đợi report.
    private static async Task<long> SeedReportAsync(
        AdminWebApplicationFactory factory,
        int setId,
        string reporterEmail)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser reporter = await userManager.FindByEmailAsync(reporterEmail)
            ?? throw new InvalidOperationException("Không tìm thấy reporter test.");

        var report = new ContentReport
        {
            FlashcardSetId = setId,
            ReporterUserId = reporter.Id,
            Reason = "spam",
            Description = "Seeded moderation report",
            Status = ContentReportStatus.Pending,
            CreatedAtUtc = factory.Clock.GetUtcNow().UtcDateTime,
            Version = 1
        };
        context.ContentReports.Add(report);
        await context.SaveChangesAsync();
        return report.Id;
    }

    // Tăng version bộ để giả lập form cũ.
    private static async Task BumpSetVersionAsync(AdminWebApplicationFactory factory, int setId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        FlashcardSet set = await context.FlashcardSets.SingleAsync(item => item.Id == setId);
        set.ModerationVersion++;
        await context.SaveChangesAsync();
    }

    // Lấy bộ flashcard không tracking để đọc trạng thái đã commit.
    private static async Task<FlashcardSet> FindSetAsync(
        AdminWebApplicationFactory factory,
        int setId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.FlashcardSets.AsNoTracking().SingleAsync(item => item.Id == setId);
    }

    // Lấy báo cáo không tracking để đọc trạng thái đã commit.
    private static async Task<ContentReport> FindReportAsync(
        AdminWebApplicationFactory factory,
        long reportId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.ContentReports.AsNoTracking().SingleAsync(item => item.Id == reportId);
    }

    // Gọi trực tiếp service học để chứng minh endpoint học công khai không vượt qua lớp nghiệp vụ.
    private static async Task StartPublicStudyAsync(
        AdminWebApplicationFactory factory,
        string learnerEmail,
        int setId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser learner = await userManager.FindByEmailAsync(learnerEmail)
            ?? throw new InvalidOperationException("Không tìm thấy learner test.");
        IStudyService studyService = scope.ServiceProvider.GetRequiredService<IStudyService>();

        await studyService.StartSessionAsync(learner.Id, setId, StudyMode.Flashcard);
    }

    // Kiểm tra audit theo action/outcome/target bộ flashcard.
    private static async Task<bool> AuditExistsAsync(
        AdminWebApplicationFactory factory,
        string action,
        string outcome,
        int setId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string targetId = setId.ToString();
        return await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == action
            && log.Outcome == outcome
            && log.TargetId == targetId);
    }

    // Tạo client không tự follow redirect để test contract HTTP chính xác.
    private static HttpClient CreateClient(AdminWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
