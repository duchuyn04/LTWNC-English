using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public sealed class AdminAreaAccessTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminAreaAccessTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminDashboard_GuestIsRedirectedToLogin()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2FAdmin", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task AdminDashboard_LearnerReceivesForbiddenPage()
    {
        const string email = "learner-admin-area@example.com";
        await _factory.SeedUserAsync("learner_admin_area", email);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Admin");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Bạn không có quyền truy cập khu vực này.", html);
    }

    [Fact]
    public async Task AdminDashboard_AdminReceivesProductionCommandCenter()
    {
        const string email = "admin-area@example.com";
        await _factory.SeedUserAsync("admin_area", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Admin");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Trung tâm quản trị", html);
        Assert.Contains("Tổng quan", html);
        Assert.Contains("Người dùng", html);
        Assert.Contains("Nội dung", html);
        Assert.Contains("Phiên học", html);
        Assert.Contains("Nhiệm vụ tiếng Anh", html);
        Assert.Contains("Thành tích", html);
        Assert.Contains("Nhà cung cấp AI", html);
        Assert.Contains("Bản ghi kiểm toán quản trị", html);
        Assert.Contains("href=\"/Set\" target=\"_blank\"", html);
        Assert.Contains("/css/site.css", html);
        Assert.Contains("/css/admin-dashboard.css", html);
        Assert.DoesNotContain("PROTOTYPE", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prototype-switcher", html);
        Assert.DoesNotContain("12.480", html);
    }

    [Theory]
    [InlineData("/Set", "/Account/Login?ReturnUrl=%2FSet")]
    [InlineData("/Study/Settings", "/Set/0")]
    public async Task AdminAreaRegistration_GuestLearningRouteKeepsExistingRedirect(
        string requestPath,
        string expectedLocation)
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(requestPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith(
            expectedLocation,
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminAreaRegistration_LearnerStillReceivesSetPage()
    {
        const string email = "learner-route-regression@example.com";
        await _factory.SeedUserAsync("learner_route_regression", email);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Set");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bộ thẻ của tôi", html);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
