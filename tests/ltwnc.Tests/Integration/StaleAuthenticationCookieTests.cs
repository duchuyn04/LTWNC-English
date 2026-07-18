using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;

namespace ltwnc.Tests.Integration;

public class StaleAuthenticationCookieTests
{
    [Fact]
    public async Task ProfileEdit_DeletedCookieUser_RedirectsToLogin()
    {
        await using WebApplicationFactory<Program> factory =
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureServices(services =>
                {
                    var userStore = new Mock<IUserStore<IdentityUser>>();
                    userStore.Setup(store => store.FindByIdAsync(
                            It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((IdentityUser?)null);
                    services.RemoveAll<IUserStore<IdentityUser>>();
                    services.AddSingleton(userStore.Object);
                });
            });
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        CookieAuthenticationOptions cookieOptions = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "deleted-user"),
                new Claim(ClaimTypes.Name, "deleted-user")
            ],
            IdentityConstants.ApplicationScheme));
        string cookie = cookieOptions.TicketDataFormat.Protect(
            new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme));
        client.DefaultRequestHeaders.Add("Cookie", $"{cookieOptions.Cookie.Name}={cookie}");

        HttpResponseMessage response = await client.GetAsync("/Account/Profile/Edit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }
}
