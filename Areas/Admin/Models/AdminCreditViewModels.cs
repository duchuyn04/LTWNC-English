using System.ComponentModel.DataAnnotations;

namespace ltwnc.Areas.Admin.Models;

public sealed class AdminCreditIndexViewModel
{
    public required IReadOnlyList<AdminCreditPackageRowViewModel> Packages { get; init; }
    public required IReadOnlyList<AdminCreditPurchaseRowViewModel> Purchases { get; init; }
}

public sealed class AdminCreditPackageRowViewModel
{
    public int Id { get; init; }
    public int Version { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public long PriceVnd { get; init; }
    public int Credits { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public bool IsArchived { get; init; }
}

public sealed class AdminCreditPurchaseRowViewModel
{
    public int Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string UserDisplay { get; init; }
    public required string PackageName { get; init; }
    public long PriceVnd { get; init; }
    public int Credits { get; init; }
    public required string Status { get; init; }
    public required string CreatedAtDisplay { get; init; }
    public required string PaidAtDisplay { get; init; }
}

public sealed class AdminCreditPackageEditViewModel
{
    public int? Id { get; set; }
    public int Version { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên gói.")]
    [StringLength(120, ErrorMessage = "Tên gói không được vượt quá 120 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? Description { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Giá phải lớn hơn 0.")]
    public long PriceVnd { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số tín dụng phải lớn hơn 0.")]
    public int Credits { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminCreditPackageLifecycleViewModel
{
    public int Version { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminCreditUserViewModel
{
    public string Search { get; set; } = string.Empty;
    public bool SearchAttempted { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public int Balance { get; init; }
    public int CreditVersion { get; init; }
    public required IReadOnlyList<AdminCreditLedgerRowViewModel> Ledger { get; init; }
}

public sealed class AdminCreditLedgerRowViewModel
{
    public long Id { get; init; }
    public int Amount { get; init; }
    public int BalanceAfter { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required string CreatedAtDisplay { get; init; }
}

public sealed class AdminCreditAdjustmentViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    public int CreditVersion { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số tín dụng phải lớn hơn 0.")]
    public int Amount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại điều chỉnh.")]
    [RegularExpression("Add|Subtract", ErrorMessage = "Loại điều chỉnh không hợp lệ.")]
    public string Operation { get; set; } = "Add";

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string Search { get; set; } = string.Empty;
}
