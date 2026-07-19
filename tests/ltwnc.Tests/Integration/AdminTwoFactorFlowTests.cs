using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public sealed class AdminTwoFactorFlowTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminTwoFactorFlowTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Login_AdminWithoutTwoFactor_RedirectsToSetup()
    {
        const string email = "admin-without-two-factor@example.com";
        await _factory.SeedUserAsync("admin_without_two_factor", email, isAdmin: true);
        using HttpClient client = CreateClient();

        HttpResponseMessage response =
            await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Account/AdminTwoFactor/Setup",
            response.Headers.Location?.OriginalString);
    }

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

    [Fact]
    public async Task Setup_AdminWithoutTwoFactor_ReceivesVietnameseAuthenticatorInstructions()
    {
        const string email = "admin-two-factor-setup@example.com";
        await _factory.SeedUserAsync("admin_two_factor_setup", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage response =
            await client.GetAsync("/Account/AdminTwoFactor/Setup");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Thiết lập xác thực hai bước", html);
        Assert.Contains("Khóa thiết lập thủ công", html);
        Assert.Contains("otpauth://totp/", html);
    }

    [Fact]
    public async Task Verify_AdminWhoHasNotEnabledTwoFactor_CannotBypassSetup()
    {
        const string email = "admin-two-factor-bypass@example.com";
        await _factory.SeedUserAsync("admin_two_factor_bypass", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);
        await client.GetAsync("/Account/AdminTwoFactor/Setup");
        string code = await _factory.GenerateAuthenticatorCodeAsync(email);

        HttpResponseMessage verifyResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Setup",
            "/Account/AdminTwoFactor/Verify",
            new Dictionary<string, string>
            {
                ["Code"] = code,
                ["ReturnUrl"] = "/Admin"
            });
        HttpResponseMessage adminResponse = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor/Setup",
            verifyResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor?returnUrl=",
            adminResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Setup_ValidAuthenticatorCode_EnablesAdminAreaAndShowsRecoveryCodes()
    {
        const string email = "admin-enable-two-factor@example.com";
        await _factory.SeedUserAsync("admin_enable_two_factor", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);
        await client.GetAsync("/Account/AdminTwoFactor/Setup");
        string code = await _factory.GenerateAuthenticatorCodeAsync(email);

        HttpResponseMessage setupResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Setup",
            "/Account/AdminTwoFactor/Setup",
            new Dictionary<string, string> { ["Code"] = code });
        string setupHtml = WebUtility.HtmlDecode(
            await setupResponse.Content.ReadAsStringAsync());
        HttpResponseMessage adminResponse = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        Assert.Contains("Mã khôi phục", setupHtml);
        Assert.Equal(10, CountOccurrences(setupHtml, "data-recovery-code"));
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Login_AdminWithTwoFactor_RedirectsToVerification()
    {
        const string email = "admin-with-two-factor@example.com";
        await _factory.SeedUserAsync(
            "admin_with_two_factor",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();

        HttpResponseMessage response =
            await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Account/AdminTwoFactor/Verify",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PendingTwoFactorLogin_LoginPageContinuesVerification()
    {
        const string email = "admin-pending-two-factor@example.com";
        await _factory.SeedUserAsync(
            "admin_pending_two_factor",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Account/AdminTwoFactor/Verify",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Verify_ValidAuthenticatorCode_StartsVerifiedAdminSession()
    {
        const string email = "admin-verify-two-factor@example.com";
        await _factory.SeedUserAsync(
            "admin_verify_two_factor",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);
        string code = await _factory.GenerateAuthenticatorCodeAsync(email);

        HttpResponseMessage verifyResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Verify",
            "/Account/AdminTwoFactor/Verify",
            new Dictionary<string, string> { ["Code"] = code });
        HttpResponseMessage adminResponse = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.Equal("/Admin", verifyResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Verify_InvalidAuthenticatorCode_DoesNotGrantAdminAccess()
    {
        const string email = "admin-invalid-two-factor@example.com";
        await _factory.SeedUserAsync(
            "admin_invalid_two_factor",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage verifyResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Verify",
            "/Account/AdminTwoFactor/Verify",
            new Dictionary<string, string> { ["Code"] = "000000" });
        string html = WebUtility.HtmlDecode(
            await verifyResponse.Content.ReadAsStringAsync());
        HttpResponseMessage adminResponse = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        Assert.Contains("Mã xác thực không đúng", html);
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.Equal("/Account/Login", adminResponse.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Setup_AlreadyEnabledAdmin_DoesNotExposeAuthenticatorSecret()
    {
        const string email = "admin-enabled-setup@example.com";
        await _factory.SeedUserAsync(
            "admin_enabled_setup",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);

        HttpResponseMessage response =
            await client.GetAsync("/Account/AdminTwoFactor/Setup");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StepUp_FiveInvalidAuthenticatorCodes_LocksAccount()
    {
        const string email = "admin-step-up-lockout@example.com";
        await _factory.SeedUserAsync(
            "admin_step_up_lockout",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));

        HttpResponseMessage? finalResponse = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            finalResponse = await AdminWebApplicationFactory.SubmitFormAsync(
                client,
                "/Account/AdminTwoFactor/Verify?returnUrl=%2FAdmin%2FSecurity",
                "/Account/AdminTwoFactor/Verify",
                new Dictionary<string, string>
                {
                    ["Code"] = "invalid-code",
                    ["ReturnUrl"] = "/Admin/Security"
                });
        }

        Assert.True(await _factory.IsLockedOutAsync(email));
        Assert.Equal(HttpStatusCode.Redirect, finalResponse!.StatusCode);
        Assert.Equal("/Account/Login", finalResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task VerifiedAdmin_WhenTwoFactorIsDisabled_LosesAdminAccess()
    {
        const string email = "admin-disabled-two-factor@example.com";
        await _factory.SeedUserAsync(
            "admin_disabled_two_factor",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);
        await _factory.DisableTwoFactorAsync(email);

        HttpResponseMessage response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor?returnUrl=",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task VerifyPage_RecoveryLinkPreservesSensitiveReturnUrl()
    {
        const string email = "admin-return-url@example.com";
        await _factory.SeedUserAsync(
            "admin_return_url",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage response = await client.GetAsync(
            "/Account/AdminTwoFactor/Verify?returnUrl=%2FAdmin%2FSecurity");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "/Account/AdminTwoFactor/RecoveryCode?returnUrl=%2FAdmin%2FSecurity",
            html);
    }

    [Fact]
    public async Task AdminArea_AdminWithoutTwoFactor_IsSentThroughSetupGate()
    {
        const string email = "admin-two-factor-gate@example.com";
        await _factory.SeedUserAsync("admin_two_factor_gate", email, isAdmin: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage areaResponse = await client.GetAsync("/Admin");
        HttpResponseMessage gateResponse = await client.GetAsync(
            areaResponse.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.Redirect, areaResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor?returnUrl=",
            areaResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, gateResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor/Setup",
            gateResponse.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/")]
    public async Task VerifiedAdmin_PublicEntryPoint_RedirectsToAdmin(string requestPath)
    {
        string suffix = requestPath.Replace("/", "-", StringComparison.Ordinal).Trim('-');
        string email = $"admin-public-entry-{suffix}@example.com";
        await _factory.SeedUserAsync(
            $"admin_public_entry_{suffix.Replace('-', '_')}",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);
        string code = await _factory.GenerateAuthenticatorCodeAsync(email);
        await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Verify",
            "/Account/AdminTwoFactor/Verify",
            new Dictionary<string, string> { ["Code"] = code });

        HttpResponseMessage response = await client.GetAsync(requestPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task RecoveryCode_ValidCode_StartsVerifiedAdminSession()
    {
        const string email = "admin-recovery-code@example.com";
        await _factory.SeedUserAsync(
            "admin_recovery_code",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        IReadOnlyList<string> recoveryCodes =
            await _factory.GenerateRecoveryCodesAsync(email);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SubmitLoginAsync(client, email);

        HttpResponseMessage recoveryResponse =
            await AdminWebApplicationFactory.SubmitFormAsync(
                client,
                "/Account/AdminTwoFactor/RecoveryCode",
                "/Account/AdminTwoFactor/RecoveryCode",
                new Dictionary<string, string>
                {
                    ["RecoveryCode"] = recoveryCodes[0]
                });
        HttpResponseMessage adminResponse = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, recoveryResponse.StatusCode);
        Assert.Equal("/Admin", recoveryResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task SensitiveAdminPage_ExpiredVerification_RequiresVerificationAgain()
    {
        const string email = "admin-expired-verification@example.com";
        await _factory.SeedUserAsync(
            "admin_expired_verification",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);

        HttpResponseMessage recentResponse = await client.GetAsync("/Admin/Security");
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        HttpResponseMessage expiredResponse = await client.GetAsync("/Admin/Security");
        HttpResponseMessage gateResponse = await client.GetAsync(
            expiredResponse.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.OK, recentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, expiredResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor?returnUrl=",
            expiredResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, gateResponse.StatusCode);
        Assert.StartsWith(
            "/Account/AdminTwoFactor/Verify?returnUrl=",
            gateResponse.Headers.Location?.OriginalString);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static int CountOccurrences(string value, string searchValue) =>
        value.Split(searchValue, StringSplitOptions.None).Length - 1;
}
