using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public sealed class AdminReleaseHardeningTests
{
    private static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    // Kiểm tra các nhãn thao tác AI trực tiếp trên HTML mà route Admin trả về.
    [Fact]
    public async Task AiProviders_IndexUsesVietnameseActionLabels()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-release-ai-labels@example.com";
        using HttpClient client = await CreateSignedInAdminClientAsync(
            factory,
            "admin_release_ai_labels",
            adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/AiProviders");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Thêm nhà cung cấp", html);
        Assert.Contains("Chưa có nhà cung cấp", html);
        Assert.DoesNotContain(">Thêm provider<", html);
        Assert.DoesNotContain(">Test<", html);
        Assert.DoesNotContain(">Models<", html);
        Assert.DoesNotContain("Chưa có provider", html);
    }

    // Biểu đồ server-rendered phải có nhãn cho công nghệ hỗ trợ mà không cần JavaScript.
    [Fact]
    public async Task Dashboard_RendersAccessibleServerSideChart()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-release-live-status@example.com";
        using HttpClient client = await CreateSignedInAdminClientAsync(
            factory,
            "admin_release_live_status",
            adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Phiên học theo ngày", html);
        Assert.Contains("phiên hoàn thành", html);
        Assert.Contains("Phiên bỏ dở", html);
        Assert.DoesNotContain("admin-dashboard.js", html);
    }

    // Các mốc nhanh phải bao gồm hôm nay và mặc định chọn 7 ngày.
    [Fact]
    public async Task Dashboard_RendersQuickDateRangesAndMarksSevenDaysAsDefault()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-release-quick-ranges@example.com";
        using HttpClient client = await CreateSignedInAdminClientAsync(
            factory,
            "admin_release_quick_ranges",
            adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/Admin?from=2026-07-06&to=2026-07-19\">14 ngày</a>", html);
        Assert.Contains("href=\"/Admin?from=2026-06-20&to=2026-07-19\">30 ngày</a>", html);
        Assert.Matches(
            "class=\"active\"[^>]*aria-current=\"page\"[^>]*href=\"/Admin\\?from=2026-07-13&to=2026-07-19\">7 ngày</a>",
            html);
    }

    // Prototype chỉ là công cụ thiết kế nội bộ nên môi trường test/production không được trả màn hình prototype.
    [Fact]
    public async Task PrototypeDashboard_IsNotAvailableOutsideDevelopment()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-release-prototype@example.com";
        using HttpClient client = await CreateSignedInAdminClientAsync(
            factory,
            "admin_release_prototype",
            adminEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Prototype/Dashboard");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // CSS Admin phải không ép tràn ngang ở màn hình hẹp, focus rõ và tôn trọng tùy chọn giảm chuyển động.
    [Fact]
    public void AdminStyles_DefineMobileFloorFocusRingAndReducedMotion()
    {
        string adminCssDirectory = Path.Combine(RepositoryRoot, "wwwroot", "css", "admin");
        string css = string.Concat(
            Directory.GetFiles(adminCssDirectory, "*.css").Select(File.ReadAllText));

        Assert.Contains("min-width: 0", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("outline:", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("animation-duration", css);
    }

    // Tạo tài khoản Admin và đăng nhập để mỗi test chỉ tập trung vào hành vi cần kiểm tra.
    private static async Task<HttpClient> CreateSignedInAdminClientAsync(
        AdminWebApplicationFactory factory,
        string userName,
        string email)
    {
        await factory.SeedUserAsync(userName, email, isAdmin: true);
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SignInVerifiedAdminAsync(client, email);

        return client;
    }
}
