using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
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
        Assert.Contains("Chỉ số vận hành hệ thống", html);
        Assert.Contains("aria-pressed=\"true\"", html);
        Assert.Contains("Người dùng hoạt động", html);
        Assert.Contains("Đăng ký mới", html);
        Assert.Contains("Phiên học", html);
        Assert.Contains("Tỷ lệ hoàn thành", html);
        Assert.Contains("Nhiệm vụ tiếng Anh", html);
        Assert.Contains("Tỷ lệ lỗi AI", html);
        Assert.Contains("50%", html);
        Assert.Contains("25%", html);
    }

    private static async Task SeedObservedDashboardDataAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        IdentityUser learner = new()
        {
            UserName = "dashboard_learner",
            Email = "dashboard-learner@example.com"
        };
        IdentityResult createResult = await userManager.CreateAsync(learner, "Testpass1");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

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
