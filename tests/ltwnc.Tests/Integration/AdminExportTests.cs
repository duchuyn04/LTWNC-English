using System.Net;
using System.Text;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminExports;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminExportTests
{
    // Người chưa đăng nhập không được tải export KPI và bị chuyển về trang đăng nhập.
    [Fact]
    public async Task ExportKpis_GuestIsRedirectedToLogin()
    {
        using var factory = new AdminWebApplicationFactory();
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/Admin/Export/Kpis?days=7");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // Người học đã đăng nhập nhưng không có role Admin không được tải export audit.
    [Fact]
    public async Task ExportAuditLogs_LearnerReceivesForbiddenPage()
    {
        using var factory = new AdminWebApplicationFactory();
        const string learnerEmail = "export-learner@example.com";
        await factory.SeedUserAsync("export_learner", learnerEmail);
        using HttpClient client = CreateClient(factory);
        await AdminWebApplicationFactory.SignInAsync(client, learnerEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs/Export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Admin xuất KPI theo bộ lọc ngày hiện tại và thao tác được ghi audit tổng hợp.
    [Fact]
    public async Task ExportKpis_UsesCurrentTimeFilterAndWritesAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "export-kpi-admin@example.com";
        await factory.SeedUserAsync("export_kpi_admin", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await SeedDashboardSessionAsync(factory);
        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Export/Kpis?days=7");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        string csv = DecodeCsv(bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        byte[] preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        Assert.True(bytes.Take(preamble.Length).SequenceEqual(preamble));
        Assert.Contains("\"Metric\",\"Value\",\"Detail\",\"Comparison\",\"Tone\"", csv);
        Assert.Contains("\"Phiên học\",\"1\"", csv);
        await AssertExportAuditAsync(factory, AdminAuditActions.AdminExportsCreate, "kpi", "days=7", "6");
    }

    // Admin xuất audit theo filter hiện tại, CSV chống công thức và audit export không chứa dữ liệu đã xuất.
    [Fact]
    public async Task ExportAuditLogs_AppliesFiltersProtectsCsvFormulasAndWritesAudit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "export-audit-admin@example.com";
        await factory.SeedUserAsync("export_audit_admin", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await SeedAuditRowsAsync(factory);
        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs/Export?action=Users.Lock");
        string csv = DecodeCsv(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"OccurredAtVietnam\",\"Actor\",\"Action\",\"Target\",\"Outcome\"", csv);
        Assert.Contains("Users.Lock", csv);
        Assert.DoesNotContain("Users.Unlock", csv);
        Assert.Contains("\"'=formula-admin@example.com\"", csv);
        Assert.DoesNotContain("dangerous reason", csv);
        Assert.DoesNotContain("ordinary reason", csv);
        await AssertExportAuditAsync(factory, AdminAuditActions.AdminExportsCreate, "audit", "action=Users.Lock", "1");
    }

    // HTTP export audit chỉ lấy 12 tháng gần nhất và không vượt quá số dòng tối đa.
    [Fact]
    public async Task ExportAuditLogs_RespectsRetentionWindowAndRowLimit()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "export-audit-limit-admin@example.com";
        await factory.SeedUserAsync("export_audit_limit_admin", adminEmail, isAdmin: true, twoFactorEnabled: true);
        await SeedManyAuditRowsAsync(factory);
        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs/Export?action=Users.Lock");
        string csv = DecodeCsv(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AdminExportService.AuditExportMaxRows + 1, CountCsvLines(csv));
        Assert.Contains("bulk-999", csv);
        Assert.DoesNotContain("too-old", csv);
        await AssertExportAuditAsync(
            factory,
            AdminAuditActions.AdminExportsCreate,
            "audit",
            $"maxRows={AdminExportService.AuditExportMaxRows}",
            AdminExportService.AuditExportMaxRows.ToString());
    }

    // Các vùng dữ liệu nhạy cảm không có endpoint bulk export cho hồ sơ, học tập, nội dung riêng tư hoặc hội thoại.
    [Theory]
    [InlineData("/Admin/Users/Export")]
    [InlineData("/Admin/Learning/Export")]
    [InlineData("/Admin/EnglishMissions/Export")]
    [InlineData("/Admin/Content/Export")]
    public async Task SensitiveAdminAreas_DoNotExposeBulkExportEndpoints(string path)
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "no-bulk-export-admin@example.com";
        await factory.SeedUserAsync("no_bulk_export_admin", adminEmail, isAdmin: true, twoFactorEnabled: true);
        using HttpClient client = CreateClient(factory);
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Seed một phiên học trong 7 ngày hiện tại để export KPI phản ánh bộ lọc days.
    private static async Task SeedDashboardSessionAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser learner = new()
        {
            UserName = "export_kpi_learner",
            Email = "export-kpi-learner@example.com"
        };
        IdentityResult result = await userManager.CreateAsync(learner, "Testpass1");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        var set = new FlashcardSet
        {
            Title = "Export KPI set",
            UserId = learner.Id,
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();

        context.StudySessions.Add(new StudySession
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.Flashcard,
            StartedAt = factory.Clock.GetUtcNow().UtcDateTime.AddDays(-1),
            CompletedAt = factory.Clock.GetUtcNow().UtcDateTime.AddDays(-1).AddMinutes(5)
        });
        await context.SaveChangesAsync();
    }

    // Seed hai audit cùng thời hạn để kiểm tra filter và bảo vệ công thức trong CSV.
    private static async Task SeedAuditRowsAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        context.AdminAuditLogs.AddRange(
            new AdminAuditLog
            {
                OccurredAtUtc = nowUtc.AddMinutes(-2),
                ActorUserId = "formula-admin",
                ActorDisplay = "=formula-admin@example.com",
                Action = "Users.Lock",
                TargetType = "User",
                TargetId = "42",
                Outcome = AdminAuditOutcome.Success,
                Reason = " +dangerous reason"
            },
            new AdminAuditLog
            {
                OccurredAtUtc = nowUtc.AddMinutes(-1),
                ActorUserId = "other-admin",
                ActorDisplay = "other@example.com",
                Action = "Users.Unlock",
                TargetType = "User",
                TargetId = "43",
                Outcome = AdminAuditOutcome.Success,
                Reason = "ordinary reason"
            });
        await context.SaveChangesAsync();
    }

    // Seed hơn giới hạn export và một dòng quá hạn để HTTP test kiểm tra cap/cutoff trên file CSV thật.
    private static async Task SeedManyAuditRowsAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime nowUtc = factory.Clock.GetUtcNow().UtcDateTime;
        var logs = new List<AdminAuditLog>();
        for (int index = 0; index < AdminExportService.AuditExportMaxRows + 5; index++)
        {
            logs.Add(new AdminAuditLog
            {
                OccurredAtUtc = nowUtc.AddMinutes(-index),
                ActorUserId = $"bulk-{index}",
                ActorDisplay = $"bulk-{index}@example.com",
                Action = "Users.Lock",
                TargetType = "User",
                TargetId = $"bulk-{index}",
                Outcome = AdminAuditOutcome.Success
            });
        }

        logs.Add(new AdminAuditLog
        {
            OccurredAtUtc = nowUtc.AddMonths(-13),
            ActorUserId = "too-old",
            ActorDisplay = "too-old@example.com",
            Action = "Users.Lock",
            TargetType = "User",
            TargetId = "too-old",
            Outcome = AdminAuditOutcome.Success
        });
        context.AdminAuditLogs.AddRange(logs);
        await context.SaveChangesAsync();
    }

    // Kiểm tra audit export chỉ ghi metadata tổng hợp, không ghi dữ liệu CSV đã xuất.
    private static async Task AssertExportAuditAsync(
        AdminWebApplicationFactory factory,
        string action,
        string scope,
        string filterFragment,
        string count)
    {
        using IServiceScope scopeContainer = factory.Services.CreateScope();
        AppDbContext context = scopeContainer.ServiceProvider.GetRequiredService<AppDbContext>();
        AdminAuditLog log = await context.AdminAuditLogs
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstAsync(item => item.Action == action && item.TargetId == scope);

        Assert.Contains($"\"exportType\":\"{scope}\"", log.MetadataJson);
        Assert.Contains($"\"scope\":\"{scope}\"", log.MetadataJson);
        Assert.Contains(filterFragment, log.MetadataJson);
        Assert.Contains($"\"rowCount\":\"{count}\"", log.MetadataJson);
        Assert.Contains($"\"count\":\"{count}\"", log.MetadataJson);
        Assert.DoesNotContain("dangerous reason", log.MetadataJson ?? string.Empty);
        Assert.DoesNotContain("Phiên học", log.MetadataJson ?? string.Empty);
    }

    // Decode UTF-8 có BOM để assert nội dung CSV theo chuỗi.
    private static string DecodeCsv(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    // Đếm số dòng CSV đã sinh, bỏ dòng rỗng cuối do writer luôn kết thúc bằng CRLF.
    private static int CountCsvLines(string csv)
    {
        string[] lines = csv.Split(
            ["\r\n"],
            StringSplitOptions.RemoveEmptyEntries);
        return lines.Length;
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
