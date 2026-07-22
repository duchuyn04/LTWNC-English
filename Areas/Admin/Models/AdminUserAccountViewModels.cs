using ltwnc.Services.AdminUsers;
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Areas.Admin.Models;

public sealed class AdminUserIndexViewModel
{
    public required IReadOnlyList<AdminUserRowViewModel> Items { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    // Tính tổng trang tối thiểu là 1 để phần phân trang không rơi vào trạng thái rỗng.
    public int TotalPages
    {
        get
        {
            if (TotalCount == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }

    public bool HasPreviousPage
    {
        get
        {
            return Page > 1;
        }
    }

    public bool HasNextPage
    {
        get
        {
            return Page < TotalPages;
        }
    }
}

public sealed class AdminUserRowViewModel
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string CreatedAtDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public bool IsAdmin { get; init; }
}

public sealed class AdminUserDetailsViewModel
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string CreatedAtDisplay { get; init; }
    public required string UpdatedAtDisplay { get; init; }
    public required string LockoutEndDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public required string ConcurrencyStamp { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsLocked { get; init; }
    public int AccessFailedCount { get; init; }
}

public sealed class AdminUserActionInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập lý do trước khi thực hiện.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu mã phiên bản tài khoản. Vui lòng tải lại trang.")]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public static class AdminUserViewModelMapper
{
    // Chuyển page service sang page view, chỉ giữ dữ liệu cần hiển thị trên danh sách.
    public static AdminUserIndexViewModel ToIndexViewModel(
        AdminUserAccountPage page,
        string? search,
        string? status,
        string? sort)
    {
        return new AdminUserIndexViewModel
        {
            Items = page.Items.Select(ToRowViewModel).ToArray(),
            Search = search,
            Status = status,
            Sort = sort,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    // Chuyển model chi tiết service sang view model chỉ đọc cho trang Admin.
    public static AdminUserDetailsViewModel ToDetailsViewModel(AdminUserAccountDetails user)
    {
        string statusLabel = BuildStatusLabel(user.IsLocked);
        string statusTone = BuildStatusTone(user.IsLocked);

        return new AdminUserDetailsViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            CreatedAtDisplay = FormatDateTime(user.CreatedAtUtc),
            UpdatedAtDisplay = FormatDateTime(user.UpdatedAtUtc),
            LockoutEndDisplay = FormatDateTime(user.LockoutEnd),
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            ConcurrencyStamp = user.ConcurrencyStamp,
            IsAdmin = user.IsAdmin,
            IsLocked = user.IsLocked,
            AccessFailedCount = user.AccessFailedCount
        };
    }

    // Chuyển một hàng tài khoản sang dữ liệu hiển thị trong bảng.
    private static AdminUserRowViewModel ToRowViewModel(AdminUserAccountRow user)
    {
        return new AdminUserRowViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            CreatedAtDisplay = FormatDateTime(user.CreatedAtUtc),
            StatusLabel = BuildStatusLabel(user.IsLocked),
            StatusTone = BuildStatusTone(user.IsLocked),
            IsAdmin = user.IsAdmin
        };
    }

    // Định dạng DateTime UTC sang giờ Việt Nam hoặc dấu gạch khi chưa có dữ liệu.
    private static string FormatDateTime(DateTime? value)
    {
        if (value == null)
        {
            return "—";
        }

        return AdminTimeZone.ToVietnamTime(value.Value).ToString("HH:mm dd/MM/yyyy");
    }

    // Định dạng DateTimeOffset UTC sang giờ Việt Nam hoặc dấu gạch khi chưa có dữ liệu.
    private static string FormatDateTime(DateTimeOffset? value)
    {
        if (value == null)
        {
            return "—";
        }

        return AdminTimeZone.ToVietnamTime(value.Value.UtcDateTime).ToString("HH:mm dd/MM/yyyy");
    }

    // Tạo nhãn trạng thái tài khoản bằng tiếng Việt.
    private static string BuildStatusLabel(bool isLocked)
    {
        if (isLocked)
        {
            return "Đang khóa";
        }

        return "Đang mở";
    }

    // Tạo tone CSS ổn định cho badge trạng thái.
    private static string BuildStatusTone(bool isLocked)
    {
        if (isLocked)
        {
            return "danger";
        }

        return "success";
    }
}
