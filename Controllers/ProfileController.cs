using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Controllers;

// Hiển thị hồ sơ công khai và xử lý cập nhật hồ sơ, mật khẩu, ảnh đại diện.
public class ProfileController : Controller
{
    // Tên route dùng để tạo và chuyển hướng tới đường dẫn hồ sơ chuẩn.
    public const string PublicProfileRouteName = "PublicProfile";

    // Các service quản lý hồ sơ, người dùng hiện tại, đăng nhập và ảnh đại diện.
    private readonly IProfileService _profileService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthService _authService;
    private readonly IAvatarService _avatarService;

    // Nhận các service cần dùng qua dependency injection.
    public ProfileController(
        IProfileService profileService,
        ICurrentUser currentUser,
        IAuthService authService,
        IAvatarService avatarService)
    {
        // 1. Lưu các service để những action hồ sơ sử dụng.
        _profileService = profileService;
        _currentUser = currentUser;
        _authService = authService;
        _avatarService = avatarService;
    }

    // Hiển thị hồ sơ theo username chuẩn; tự chuyển hướng nếu khác chữ hoa/chữ thường.
    [AllowAnonymous]
    [HttpGet("/{username:profileUsername}", Name = PublicProfileRouteName)]
    public async Task<IActionResult> Public(
        string username,
        CancellationToken cancellationToken)
    {
        // 1. Lấy hồ sơ theo username và quyền xem của người truy cập.
        // 2. Chuẩn hóa chữ hoa/chữ thường bằng đường dẫn chính thức.
        // 3. Hiển thị view công khai hoặc riêng tư phù hợp.
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

    // Giữ đường dẫn hồ sơ cũ và chuyển vĩnh viễn sang đường dẫn chuẩn.
    [AllowAnonymous]
    [HttpGet("/u/{username}")]
    public IActionResult LegacyPublic(string username)
    {
        // 1. Loại bỏ khoảng trắng và kiểm tra username từ đường dẫn cũ.
        // 2. Trả 404 nếu username sai định dạng.
        // 3. Chuyển vĩnh viễn sang đường dẫn hồ sơ chuẩn.
        string candidate = username.Trim();
        if (!UsernamePolicy.IsValid(candidate))
        {
            return NotFound();
        }

        return RedirectToRoutePermanent(
            PublicProfileRouteName,
            new { username = candidate });
    }

    // Hiển thị form chỉnh sửa hồ sơ của người đang đăng nhập.
    [Authorize]
    [HttpGet("/Account/Profile/Edit")]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Lấy dữ liệu hồ sơ hiện tại để điền vào form.
        // 3. Hiển thị trang chỉnh sửa.
        if (_currentUser.UserId == null)
        {
            return Challenge();
        }

        ProfileEditViewModel model = await _profileService.GetEditModelAsync(
            _currentUser.UserId,
            cancellationToken);
        return View(model);
    }

    // Lưu thay đổi hồ sơ và làm mới cookie nếu thông tin người dùng thay đổi.
    [Authorize]
    [HttpPost("/Account/Profile/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        ProfileEditViewModel model,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu form.
        // 2. Cập nhật hồ sơ, đồng thời khôi phục dữ liệu hiển thị nếu có lỗi.
        // 3. Làm mới cookie và chuyển hướng khi lưu thành công.
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

    // Đổi mật khẩu và làm mới phiên đăng nhập sau khi thành công.
    [Authorize]
    [HttpPost("/Account/Profile/ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu đổi mật khẩu.
        // 2. Yêu cầu service đổi mật khẩu, đưa lỗi về form nếu thất bại.
        // 3. Làm mới cookie rồi thông báo thành công.
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

    // Thay ảnh đại diện và đưa thông báo kết quả về trang chỉnh sửa.
    [Authorize]
    [HttpPost("/Account/Profile/Avatar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avatar(
        IFormFile avatar,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Gửi tệp ảnh tới service để kiểm tra và thay ảnh cũ.
        // 3. Lưu thông báo kết quả rồi quay về trang chỉnh sửa.
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

    // Đưa lỗi nghiệp vụ vào ModelState đúng với từng trường trên form.
    private void AddErrors(ProfileOperationResult result)
    {
        // 1. Duyệt từng lỗi nghiệp vụ do service trả về.
        // 2. Gắn lỗi vào đúng trường để form hiển thị đúng vị trí.
        foreach (ProfileFieldError error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
    }

    // Khôi phục dữ liệu chỉ đọc của form khi cần hiển thị lại thông tin người dùng đã nhập.
    private async Task<ProfileEditViewModel> RestoreEditDisplayContextAsync(
        string userId,
        ProfileEditViewModel submittedModel,
        CancellationToken cancellationToken)
    {
        // 1. Tải lại email và thông tin ảnh chỉ đọc từ dữ liệu hiện tại.
        // 2. Giữ nguyên các giá trị người dùng vừa nhập trên form.
        // 3. Trả ViewModel đầy đủ để hiển thị lại khi có lỗi.
        ProfileEditViewModel currentModel = await _profileService.GetEditModelAsync(
            userId,
            cancellationToken);

        return new ProfileEditViewModel
        {
            Username = submittedModel.Username,
            Bio = submittedModel.Bio,
            IsPublic = submittedModel.IsPublic,
            ShowStats = true,
            ShowBadges = submittedModel.ShowBadges,
            ShowActivity = submittedModel.ShowActivity,
            ShowPublicSets = submittedModel.ShowPublicSets,
            Email = currentModel.Email,
            AvatarPath = currentModel.AvatarPath,
            AvatarInitial = currentModel.AvatarInitial
        };
    }
}
