using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Services.ContentReports;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class ContentReportTests
{
    // Người học đã đăng nhập gửi được báo cáo cho bộ công khai không thuộc sở hữu của mình.
    [Fact]
    public async Task Learner_SubmitsReportForPublicSet_CreatesPendingReport()
    {
        using var factory = new AdminWebApplicationFactory();
        const string ownerEmail = "report-owner@example.com";
        const string reporterEmail = "reporter-submit@example.com";
        await factory.SeedUserAsync("report_owner", ownerEmail);
        await factory.SeedUserAsync("reporter_submit", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true);

        using HttpClient client = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(client, reporterEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Set/{setId}",
            $"/Set/{setId}/Report",
            new Dictionary<string, string>
            {
                ["Reason"] = "spam",
                ["Description"] = "Bộ này có nội dung quảng cáo."
            });

        ContentReport report = await SingleReportAsync(factory);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(setId, report.FlashcardSetId);
        Assert.Equal(ContentReportStatus.Pending, report.Status);
        Assert.Equal("spam", report.Reason);
    }

    // Form báo cáo không xuất hiện cho chủ sở hữu và service từ chối tự báo cáo.
    [Fact]
    public async Task Owner_CannotReportOwnSet()
    {
        using var factory = new AdminWebApplicationFactory();
        const string ownerEmail = "report-self-owner@example.com";
        const string otherOwnerEmail = "report-self-other-owner@example.com";
        await factory.SeedUserAsync("report_self_owner", ownerEmail);
        await factory.SeedUserAsync("report_self_other_owner", otherOwnerEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true);
        int tokenSourceSetId = await SeedSetAsync(factory, otherOwnerEmail, isPublic: true, title: "Token source");

        using HttpClient client = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(client, ownerEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Set/{tokenSourceSetId}",
            $"/Set/{setId}/Report",
            new Dictionary<string, string>
            {
                ["Reason"] = "spam",
                ["Description"] = "Thử tự báo cáo."
            });
        HttpResponseMessage detailsResponse = await client.GetAsync($"/Set/{setId}");
        string html = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("không thể báo cáo bộ flashcard của chính mình", html);
        Assert.Equal(0, await CountReportsAsync(factory));
    }

    // Báo cáo bộ riêng tư bị từ chối và không tạo dữ liệu moderation.
    [Fact]
    public async Task Learner_CannotReportPrivateSet()
    {
        using var factory = new AdminWebApplicationFactory();
        const string ownerEmail = "report-private-owner@example.com";
        const string reporterEmail = "report-private-reporter@example.com";
        await factory.SeedUserAsync("report_private_owner", ownerEmail);
        await factory.SeedUserAsync("report_private_reporter", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: false);
        int tokenSourceSetId = await SeedSetAsync(factory, ownerEmail, isPublic: true, title: "Token source");

        using HttpClient client = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(client, reporterEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Set/{tokenSourceSetId}",
            $"/Set/{setId}/Report",
            new Dictionary<string, string>
            {
                ["Reason"] = "spam",
                ["Description"] = "Không được phép."
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await CountReportsAsync(factory));
    }

    // Cùng người học không thể có hai báo cáo đang mở cho cùng một bộ.
    [Fact]
    public async Task Learner_DuplicateOpenReport_IsRejected()
    {
        using var factory = new AdminWebApplicationFactory();
        const string ownerEmail = "report-duplicate-owner@example.com";
        const string reporterEmail = "report-duplicate-reporter@example.com";
        await factory.SeedUserAsync("report_duplicate_owner", ownerEmail);
        await factory.SeedUserAsync("report_duplicate_reporter", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true);

        using HttpClient client = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(client, reporterEmail);
        await SubmitReportAsync(client, setId, "spam", "Lần đầu.");
        HttpResponseMessage duplicateResponse =
            await SubmitReportAsync(client, setId, "unsafe", "Lần hai.");
        HttpResponseMessage detailsResponse = await client.GetAsync($"/Set/{setId}");
        string html = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, duplicateResponse.StatusCode);
        Assert.Contains("đã có một báo cáo đang chờ xử lý", html);
        Assert.Equal(1, await CountReportsAsync(factory));
    }

    // Admin xem được hàng đợi, lọc theo lý do và thấy báo cáo quá 24 giờ để cảnh báo dùng sau.
    [Fact]
    public async Task AdminQueue_FiltersReportsAndCountsOverdue()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "report-admin-queue@example.com";
        const string ownerEmail = "report-queue-owner@example.com";
        const string reporterEmail = "report-queue-reporter@example.com";
        await factory.SeedUserAsync("report_admin_queue", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("report_queue_owner", ownerEmail);
        await factory.SeedUserAsync("report_queue_reporter", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true, title: "Bộ cần kiểm tra");
        await SeedReportAsync(factory, setId, reporterEmail, "copyright", createdHoursAgo: 25);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        HttpResponseMessage response = await client.GetAsync(
            "/Admin/ContentReports?reason=copyright&sort=oldest&pageSize=25");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hàng đợi báo cáo nội dung", html);
        Assert.Contains("Bộ cần kiểm tra", html);
        Assert.Contains("báo cáo đang chờ quá 24 giờ", html);
    }

    // Bác bỏ báo cáo ghi trạng thái xử lý và audit trong cùng kết quả nghiệp vụ.
    [Fact]
    public async Task Admin_DismissesReport_UpdatesReportAndWritesAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "report-admin-dismiss@example.com";
        const string ownerEmail = "report-dismiss-owner@example.com";
        const string reporterEmail = "report-dismiss-reporter@example.com";
        await factory.SeedUserAsync("report_admin_dismiss", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("report_dismiss_owner", ownerEmail);
        await factory.SeedUserAsync("report_dismiss_reporter", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true);
        long reportId = await SeedReportAsync(factory, setId, reporterEmail, "incorrect");

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Admin/ContentReports",
            $"/Admin/ContentReports/{reportId}/Dismiss",
            new Dictionary<string, string>
            {
                ["Version"] = "1",
                ["Reason"] = "Nội dung không vi phạm chính sách."
            });

        ContentReport report = await SingleReportAsync(factory);
        bool auditExists = await AuditExistsAsync(factory, reportId, AdminAuditOutcome.Success);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ContentReportStatus.Dismissed, report.Status);
        Assert.Equal("Nội dung không vi phạm chính sách.", report.ResolutionReason);
        Assert.True(auditExists);
    }

    // Form cũ sau khi báo cáo đã đổi phiên bản bị từ chối và tạo audit Denied.
    [Fact]
    public async Task Admin_DismissWithStaleVersion_IsDeniedAndAudited()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "report-admin-stale@example.com";
        const string ownerEmail = "report-stale-owner@example.com";
        const string reporterEmail = "report-stale-reporter@example.com";
        await factory.SeedUserAsync("report_admin_stale", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await factory.SeedUserAsync("report_stale_owner", ownerEmail);
        await factory.SeedUserAsync("report_stale_reporter", reporterEmail);
        int setId = await SeedSetAsync(factory, ownerEmail, isPublic: true);
        long reportId = await SeedReportAsync(factory, setId, reporterEmail, "other");
        await BumpReportVersionAsync(factory, reportId);

        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);
        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Admin/ContentReports",
            $"/Admin/ContentReports/{reportId}/Dismiss",
            new Dictionary<string, string>
            {
                ["Version"] = "1",
                ["Reason"] = "Form cũ."
            });

        ContentReport report = await SingleReportAsync(factory);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ContentReportStatus.Pending, report.Status);
        Assert.True(await AuditExistsAsync(factory, reportId, AdminAuditOutcome.Denied));
    }

    // Gửi form report hợp lệ và trả về response để test có thể kiểm tra redirect.
    private static Task<HttpResponseMessage> SubmitReportAsync(
        HttpClient client,
        int setId,
        string reason,
        string description)
    {
        return AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Set/{setId}",
            $"/Set/{setId}/Report",
            new Dictionary<string, string>
            {
                ["Reason"] = reason,
                ["Description"] = description
            });
    }

    // Tạo bộ flashcard tối thiểu cho các test báo cáo.
    private static async Task<int> SeedSetAsync(
        AdminWebApplicationFactory factory,
        string ownerEmail,
        bool isPublic,
        string title = "Reported set")
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppUser owner = await context.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == ownerEmail.ToUpperInvariant())
            ?? throw new InvalidOperationException("Không tìm thấy owner test.");

        FlashcardSet set = new()
        {
            Title = title,
            UserId = owner.Id,
            IsPublic = isPublic,
            CreatedAt = factory.Clock.GetUtcNow().UtcDateTime,
            UpdatedAt = factory.Clock.GetUtcNow().UtcDateTime
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        return set.Id;
    }

    // Tạo báo cáo trực tiếp trong database để test hàng đợi Admin nhanh và ổn định.
    private static async Task<long> SeedReportAsync(
        AdminWebApplicationFactory factory,
        int setId,
        string reporterEmail,
        string reason,
        int createdHoursAgo = 1)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppUser reporter = await context.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == reporterEmail.ToUpperInvariant())
            ?? throw new InvalidOperationException("Không tìm thấy reporter test.");

        ContentReport report = new()
        {
            FlashcardSetId = setId,
            ReporterUserId = reporter.Id,
            Reason = reason,
            Status = ContentReportStatus.Pending,
            Description = "Seeded report",
            CreatedAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddHours(-createdHoursAgo),
            Version = 1
        };
        context.ContentReports.Add(report);
        await context.SaveChangesAsync();
        return report.Id;
    }

    // Tăng version để giả lập tab cũ hoặc Admin khác vừa xử lý dữ liệu.
    private static async Task BumpReportVersionAsync(AdminWebApplicationFactory factory, long reportId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ContentReport report = await context.ContentReports.SingleAsync(item => item.Id == reportId);
        report.Version++;
        await context.SaveChangesAsync();
    }

    // Đếm số báo cáo hiện có để test các nhánh bị từ chối không ghi dữ liệu thừa.
    private static async Task<int> CountReportsAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.ContentReports.CountAsync();
    }

    // Lấy báo cáo duy nhất và dùng AsNoTracking để đọc đúng trạng thái đã commit.
    private static async Task<ContentReport> SingleReportAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.ContentReports.AsNoTracking().SingleAsync();
    }

    // Kiểm tra audit cho thao tác xử lý báo cáo nội dung.
    private static async Task<bool> AuditExistsAsync(
        AdminWebApplicationFactory factory,
        long reportId,
        string outcome)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string targetId = reportId.ToString();
        return await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == AdminAuditActions.ContentReportsDismiss
            && log.TargetId == targetId
            && log.Outcome == outcome);
    }

    // Tạo client không tự follow redirect để kiểm tra contract HTTP chính xác.
    private static HttpClient CreateClient(AdminWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
