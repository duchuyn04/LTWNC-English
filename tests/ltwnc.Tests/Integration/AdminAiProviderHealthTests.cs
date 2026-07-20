using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminAiProviderHealthTests
{
    // Trang Nhà cung cấp AI hiển thị sức khỏe vận hành, thời gian Việt Nam và không lộ dữ liệu nhạy cảm.
    [Fact]
    public async Task Index_RendersProviderHealthSummary()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-ai-provider-health@example.com";
        await factory.SeedUserAsync("admin_ai_provider_health", adminEmail, isAdmin: true);
        await SeedUnstableProviderAsync(factory);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/AiProviders");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Không ổn định", html);
        Assert.Contains("15% lỗi / 5 phút", html);
        Assert.Contains("3 lần kiểm tra lỗi liên tiếp", html);
        Assert.Contains("07:00 19/07/2026 giờ Việt Nam", html);
        Assert.DoesNotContain("system-secret", html);
        Assert.DoesNotContain("user-conversation", html);
    }

    // Tạo nhà cung cấp có 20 log trong cửa sổ 5 phút, 3 lỗi nên vượt ngưỡng 10%.
    private static async Task SeedUnstableProviderAsync(AdminWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime now = factory.Clock.GetUtcNow().UtcDateTime;
        var provider = new AiProvider
        {
            Name = "Provider Health",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-health",
            IsEnabled = true,
            LastCheckSucceeded = false,
            LastCheckedAt = now,
            ConsecutiveFailureCount = 3
        };
        context.AiProviders.Add(provider);
        await context.SaveChangesAsync();

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
                ProviderId = provider.Id,
                ProviderName = provider.Name,
                ModelId = provider.ModelId,
                Operation = "Completion",
                OccurredAtUtc = now.AddMinutes(-1),
                Succeeded = succeeded,
                FailureKind = failureKind,
                LatencyMs = 20,
                FallbackAttempt = 0
            });
        }

        await context.SaveChangesAsync();
    }
}
