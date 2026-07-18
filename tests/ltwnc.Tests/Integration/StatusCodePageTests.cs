using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public class StatusCodePageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StatusCodePageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task UnknownRoute_ReturnsCustom404With404Status()
    {
        HttpResponseMessage response = await _client.GetAsync("/khong-ton-tai");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Bạn vừa rẽ nhầm một hướng.", html);
        Assert.Contains("Về trang chủ", html);
    }

    [Fact]
    public async Task LoginRoute_RemainsSuccessful()
    {
        HttpResponseMessage response = await _client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
