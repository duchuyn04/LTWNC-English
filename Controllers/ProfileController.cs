using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Controllers;

public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly ICurrentUser _currentUser;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IAvatarService _avatarService;

    public ProfileController(
        IProfileService profileService,
        ICurrentUser currentUser,
        SignInManager<IdentityUser> signInManager,
        IAvatarService avatarService)
    {
        _profileService = profileService;
        _currentUser = currentUser;
        _signInManager = signInManager;
        _avatarService = avatarService;
    }

    [AllowAnonymous]
    [HttpGet("/u/{username}")]
    public async Task<IActionResult> Public(
        string username,
        CancellationToken cancellationToken)
    {
        PublicProfileViewModel? model = await _profileService.GetPublicProfileAsync(
            username,
            _currentUser.UserId,
            cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model.IsPrivate ? "Private" : "Public", model);
    }

    [Authorize]
    [HttpGet("/Account/Profile/Edit")]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        ProfileEditViewModel model = await _profileService.GetEditModelAsync(
            _currentUser.UserId,
            cancellationToken);
        return View(model);
    }

    [Authorize]
    [HttpPost("/Account/Profile/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        ProfileEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        ProfileOperationResult result = await _profileService.UpdateProfileAsync(
            _currentUser.UserId,
            model,
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View("Edit", model);
        }

        IdentityUser? user = await _signInManager.UserManager.FindByIdAsync(_currentUser.UserId);
        if (user != null)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["Success"] = "Đã cập nhật profile.";
        return RedirectToAction(nameof(Edit));
    }

    [Authorize]
    [HttpPost("/Account/Profile/ChangeEmail")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(
        ChangeEmailViewModel model,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            ProfileEditViewModel editModel = await _profileService.GetEditModelAsync(
                _currentUser.UserId,
                cancellationToken);
            return View("Edit", editModel);
        }

        ProfileOperationResult result = await _profileService.ChangeEmailAsync(
            _currentUser.UserId,
            model,
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ProfileEditViewModel editModel = await _profileService.GetEditModelAsync(
                _currentUser.UserId,
                cancellationToken);
            return View("Edit", editModel);
        }

        TempData["Success"] = "Đã cập nhật email.";
        return RedirectToAction(nameof(Edit));
    }

    [Authorize]
    [HttpPost("/Account/Profile/ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            ProfileEditViewModel editModel = await _profileService.GetEditModelAsync(
                _currentUser.UserId,
                cancellationToken);
            return View("Edit", editModel);
        }

        ProfileOperationResult result = await _profileService.ChangePasswordAsync(
            _currentUser.UserId,
            model,
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ProfileEditViewModel editModel = await _profileService.GetEditModelAsync(
                _currentUser.UserId,
                cancellationToken);
            return View("Edit", editModel);
        }

        IdentityUser? user = await _signInManager.UserManager.FindByIdAsync(_currentUser.UserId);
        if (user != null)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["Success"] = "Đã đổi mật khẩu.";
        return RedirectToAction(nameof(Edit));
    }

    [Authorize]
    [HttpPost("/Account/Profile/Avatar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avatar(
        IFormFile avatar,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        AvatarUploadResult result = await _avatarService.ReplaceAvatarAsync(
            _currentUser.UserId,
            avatar,
            cancellationToken);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Edit));
        }

        TempData["Success"] = "Đã cập nhật ảnh đại diện.";
        return RedirectToAction(nameof(Edit));
    }

    private void AddErrors(ProfileOperationResult result)
    {
        foreach (ProfileFieldError error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
    }
}
