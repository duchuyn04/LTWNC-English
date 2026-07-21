using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminDashboardKpiTests
{
    [Fact]
    public async Task AdminDashboard_RendersKpisWithSelectedRangeAndObservedData()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "dashboard-admin@example.com";
        await factory.SeedUserAsync(
            "dashboard_admin",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        await SeedObservedDashboardDataAsync(factory);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin?days=7");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Tổng quan</h1>", html);
        Assert.Contains("aria-pressed=\"true\"", html);
        Assert.Contains("Đang hoạt động", html);
        Assert.Contains("Mới đăng ký", html);
        Assert.Contains("Phiên học", html);
        Assert.Contains("Hoàn thành", html);
        Assert.Contains("Nhiệm vụ", html);
        Assert.Contains("Lỗi AI", html);
        Assert.Contains("href=\"/Admin/Users\">", html);
        Assert.Contains("Xem người dùng", html);
        Assert.Contains("href=\"/Admin/Learning\">", html);
        Assert.Contains("Xem phiên học", html);
        Assert.Contains("href=\"/Admin/EnglishMissions\">", html);
        Assert.Contains("Xem nhiệm vụ", html);
        Assert.Contains("href=\"/Admin/AiProviders\">", html);
        Assert.Contains("Kiểm tra AI", html);
        Assert.Contains("<details", html);
        Assert.Contains("Cách tính chỉ số", html);
        Assert.DoesNotContain("TRUNG TÂM VẬN HÀNH", html);
        Assert.DoesNotContain("Chỉ số vận hành hệ thống", html);
        Assert.DoesNotContain("Quy tắc tính", html);
        Assert.DoesNotContain("hồ sơ tạo trong khoảng", html);
        Assert.Contains("50%", html);
        Assert.Contains("25%", html);
    }

    private static async Task SeedObservedDashboardDataAsync(AdminWebApplicationFactory factory)
    {
        const string learnerEmail = "dashboard-learner@example.com";
        await factory.SeedUserAsync("dashboard_learner", learnerEmail);

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppUser learner = await context.AppUsers
            .SingleAsync(item => item.NormalizedEmail == learnerEmail.ToUpperInvariant());

        DateTime now = factory.Clock.GetUtcNow().UtcDateTime;
        context.UserProfiles.Add(new UserProfile
        {
            UserId = learner.Id,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        });
        var set = new FlashcardSet
        {
            Title = "Dashboard set",
            UserId = learner.Id,
            IsPublic = true
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();

        StudySession completed = new()
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.Flashcard,
            StartedAt = now.AddHours(-3),
            CompletedAt = now.AddHours(-2)
        };
        StudySession abandoned = new()
        {
            UserId = learner.Id,
            FlashcardSetId = set.Id,
            Mode = StudyMode.Flashcard,
            StartedAt = now.AddHours(-2),
            CompletedAt = null
        };
        context.StudySessions.AddRange(completed, abandoned);
        await context.SaveChangesAsync();

        for (int index = 0; index < 20; index++)
        {
            bool isFailure = index < 5;
            string? failureKind = null;
            if (isFailure)
            {
                failureKind = "Timeout";
            }

            context.AiOperationLogs.Add(new AiOperationLog
            {
                OccurredAtUtc = now.AddMinutes(-index),
                Operation = "Completion",
                Succeeded = !isFailure,
                FailureKind = failureKind,
                LatencyMs = 20
            });
        }

        await context.SaveChangesAsync();
    }
}
