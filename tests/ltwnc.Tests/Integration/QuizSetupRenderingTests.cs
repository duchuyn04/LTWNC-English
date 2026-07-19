using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ltwnc.Controllers;
using ltwnc.Services.Study;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ltwnc.Tests.Integration;

public sealed class QuizSetupRenderingTests : IClassFixture<WebApplicationFactory<StudyController>>
{
    private readonly WebApplicationFactory<StudyController> _factory;

    public QuizSetupRenderingTests(WebApplicationFactory<StudyController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Quiz_setup_renders_named_post_action_and_antiforgery_field()
    {
        Mock<IQuizService> quizService = new();
        quizService.Setup(service => service.GetSetupAsync(7, "user-1"))
            .ReturnsAsync(new QuizSetupState
            {
                SetId = 7,
                SetTitle = "Core English"
            });
        using WebApplicationFactory<StudyController> factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQuizService>();
                services.AddSingleton(quizService.Object);
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.AuthenticationScheme,
                        _ => { });
            }));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync("/Study/7/Quiz");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        System.Text.RegularExpressions.Match form = Regex.Match(
            html,
            "<form(?=[^>]*action=\"/Study/7/Quiz/Start\")(?=[^>]*method=\"post\")[^>]*>[\\s\\S]*?</form>",
            RegexOptions.IgnoreCase);
        Assert.True(form.Success, html);
        Assert.Contains("name=\"__RequestVerificationToken\"", form.Value);
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string AuthenticationScheme = "QuizSetupTest";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            {
                new(ClaimTypes.NameIdentifier, "user-1"),
                new(ClaimTypes.Name, "Quiz User")
            };
            ClaimsPrincipal principal = new(new ClaimsIdentity(claims, AuthenticationScheme));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, AuthenticationScheme)));
        }
    }
}
