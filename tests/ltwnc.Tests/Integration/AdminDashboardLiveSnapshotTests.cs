using System.Net;
using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminDashboardLiveSnapshotTests
{
    // Chỉ Admin mới được lấy ảnh chụp vận hành và phản hồi không được lưu vào bộ nhớ đệm công khai.
    [Fact]
    public async Task Snapshot_RequiresAdminAndDisablesPublicCaching()
    {
        using var factory = new AdminWebApplicationFactory();
        await factory.SeedUserAsync("normal_dashboard", "normal-dashboard@example.com");
        await factory.SeedUserAsync("admin_dashboard_live", "admin-dashboard-live@example.com", isAdmin: true);

        using HttpClient anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        HttpResponseMessage anonymousResponse = await anonymousClient.GetAsync("/Admin/Snapshot");

        using HttpClient normalClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await AdminWebApplicationFactory.SignInAsync(normalClient, "normal-dashboard@example.com");
        HttpResponseMessage normalResponse = await normalClient.GetAsync("/Admin/Snapshot");

        using HttpClient adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SignInVerifiedAdminAsync(adminClient, "admin-dashboard-live@example.com");
        HttpResponseMessage adminResponse = await adminClient.GetAsync("/Admin/Snapshot");

        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, normalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Contains("no-store", adminResponse.Headers.CacheControl?.ToString());
    }

    // Contract JSON trả số liệu tổng hợp và cảnh báo, không trả dữ liệu người dùng hay hội thoại nhạy cảm.
    [Fact]
    public async Task Snapshot_ReturnsStableSafeContractWithAlerts()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-dashboard-contract@example.com";
        await factory.SeedUserAsync("admin_dashboard_contract", adminEmail, isAdmin: true);
        await SeedAlertDataAsync(factory);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Snapshot?days=7");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement alerts = root.GetProperty("alerts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, root.GetProperty("days").GetInt32());
        Assert.True(root.TryGetProperty("period", out _));
        Assert.True(root.TryGetProperty("kpis", out _));
        Assert.Equal(1, root.GetProperty("contentReports").GetProperty("pendingCount").GetInt32());
        Assert.Equal(15m, root.GetProperty("aiStatus").GetProperty("errorRatePercent").GetDecimal());
        Assert.Contains(alerts.EnumerateArray(), alert => alert.GetProperty("code").GetString() == "ai-primary-unstable");
        Assert.Contains(alerts.EnumerateArray(), alert => alert.GetProperty("code").GetString() == "ai-error-rate");
        Assert.Contains(alerts.EnumerateArray(), alert => alert.GetProperty("code").GetString() == "content-report-overdue");
        Assert.Contains(alerts.EnumerateArray(), alert => alert.GetProperty("code").GetString() == "achievement-resync-failed");

        // Chỉ ghép các trường hiển thị để tên thuộc tính kỹ thuật trong JSON không làm sai kết quả kiểm tra.
        List<string> visibleAlertParts = new();
        foreach (JsonElement alert in alerts.EnumerateArray())
        {
            visibleAlertParts.Add(alert.GetProperty("title").GetString() ?? string.Empty);
            visibleAlertParts.Add(alert.GetProperty("detail").GetString() ?? string.Empty);
            visibleAlertParts.Add(alert.GetProperty("actionText").GetString() ?? string.Empty);
        }

        string visibleAlertText = string.Join(" ", visibleAlertParts);
        Assert.Contains("Kiểm tra nhà cung cấp", visibleAlertText);
        Assert.DoesNotContain("health check", visibleAlertText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", visibleAlertText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-user-id", json);
        Assert.DoesNotContain("noi dung rieng tu", json);
        Assert.DoesNotContain("system-secret", json);
        Assert.DoesNotContain("user-conversation", json);
    }

    // Tạo đủ dữ liệu tổng hợp để dashboard sinh các cảnh báo vận hành cần thiết.
    private static async Task SeedAlertDataAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime now = factory.Clock.GetUtcNow().UtcDateTime;
        var set = new FlashcardSet
        {
            Title = "Dashboard alert set",
            UserId = "owner-alert",
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();

        context.AiProviders.Add(new AiProvider
        {
            Name = "Primary Gateway",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-live",
            IsEnabled = true,
            IsPrimary = true,
            LastCheckSucceeded = false,
            ConsecutiveFailureCount = 3
        });
        for (int index = 0; index < 20; index++)
        {
            bool succeeded = index >= 3;
            string? failureKind = null;
            if (!succeeded)
            {
                failureKind = "Timeout";
            }

            context.AiOperationLogs.Add(new AiOperationLog
            {
                OccurredAtUtc = now.AddMinutes(-1),
                Operation = "Completion",
                Succeeded = succeeded,
                FailureKind = failureKind,
                LatencyMs = 20
            });
        }

        context.ContentReports.Add(new ContentReport
        {
            FlashcardSetId = set.Id,
            ReporterUserId = "private-user-id",
            Reason = "spam",
            Description = "noi dung rieng tu",
            Status = ContentReportStatus.Pending,
            CreatedAtUtc = now.AddHours(-25)
        });
        context.AdminAuditLogs.Add(new AdminAuditLog
        {
            OccurredAtUtc = now.AddMinutes(-1),
            ActorUserId = "admin-id",
            ActorDisplay = "Admin",
            Action = AdminAuditActions.AchievementsResyncUser,
            Outcome = AdminAuditOutcome.Failure,
            TargetType = "AppUser",
            TargetId = "private-user-id"
        });
        await context.SaveChangesAsync();
    }
}
