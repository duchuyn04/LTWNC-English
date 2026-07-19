using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.AdminUsers;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/Users")]
public sealed class UsersController : Controller
{
    private readonly IAdminUserAccountService _accountService;

    // Nhận service nghiệp vụ để controller chỉ điều phối HTTP và thông báo giao diện.
    public UsersController(IAdminUserAccountService accountService)
    {
        _accountService = accountService;
    }

    // Hiển thị danh sách tài khoản với tìm kiếm, lọc, sắp xếp và phân trang server-side.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? sort,
        int page = AdminUserAccountService.DefaultPage,
        int pageSize = AdminUserAccountService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AdminUserAccountPage result = await _accountService.SearchAsync(
            new AdminUserAccountQuery(search, status, sort, page, pageSize),
            cancellationToken);
        AdminUserIndexViewModel model =
            AdminUserViewModelMapper.ToIndexViewModel(result, search, status, sort);

        return View(model);
    }

    // Hiển thị chi tiết tài khoản ở chế độ chỉ đọc, không có form sửa hồ sơ/mật khẩu/role.
    [HttpGet("{id}")]
    public async Task<IActionResult> Details(
        string id,
        CancellationToken cancellationToken = default)
    {
        AdminUserAccountDetails? user =
            await _accountService.GetDetailsAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        return View(AdminUserViewModelMapper.ToDetailsViewModel(user));
    }

    // Xử lý POST khóa tài khoản, yêu cầu antiforgery và lý do bắt buộc từ form.
    [HttpPost("{id}/Lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(
        string id,
        AdminUserActionInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminUsersError"] = FirstModelError();
            return RedirectToAction(nameof(Details), new { id });
        }

        AdminUserOperationResult result = await _accountService.LockAsync(
            BuildCommand(id, input),
            cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Details), new { id });
    }

    // Xử lý POST mở khóa, chỉ đổi trạng thái lockout và không chạm dữ liệu học tập.
    [HttpPost("{id}/Unlock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(
        string id,
        AdminUserActionInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminUsersError"] = FirstModelError();
            return RedirectToAction(nameof(Details), new { id });
        }

        AdminUserOperationResult result = await _accountService.UnlockAsync(
            BuildCommand(id, input),
            cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Details), new { id });
    }

    // Xử lý POST thu hồi phiên độc lập với khóa/mở khóa tài khoản.
    [HttpPost("{id}/RevokeSessions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(
        string id,
        AdminUserActionInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminUsersError"] = FirstModelError();
            return RedirectToAction(nameof(Details), new { id });
        }

        AdminUserOperationResult result = await _accountService.RevokeSessionsAsync(
            BuildCommand(id, input),
            cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Details), new { id });
    }

    // Dựng lệnh nghiệp vụ từ người đang đăng nhập và dữ liệu form.
    private AdminUserAccountCommand BuildCommand(
        string targetUserId,
        AdminUserActionInputModel input)
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AdminUserAccountCommand(
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            TargetUserId: targetUserId,
            Reason: input.Reason,
            ConcurrencyStamp: input.ConcurrencyStamp,
            CorrelationId: HttpContext.TraceIdentifier);
    }

    // Lưu thông báo thành công/thất bại qua TempData để hiện sau redirect.
    private void StoreOperationMessage(AdminUserOperationResult result)
    {
        if (result.Succeeded)
        {
            TempData["AdminUsersSuccess"] = result.Message;
            return;
        }

        TempData["AdminUsersError"] = result.Message;
    }

    // Lấy lỗi ModelState đầu tiên để hiển thị ngắn gọn trên trang chi tiết.
    private string FirstModelError()
    {
        foreach (var state in ModelState.Values)
        {
            foreach (var error in state.Errors)
            {
                if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
                {
                    return error.ErrorMessage;
                }
            }
        }

        return "Dữ liệu thao tác không hợp lệ.";
    }
}
