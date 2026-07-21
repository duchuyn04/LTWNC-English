using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public class StaleAuthenticationCookieTests
{
    [Fact]
    public async Task ProfileEdit_DeletedCookieUser_RedirectsToLogin()
    {
        await using var factory = new AdminWebApplicationFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // Đăng nhập thật rồi xóa user khỏi DB để mô phỏng cookie của user đã bị xóa.
        await factory.SeedUserAsync("alice", "alice@example.com");
        await AdminWebApplicationFactory.SignInAsync(client, "alice@example.com");
        await factory.DeleteUserAsync("alice@example.com");

        HttpResponseMessage response = await client.GetAsync("/Account/Profile/Edit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ProfileEdit_RotatedSecurityStamp_RedirectsToLogin()
    {
        await using var factory = new AdminWebApplicationFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SeedUserAsync("alice", "alice@example.com");
        await AdminWebApplicationFactory.SignInAsync(client, "alice@example.com");
        await factory.RotateSecurityStampAsync("alice@example.com");

        HttpResponseMessage response = await client.GetAsync("/Account/Profile/Edit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }
}
