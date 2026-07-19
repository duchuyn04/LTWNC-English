using ltwnc.Areas.Admin;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Controllers;

[Authorize(Roles = AdminRoleBootstrapper.AdminRole)]
[Route("Account/AdminTwoFactor")]
public sealed class AdminTwoFactorController : Controller
{
    private const string AuthenticatorIssuer = "LTWNC English";
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly AdminAuthenticationSession _adminAuthenticationSession;
    private readonly TimeProvider _timeProvider;

    public AdminTwoFactorController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        AdminAuthenticationSession adminAuthenticationSession,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _adminAuthenticationSession = adminAuthenticationSession;
        _timeProvider = timeProvider;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Redirect(
                $"/Account/AdminTwoFactor/Setup?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        if (AdminAuthenticationSession.IsRecent(
                User,
                _timeProvider.GetUtcNow(),
                AdminAreaPolicy.RecentAuthenticationLifetime))
        {
            return LocalRedirect(safeReturnUrl);
        }

        return Redirect(
            $"/Account/AdminTwoFactor/Verify?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
    }

    [HttpGet("Setup")]
    public async Task<IActionResult> Setup(string? returnUrl = null)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToAction(nameof(Index), new { returnUrl = GetSafeReturnUrl(returnUrl) });
        }

        string? authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            IdentityResult resetResult = await _userManager.ResetAuthenticatorKeyAsync(user);
            if (!resetResult.Succeeded)
            {
                throw new InvalidOperationException("Không thể tạo khóa xác thực hai bước.");
            }

            await _signInManager.RefreshSignInAsync(user);

            authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            throw new InvalidOperationException("Khóa xác thực hai bước không khả dụng.");
        }

        return View(CreateSetupModel(user, authenticatorKey, GetSafeReturnUrl(returnUrl)));
    }

    [HttpPost("Setup")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Setup(AdminTwoFactorSetupViewModel model)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToAction(nameof(Index), new { returnUrl = GetSafeReturnUrl(model.ReturnUrl) });
        }

        string? authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            return RedirectToAction(nameof(Setup), new { returnUrl = GetSafeReturnUrl(model.ReturnUrl) });
        }

        if (!ModelState.IsValid)
        {
            return View(CreateSetupModel(user, authenticatorKey, GetSafeReturnUrl(model.ReturnUrl)));
        }

        string verificationCode = model.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        bool isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            verificationCode);
        if (!isValid)
        {
            ModelState.AddModelError(nameof(model.Code), "Mã xác thực không đúng.");
            return View(CreateSetupModel(user, authenticatorKey, GetSafeReturnUrl(model.ReturnUrl)));
        }

        EnsureSucceeded(
            await _userManager.SetTwoFactorEnabledAsync(user, true),
            "Không thể bật xác thực hai bước.");
        IEnumerable<string>? recoveryCodes =
            await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        if (recoveryCodes == null)
        {
            throw new InvalidOperationException("Không thể tạo mã khôi phục.");
        }

        AuthenticateResult currentAuthentication =
            await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        await _adminAuthenticationSession.SignInVerifiedAsync(
            user,
            currentAuthentication.Properties ?? new AuthenticationProperties());

        return View("RecoveryCodes", new AdminRecoveryCodesViewModel
        {
            RecoveryCodes = recoveryCodes.ToArray(),
            ReturnUrl = GetSafeReturnUrl(model.ReturnUrl)
        });
    }

    [HttpGet("Verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(string? returnUrl = null)
    {
        IdentityUser? user = await GetVerificationUserAsync();
        if (user == null)
        {
            return Redirect("/Account/Login");
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole))
        {
            return Forbid();
        }

        IActionResult? disabledResult =
            await RedirectWhenTwoFactorDisabledAsync(user);
        if (disabledResult != null)
        {
            return disabledResult;
        }

        return View(new AdminTwoFactorVerifyViewModel
        {
            ReturnUrl = GetSafeReturnUrl(returnUrl)
        });
    }

    [HttpPost("Verify")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Verify(AdminTwoFactorVerifyViewModel model)
    {
        IdentityUser? pendingUser =
            await _signInManager.GetTwoFactorAuthenticationUserAsync();
        IdentityUser? user = pendingUser ?? await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Redirect("/Account/Login");
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole))
        {
            return Forbid();
        }

        IActionResult? disabledResult =
            await RedirectWhenTwoFactorDisabledAsync(user);
        if (disabledResult != null)
        {
            return disabledResult;
        }

        model.ReturnUrl = GetSafeReturnUrl(model.ReturnUrl);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string verificationCode = NormalizeCode(model.Code);
        Microsoft.AspNetCore.Identity.SignInResult result;
        if (pendingUser != null)
        {
            result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                verificationCode,
                model.RememberMe,
                rememberClient: false);
            if (result.IsLockedOut)
            {
                return await SignOutLockedUserAsync();
            }
        }
        else
        {
            bool isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                verificationCode);
            if (!isValid && await RecordFailedStepUpAsync(user))
            {
                return await SignOutLockedUserAsync();
            }

            if (isValid)
            {
                EnsureSucceeded(
                    await _userManager.ResetAccessFailedCountAsync(user),
                    "Không thể đặt lại số lần xác thực sai.");
            }

            result = isValid
                ? Microsoft.AspNetCore.Identity.SignInResult.Success
                : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.Code), "Mã xác thực không đúng.");
            return View(model);
        }

        AuthenticationProperties properties = await GetCurrentAuthenticationPropertiesAsync();
        properties.IsPersistent = properties.IsPersistent || model.RememberMe;
        await _adminAuthenticationSession.SignInVerifiedAsync(user, properties);
        return LocalRedirect(model.ReturnUrl);
    }

    [HttpGet("RecoveryCode")]
    [AllowAnonymous]
    public async Task<IActionResult> RecoveryCode(string? returnUrl = null)
    {
        IdentityUser? user = await GetVerificationUserAsync();
        if (user == null)
        {
            return Redirect("/Account/Login");
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole))
        {
            return Forbid();
        }

        IActionResult? disabledResult =
            await RedirectWhenTwoFactorDisabledAsync(user);
        if (disabledResult != null)
        {
            return disabledResult;
        }

        return View(new AdminTwoFactorRecoveryCodeViewModel
        {
            ReturnUrl = GetSafeReturnUrl(returnUrl)
        });
    }

    [HttpPost("RecoveryCode")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RecoveryCode(
        AdminTwoFactorRecoveryCodeViewModel model)
    {
        IdentityUser? pendingUser =
            await _signInManager.GetTwoFactorAuthenticationUserAsync();
        IdentityUser? user = pendingUser ?? await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Redirect("/Account/Login");
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole))
        {
            return Forbid();
        }

        IActionResult? disabledResult =
            await RedirectWhenTwoFactorDisabledAsync(user);
        if (disabledResult != null)
        {
            return disabledResult;
        }

        model.ReturnUrl = GetSafeReturnUrl(model.ReturnUrl);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string recoveryCode = model.RecoveryCode
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        bool succeeded;
        if (pendingUser != null)
        {
            Microsoft.AspNetCore.Identity.SignInResult result =
                await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
            if (result.IsLockedOut)
            {
                return await SignOutLockedUserAsync();
            }

            succeeded = result.Succeeded;
        }
        else
        {
            IdentityResult result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(
                user,
                recoveryCode);
            succeeded = result.Succeeded;
            if (!succeeded && await RecordFailedStepUpAsync(user))
            {
                return await SignOutLockedUserAsync();
            }

            if (succeeded)
            {
                EnsureSucceeded(
                    await _userManager.ResetAccessFailedCountAsync(user),
                    "Không thể đặt lại số lần xác thực sai.");
            }
        }

        if (!succeeded)
        {
            ModelState.AddModelError(
                nameof(model.RecoveryCode),
                "Mã khôi phục không đúng hoặc đã được sử dụng.");
            return View(model);
        }

        await _adminAuthenticationSession.SignInVerifiedAsync(
            user,
            await GetCurrentAuthenticationPropertiesAsync());
        return LocalRedirect(model.ReturnUrl);
    }

    private static AdminTwoFactorSetupViewModel CreateSetupModel(
        IdentityUser user,
        string authenticatorKey,
        string returnUrl)
    {
        string accountName = user.Email ?? user.UserName ?? user.Id;
        string uri = $"otpauth://totp/{Uri.EscapeDataString(AuthenticatorIssuer)}:{Uri.EscapeDataString(accountName)}"
            + $"?secret={authenticatorKey}&issuer={Uri.EscapeDataString(AuthenticatorIssuer)}&digits=6";

        return new AdminTwoFactorSetupViewModel
        {
            SharedKey = FormatKey(authenticatorKey),
            AuthenticatorUri = uri,
            ReturnUrl = returnUrl
        };
    }

    private async Task<IdentityUser?> GetVerificationUserAsync() =>
        await _signInManager.GetTwoFactorAuthenticationUserAsync()
        ?? await _userManager.GetUserAsync(User);

    private async Task<IActionResult?> RedirectWhenTwoFactorDisabledAsync(
        IdentityUser user)
    {
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return null;
        }

        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Setup))
            : Redirect("/Account/Login");
    }

    private async Task<bool> RecordFailedStepUpAsync(IdentityUser user)
    {
        EnsureSucceeded(
            await _userManager.AccessFailedAsync(user),
            "Không thể ghi nhận lần xác thực sai.");
        return await _userManager.IsLockedOutAsync(user);
    }

    private async Task<IActionResult> SignOutLockedUserAsync()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/Account/Login");
    }

    private async Task<AuthenticationProperties> GetCurrentAuthenticationPropertiesAsync()
    {
        AuthenticateResult authentication =
            await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        return authentication.Properties ?? new AuthenticationProperties();
    }

    private string GetSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/Admin";

    private static string NormalizeCode(string code) => code
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FormatKey(string key) => string.Join(
        ' ',
        Enumerable.Range(0, (key.Length + 3) / 4)
            .Select(index => key.Substring(
                index * 4,
                Math.Min(4, key.Length - (index * 4)))))
        .ToLowerInvariant();

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(message);
        }
    }
}
