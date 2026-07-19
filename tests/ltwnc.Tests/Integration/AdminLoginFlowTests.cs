using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public sealed class AdminLoginFlowTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminLoginFlowTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    // Admin chỉ cần đăng nhập bằng mật khẩu hợp lệ là được chuyển thẳng vào dashboard.
    [Fact]
    public async Task Login_Admin_RedirectsDirectlyToAdminDashboard()
    {
        const string email = "admin-login-no-two-factor@example.com";
        await _factory.SeedUserAsync("admin_login_no_two_factor", email, isAdmin: true);
        using HttpClient client = CreateClient();

        HttpResponseMessage response =
            await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location?.OriginalString);
    }

    // Tài khoản Admin vẫn vào được dashboard dù không thiết lập 2FA.
    [Fact]
    public async Task AdminDashboard_AdminWithoutTwoFactor_ReturnsDashboard()
    {
        const string email = "admin-dashboard-no-two-factor@example.com";
        await _factory.SeedUserAsync("admin_dashboard_no_two_factor", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Admin");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Trung tâm quản trị", html);
        Assert.DoesNotContain("AdminTwoFactor", html);
    }

    // Learner không đổi luồng: đăng nhập xong vẫn vào trang bộ thẻ học tập.
    [Fact]
    public async Task Login_Learner_RedirectsToLearningSets()
    {
        const string email = "learner-login-landing@example.com";
        await _factory.SeedUserAsync("learner_login_landing", email);
        using HttpClient client = CreateClient();

        HttpResponseMessage response =
            await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Set", response.Headers.Location?.OriginalString);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
