using System.Net;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ltwnc.Tests.Integration;

public class ProfileRouteTests
{
    private static (WebApplicationFactory<Program> Factory, Mock<IProfileService> Service)
        CreateFactory()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetPublicProfileAsync(
                "user1",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicProfileViewModel { Username = "user1" });
        profileService.Setup(service => service.GetPublicProfileAsync(
                "USER1",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicProfileViewModel { Username = "user1" });

        WebApplicationFactory<Program> factory =
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IProfileService>();
                    services.AddSingleton(profileService.Object);
                });
            });

        return (factory, profileService);
    }

    [Fact]
    public async Task CanonicalUsernameRoute_ReturnsProfilePage()
    {
        (WebApplicationFactory<Program> factory, _) = CreateFactory();
        await using (factory)
        using (HttpClient client = CreateClient(factory))
        {
            HttpResponseMessage response = await client.GetAsync("/user1");
            string html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<h1>user1</h1>", html);
        }
    }

    [Theory]
    [InlineData("/USER1", "/user1")]
    [InlineData("/u/user1", "/user1")]
    public async Task AliasOrNonCanonicalCasing_RedirectsPermanently(
        string requestPath,
        string expectedLocation)
    {
        (WebApplicationFactory<Program> factory, _) = CreateFactory();
        await using (factory)
        using (HttpClient client = CreateClient(factory))
        {
            HttpResponseMessage response = await client.GetAsync(requestPath);

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        }
    }

    [Theory]
    [InlineData("/missing-user")]
    [InlineData("/u/account")]
    public async Task MissingOrInvalidUsername_ReturnsCustom404(string requestPath)
    {
        (WebApplicationFactory<Program> factory, _) = CreateFactory();
        await using (factory)
        using (HttpClient client = CreateClient(factory))
        {
            HttpResponseMessage response = await client.GetAsync(requestPath);
            string html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("Bạn vừa rẽ nhầm một hướng.", html);
        }
    }

    [Theory]
    [InlineData("/Set", HttpStatusCode.Redirect)]
    [InlineData("/Achievements", HttpStatusCode.Redirect)]
    [InlineData("/Account/Login", HttpStatusCode.OK)]
    [InlineData("/Study/Settings", HttpStatusCode.Redirect)]
    public async Task ExistingApplicationRoute_IsNotHandledAsProfile(
        string requestPath,
        HttpStatusCode expectedStatus)
    {
        (WebApplicationFactory<Program> factory, Mock<IProfileService> profileService) =
            CreateFactory();
        await using (factory)
        using (HttpClient client = CreateClient(factory))
        {
            HttpResponseMessage response = await client.GetAsync(requestPath);

            Assert.Equal(expectedStatus, response.StatusCode);
            profileService.Verify(service => service.GetPublicProfileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
