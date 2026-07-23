using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Controllers;

public class ProfileController : Controller
{
    public const string PublicProfileRouteName = "PublicProfile";

    private readonly IProfileService _profileService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthService _authService;
    private readonly IAvatarService _avatarService;

    public ProfileController(
        IProfileService profileService,
        ICurrentUser currentUser,
        IAuthService authService,
        IAvatarService avatarService)
    {
        _profileService = profileService;
        _currentUser = currentUser;
        _authService = authService;
        _avatarService = avatarService;
    }

    [AllowAnonymous]
    [HttpGet("/{username:profileUsername}", Name = PublicProfileRouteName)]
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

        if (!string.Equals(username, model.Username, StringComparison.Ordinal))
        {
            return RedirectToRoutePermanent(
                PublicProfileRouteName,
                new { username = model.Username });
        }

        return View(model.IsPrivate ? "Private" : "Public", model);
    }

    [AllowAnonymous]
    [HttpGet("/u/{username}")]
    public IActionResult LegacyPublic(string username)
    {
        string candidate = username.Trim();
        if (!UsernamePolicy.IsValid(candidate))
        {
            return NotFound();
        }

        return RedirectToRoutePermanent(
            PublicProfileRouteName,
            new { username = candidate });
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
            return View(
                "Edit",
                await RestoreEditDisplayContextAsync(
                    _currentUser.UserId,
                    model,
                    cancellationToken));
        }

        ProfileOperationResult result = await _profileService.UpdateProfileAsync(
            _currentUser.UserId,
            model,
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(
                "Edit",
                await RestoreEditDisplayContextAsync(
                    _currentUser.UserId,
                    model,
                    cancellationToken));
        }

        AppUser? user = await _authService.FindByIdAsync(_currentUser.UserId);
        if (user != null)
        {
            await _authService.RefreshSignInAsync(user);
        }

        TempData["Success"] = "Đã cập nhật profile.";
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

        AppUser? user = await _authService.FindByIdAsync(_currentUser.UserId);
        if (user != null)
        {
            await _authService.RefreshSignInAsync(user);
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

    private async Task<ProfileEditViewModel> RestoreEditDisplayContextAsync(
        string userId,
        ProfileEditViewModel submittedModel,
        CancellationToken cancellationToken)
    {
        ProfileEditViewModel currentModel = await _profileService.GetEditModelAsync(
            userId,
            cancellationToken);

        return new ProfileEditViewModel
        {
            Username = submittedModel.Username,
            Bio = submittedModel.Bio,
            IsPublic = submittedModel.IsPublic,
            ShowStats = submittedModel.ShowStats,
            ShowBadges = submittedModel.ShowBadges,
            ShowActivity = submittedModel.ShowActivity,
            ShowPublicSets = submittedModel.ShowPublicSets,
            Email = currentModel.Email,
            AvatarPath = currentModel.AvatarPath,
            AvatarInitial = currentModel.AvatarInitial
        };
    }
}
