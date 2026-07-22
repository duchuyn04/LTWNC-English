using System.Net;
using ltwnc.Services.PublicLibrary;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ltwnc.Tests.Integration;

// Kiểm tra route /Library mở ẩn danh trong khi /Set vẫn chuyển hướng đăng nhập.
public class PublicLibraryRouteTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var result = new PublicLibraryResult(
            "ielts", "recent", 1, 12, 1, 1,
            new PublicLibrarySummary(3, 60, 5),
            [new PublicLibrarySetItem(7, "IELTS", "Mô tả", "minhanh", 20, 4, new DateTime(2026, 7, 20))]);
        var libraryService = new Mock<IPublicLibraryService>();
        libraryService.Setup(service => service.BrowseAsync(
                It.IsAny<PublicLibraryQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPublicLibraryService>();
                services.AddSingleton(libraryService.Object);
            });
        });
    }

    [Fact]
    public async Task LibraryRoute_ReturnsOkForAnonymous()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/Library?q=ielts&sort=recent&page=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PersonalSetRoute_StillRedirectsAnonymousToLogin()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/Set");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task OldPrototypeRoute_ReturnsNotFound()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/prototype/library");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
