using System.Net;
using ltwnc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public class QuizAuthenticationPipelineTests : IClassFixture<WebApplicationFactory<StudyController>>
{
    private readonly WebApplicationFactory<StudyController> _factory;

    public QuizAuthenticationPipelineTests(WebApplicationFactory<StudyController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QuizAnswer_unauthenticated_ajax_request_returns_401_without_login_redirect()
    {
        using HttpClient client = CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "/Study/1/Quiz/1/Answer");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questionId"] = "1",
            ["selectedChoiceIndex"] = "0"
        });

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task QuizStart_unauthenticated_browser_request_redirects_to_login()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/Study/1/Quiz");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location!.AbsolutePath);
        Assert.StartsWith("?ReturnUrl=", response.Headers.Location.Query, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }
}
