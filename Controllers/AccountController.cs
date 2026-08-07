using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ltwnc.Controllers;

// Xử lý xác thực local, OTP email, đăng nhập Google và đăng xuất.
public class AccountController : Controller
{
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    private readonly IAuthService _authService;
    private readonly IAccountSecurityService _accountSecurityService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly GoogleAuthSettings _googleSettings;
    private readonly ITimeLimitedDataProtector _googleLinkProtector;

    public AccountController(
        IAuthService authService,
        IAccountSecurityService accountSecurityService,
        IAdminAuditService adminAuditService,
        IOptions<GoogleAuthSettings> googleSettings,
        IDataProtectionProvider dataProtectionProvider)
    {
        _authService = authService;
        _accountSecurityService = accountSecurityService;
        _adminAuditService = adminAuditService;
        _googleSettings = googleSettings.Value;
        _googleLinkProtector = dataProtectionProvider
            .CreateProtector("ltwnc.account.google-link")
            .ToTimeLimitedDataProtector();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        SetGoogleLoginViewData();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        RegisterViewModel model,
        CancellationToken cancellationToken)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        SetGoogleLoginViewData();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? usernameError = UsernamePolicy.GetValidationError(model.Username);
        if (usernameError != null)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.Username), usernameError);
            return View(model);
        }

        RegistrationStartResult result = await _accountSecurityService.StartLocalRegistrationAsync(
            model.Email,
            model.Username,
            model.Password,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded || result.ChallengeId == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi mã xác thực.");
            return View(model);
        }

        return RedirectToAction(
            nameof(VerifyRegistration),
            new { challengeId = result.ChallengeId });
    }

    [HttpGet]
    public IActionResult VerifyRegistration(string challengeId)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            return RedirectToAction(nameof(Register));
        }

        return View(new VerifyRegistrationViewModel { ChallengeId = challengeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyRegistration(
        VerifyRegistrationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountSecurityResult result = await _accountSecurityService.VerifyRegistrationAsync(
            model.ChallengeId,
            model.Code,
            cancellationToken);
        if (!result.Succeeded || result.UserId == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Mã OTP không hợp lệ.");
            return View(model);
        }

        AppUser? user = await _authService.FindByIdAsync(result.UserId, cancellationToken);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Không thể hoàn tất đăng ký.");
            return View(model);
        }

        await _authService.SignInAsync(user, RegisterCookieLifetime);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendRegistrationOtp(
        string challengeId,
        CancellationToken cancellationToken)
    {
        RegistrationStartResult result = await _accountSecurityService.ResendOtpAsync(
            challengeId,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded || result.ChallengeId == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi lại mã OTP.");
            return View("VerifyRegistration", new VerifyRegistrationViewModel
            {
                ChallengeId = challengeId
            });
        }

        return RedirectToAction(nameof(VerifyRegistration), new { challengeId = result.ChallengeId });
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        SetGoogleLoginViewData();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!ModelState.IsValid)
        {
            SetGoogleLoginViewData();
            return View(model);
        }

        AppUser? user = await _authService.FindByUsernameAsync(
            model.Username.Trim(),
            cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            SetGoogleLoginViewData();
            return View(model);
        }

        AuthResult result = await _authService.ValidateLoginAsync(
            user,
            model.Password,
            cancellationToken);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                AddLockedAccountMessage();
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            }

            SetGoogleLoginViewData();
            return View(model);
        }

        TimeSpan lifetime = model.RememberMe
            ? RememberMeCookieLifetime
            : SessionCookieLifetime;
        await _authService.SignInAsync(user, lifetime);

        if (user.IsAdmin)
        {
            await RecordAdminSignInAuditAsync(user);
            return Redirect("/Admin");
        }

        return Redirect("/Set");
    }

    [HttpGet]
    [EnableRateLimiting("auth")]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!_googleSettings.IsConfigured)
        {
            TempData["Error"] = "Đăng nhập Google chưa được cấu hình.";
            return RedirectToAction(nameof(Login));
        }

        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        AuthenticationProperties properties = new()
        {
            RedirectUri = Url.Action(
                nameof(GoogleCallback),
                "Account",
                new { returnUrl = safeReturnUrl })
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback(
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        AuthenticateResult external = await HttpContext.AuthenticateAsync(
            AuthSchemes.ExternalCookie);
        await HttpContext.SignOutAsync(AuthSchemes.ExternalCookie);

        if (!external.Succeeded || external.Principal == null)
        {
            TempData["Error"] = "Không thể xác thực tài khoản Google.";
            return RedirectToAction(nameof(Login));
        }

        string? googleSubjectId = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = external.Principal.FindFirstValue(ClaimTypes.Email);
        string? verifiedValue = external.Principal.FindFirstValue("urn:google:verified_email")
            ?? external.Principal.FindFirstValue("email_verified")
            ?? external.Principal.FindFirstValue("verified_email");
        bool emailVerified = string.Equals(verifiedValue, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(googleSubjectId) ||
            string.IsNullOrWhiteSpace(email) ||
            !emailVerified)
        {
            TempData["Error"] = "Google chưa xác nhận email của tài khoản này.";
            return RedirectToAction(nameof(Login));
        }

        AppUser? user = await _authService.FindByEmailAsync(email, cancellationToken);
        int atIndex = email.IndexOf('@');
        if (user == null && atIndex > 0)
        {
            string userNameCandidate = email[..atIndex];
            AuthResult createResult = await _authService.CreateGoogleUserAsync(
                email,
                userNameCandidate,
                googleSubjectId,
                cancellationToken);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = createResult.Errors.FirstOrDefault()?.Message
                    ?? "Không thể tạo tài khoản Google.";
                return RedirectToAction(nameof(Login));
            }

            user = await _authService.FindByEmailAsync(email, cancellationToken);
        }

        if (user == null)
        {
            TempData["Error"] = "Không thể hoàn tất đăng nhập Google.";
            return RedirectToAction(nameof(Login));
        }

        if (string.Equals(user.GoogleSubjectId, googleSubjectId, StringComparison.Ordinal))
        {
            await _authService.SignInAsync(user, SessionCookieLifetime);

            if (user.IsAdmin)
            {
                await RecordAdminSignInAuditAsync(user);
                return Redirect("/Admin");
            }

            return Redirect(GetSafeReturnUrl(returnUrl, "/Set"));
        }

        if (!string.IsNullOrWhiteSpace(user.GoogleSubjectId))
        {
            TempData["Error"] = "Email này đã liên kết với tài khoản Google khác.";
            return RedirectToAction(nameof(Login));
        }

        return View("LinkGoogle", new GoogleLinkViewModel
        {
            Ticket = ProtectGoogleLinkTicket(user.Id, user.Email, googleSubjectId),
            Email = user.Email
        });
    }

    [HttpGet]
    public IActionResult LinkGoogle(string ticket)
    {
        return TryGetGoogleLink(ticket, out GoogleLinkPayload payload)
            ? View(new GoogleLinkViewModel { Ticket = ticket, Email = payload.Email })
            : RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LinkGooglePassword(
        GoogleLinkViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetGoogleLink(model.Ticket, out GoogleLinkPayload payload))
        {
            ModelState.AddModelError(string.Empty, "Yêu cầu liên kết đã hết hạn.");
            return View("LinkGoogle", model);
        }

        model.Email = payload.Email;
        if (!ModelState.IsValid)
        {
            return View("LinkGoogle", model);
        }

        AppUser? user = await _authService.FindByIdAsync(payload.UserId, cancellationToken);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Không thể liên kết tài khoản Google.");
            return View("LinkGoogle", model);
        }

        AuthResult passwordResult = await _authService.ValidateLoginAsync(
            user,
            model.Password,
            cancellationToken);
        if (!passwordResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu hiện tại không đúng.");
            return View("LinkGoogle", model);
        }

        AuthResult linkResult = await _authService.LinkGoogleAsync(
            user,
            payload.GoogleSubjectId,
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, linkResult.Errors.FirstOrDefault()?.Message
                ?? "Không thể liên kết tài khoản Google.");
            return View("LinkGoogle", model);
        }

        return await CompleteGoogleLinkSignInAsync(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SendGoogleLinkOtp(
        string ticket,
        CancellationToken cancellationToken)
    {
        if (!TryGetGoogleLink(ticket, out GoogleLinkPayload payload))
        {
            ModelState.AddModelError(string.Empty, "Yêu cầu liên kết đã hết hạn.");
            return View("LinkGoogle", new GoogleLinkViewModel { Ticket = ticket });
        }

        RegistrationStartResult result = await _accountSecurityService.StartGoogleLinkOtpAsync(
            payload.UserId,
            payload.GoogleSubjectId,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded || result.ChallengeId == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi OTP.");
            return View("LinkGoogle", new GoogleLinkViewModel
            {
                Ticket = ticket,
                Email = payload.Email
            });
        }

        return RedirectToAction(nameof(LinkGoogleOtp), new { challengeId = result.ChallengeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendGoogleLinkOtp(
        string challengeId,
        CancellationToken cancellationToken)
    {
        RegistrationStartResult result = await _accountSecurityService.ResendOtpAsync(
            challengeId,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi lại mã OTP.");
            return View("LinkGoogleOtp", new GoogleLinkOtpViewModel { ChallengeId = challengeId });
        }

        return RedirectToAction(
            nameof(LinkGoogleOtp),
            new { challengeId = result.ChallengeId ?? challengeId });
    }

    [HttpGet]
    public IActionResult LinkGoogleOtp(string challengeId)
    {
        return View(new GoogleLinkOtpViewModel { ChallengeId = challengeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LinkGoogleOtp(
        GoogleLinkOtpViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountSecurityResult result = await _accountSecurityService.CompleteGoogleLinkOtpAsync(
            model.ChallengeId,
            model.Code,
            cancellationToken);
        if (!result.Succeeded || result.UserId == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Mã OTP không hợp lệ.");
            return View(model);
        }

        AppUser? user = await _authService.FindByIdAsync(result.UserId, cancellationToken);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Không thể hoàn tất liên kết.");
            return View(model);
        }

        return await CompleteGoogleLinkSignInAsync(user);
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        PasswordResetStartResult result = await _accountSecurityService.StartPasswordResetAsync(
            model.Email,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi email lúc này.");
            return View(model);
        }

        return RedirectToAction(nameof(ResetPassword), new { challengeId = result.ChallengeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendPasswordResetOtp(
        string challengeId,
        CancellationToken cancellationToken)
    {
        RegistrationStartResult result = await _accountSecurityService.ResendOtpAsync(
            challengeId,
            GetRequestIpAddress(),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi lại mã OTP.");
            return View("ResetPassword", new ResetPasswordViewModel { ChallengeId = challengeId });
        }

        return RedirectToAction(
            nameof(ResetPassword),
            new { challengeId = result.ChallengeId ?? challengeId });
    }

    [HttpGet]
    public IActionResult ResetPassword(string challengeId)
    {
        return View(new ResetPasswordViewModel { ChallengeId = challengeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountSecurityResult result = await _accountSecurityService.CompletePasswordResetAsync(
            model.ChallengeId,
            model.Code,
            model.NewPassword,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể đặt lại mật khẩu.");
            return View(model);
        }

        TempData["Success"] = "Mật khẩu đã được đặt lại. Vui lòng đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private string GetAuthenticatedLandingPath()
    {
        return User.HasClaim(AppClaimTypes.IsAdmin, "true") ? "/Admin" : "/Set";
    }

    private void SetGoogleLoginViewData()
    {
        ViewData["GoogleLoginEnabled"] = _googleSettings.IsConfigured;
    }

    private string? GetRequestIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string GetSafeReturnUrl(string? returnUrl, string fallback = "/Set")
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : fallback;
    }

    private string ProtectGoogleLinkTicket(
        string userId,
        string email,
        string googleSubjectId)
    {
        string payload = JsonSerializer.Serialize(new GoogleLinkPayload(
            userId,
            email,
            googleSubjectId));
        return _googleLinkProtector.Protect(payload, TimeSpan.FromMinutes(10));
    }

    private bool TryGetGoogleLink(
        string ticket,
        out GoogleLinkPayload payload)
    {
        try
        {
            string json = _googleLinkProtector.Unprotect(ticket);
            payload = JsonSerializer.Deserialize<GoogleLinkPayload>(json)
                ?? throw new InvalidOperationException("Google link ticket trống.");
            return !string.IsNullOrWhiteSpace(payload.UserId) &&
                !string.IsNullOrWhiteSpace(payload.GoogleSubjectId);
        }
        catch (CryptographicException)
        {
            payload = new GoogleLinkPayload(string.Empty, string.Empty, string.Empty);
            return false;
        }
        catch (JsonException)
        {
            payload = new GoogleLinkPayload(string.Empty, string.Empty, string.Empty);
            return false;
        }
        catch (ArgumentException)
        {
            payload = new GoogleLinkPayload(string.Empty, string.Empty, string.Empty);
            return false;
        }
    }

    private async Task<IActionResult> CompleteGoogleLinkSignInAsync(AppUser user)
    {
        await _authService.SignInAsync(user, SessionCookieLifetime);

        if (user.IsAdmin)
        {
            await RecordAdminSignInAuditAsync(user);
            return Redirect("/Admin");
        }

        return Redirect("/Set");
    }

    private async Task RecordAdminSignInAuditAsync(AppUser user)
    {
        await _adminAuditService.RecordAsync(new AdminAuditEntry(
            ActorUserId: user.Id,
            ActorDisplay: user.Email,
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "AppUser",
            TargetId: user.Id,
            CorrelationId: HttpContext.TraceIdentifier));
    }

    private void AddLockedAccountMessage()
    {
        ModelState.AddModelError(
            string.Empty,
            "Tài khoản hiện không thể đăng nhập. Vui lòng liên hệ bộ phận hỗ trợ để được kiểm tra.");
    }

    private sealed record GoogleLinkPayload(
        string UserId,
        string Email,
        string GoogleSubjectId);
}
