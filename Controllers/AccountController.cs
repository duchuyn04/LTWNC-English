using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Controllers;

// Xử lý đăng ký, đăng nhập, đăng xuất và điều hướng người dùng sau khi xác thực.
public class AccountController : Controller
{
    // Thời gian duy trì đăng nhập cho từng trường hợp xác thực.
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    // Service xác thực tài khoản và service ghi lịch sử đăng nhập của Admin.
    private readonly IAuthService _authService;
    private readonly IAdminAuditService _adminAuditService;

    // Nhận các service cần dùng qua dependency injection.
    public AccountController(
        IAuthService authService,
        IAdminAuditService adminAuditService)
    {
        // 1. Lưu các service để những action bên dưới có thể sử dụng lại.
        _authService = authService;
        _adminAuditService = adminAuditService;
    }

    // Hiển thị trang đăng ký; người đã đăng nhập được chuyển về khu vực phù hợp.
    [HttpGet]
    public IActionResult Register()
    {
        // 1. Không cho người đã đăng nhập mở lại trang đăng ký.
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        // 2. Người chưa đăng nhập được hiển thị form đăng ký.
        return View();
    }

    // Tạo tài khoản mới, đăng nhập ngay sau khi thành công và giới hạn tần suất gửi form.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // 1. Chuyển người đã đăng nhập về đúng khu vực thay vì tạo thêm tài khoản.
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        // 2. Trả lại form nếu các validation attribute phát hiện dữ liệu không hợp lệ.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 3. Kiểm tra thêm quy tắc riêng của username trước khi gọi service.
        string? usernameError = UsernamePolicy.GetValidationError(model.Username);
        if (usernameError != null)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.Username), usernameError);
            return View(model);
        }

        // 4. Chuẩn hóa email, username rồi yêu cầu service tạo tài khoản.
        AuthResult result = await _authService.RegisterAsync(
            model.Email.Trim(),
            model.Username.Trim(),
            model.Password);
        // 5. Đưa lỗi nghiệp vụ về ModelState để hiển thị trên form.
        if (!result.Succeeded)
        {
            foreach (AuthError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            return View(model);
        }

        // 6. Tìm tài khoản vừa tạo và đăng nhập tự động trong một ngày.
        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user != null)
        {
            await _authService.SignInAsync(user, RegisterCookieLifetime);
        }

        // 7. Hoàn tất đăng ký và chuyển về trang chủ.
        return RedirectToAction("Index", "Home");
    }

    // Hiển thị trang đăng nhập nếu người dùng chưa xác thực.
    [HttpGet]
    public Task<IActionResult> Login()
    {
        // 1. Người đã đăng nhập được chuyển thẳng về khu vực của mình.
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<IActionResult>(Redirect(GetAuthenticatedLandingPath()));
        }

        // 2. Người chưa đăng nhập được hiển thị form.
        return Task.FromResult<IActionResult>(View());
    }

    // Kiểm tra thông tin đăng nhập, tạo cookie và chuyển Admin vào trang quản trị.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // 1. Không xử lý đăng nhập lại nếu đã có phiên hợp lệ.
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        // 2. Dừng sớm khi dữ liệu form chưa hợp lệ.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 3. Tìm tài khoản theo email đã loại bỏ khoảng trắng thừa.
        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // 4. Kiểm tra mật khẩu và trạng thái khóa của tài khoản.
        AuthResult result = await _authService.ValidateLoginAsync(user, model.Password);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                AddLockedAccountMessage();
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // 5. Chọn thời hạn cookie theo tùy chọn "Ghi nhớ đăng nhập".
        TimeSpan lifetime = model.RememberMe ? RememberMeCookieLifetime : SessionCookieLifetime;
        await _authService.SignInAsync(user, lifetime);

        // 6. Admin được ghi audit rồi chuyển vào khu vực quản trị.
        if (user.IsAdmin)
        {
            await RecordAdminSignInAuditAsync(user);
            return Redirect("/Admin");
        }

        // 7. Người dùng thường được chuyển vào thư viện cá nhân.
        return Redirect("/Set");
    }

    // Xóa phiên đăng nhập hiện tại rồi quay về trang chủ.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // 1. Xóa cookie và dữ liệu xác thực của phiên hiện tại.
        await _authService.SignOutAsync();

        // 2. Chuyển người dùng về trang chủ công khai.
        return RedirectToAction("Index", "Home");
    }

    // Chọn trang đích theo quyền Admin lưu trong claim của người dùng.
    private string GetAuthenticatedLandingPath()
    {
        // 1. Đọc claim quyền và trả về đường dẫn tương ứng.
        return User.HasClaim(AppClaimTypes.IsAdmin, "true") ? "/Admin" : "/Set";
    }

    // Ghi audit sau khi Admin đăng nhập thành công; không ghi mật khẩu hoặc thông tin nhạy cảm.
    private async Task RecordAdminSignInAuditAsync(AppUser user)
    {
        // 1. Ghi người thực hiện, kết quả và mã truy vết; không lưu dữ liệu bí mật.
        await _adminAuditService.RecordAsync(new AdminAuditEntry(
            ActorUserId: user.Id,
            ActorDisplay: user.Email,
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "AppUser",
            TargetId: user.Id,
            CorrelationId: HttpContext.TraceIdentifier));
    }

    // Thông báo chung cho tài khoản bị khóa, không lộ lý do nội bộ do Admin nhập.
    private void AddLockedAccountMessage()
    {
        // 1. Thêm thông báo chung để không làm lộ nguyên nhân khóa nội bộ.
        ModelState.AddModelError(
            string.Empty,
            "Tài khoản hiện không thể đăng nhập. Vui lòng liên hệ bộ phận hỗ trợ để được kiểm tra.");
    }
}
