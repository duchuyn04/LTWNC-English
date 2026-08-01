using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public static class CreditLedgerTypes
{
    public const string WelcomeBonus = "WelcomeBonus";
    public const string Purchase = "Purchase";
    public const string MissionTurn = "MissionTurn";
    public const string AdminAdjustment = "AdminAdjustment";
}

public static class CreditPurchaseStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Failed = "Failed";
    public const string Voided = "Voided";
}

public sealed class CreditPackage
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public long PriceVnd { get; set; }
    public int Credits { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CreditPurchase
{
    public int Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public int? CreditPackageId { get; set; }
    [MaxLength(64)] public string InvoiceNumber { get; set; } = string.Empty;
    [MaxLength(120)] public string PackageName { get; set; } = string.Empty;
    public long PriceVnd { get; set; }
    public int Credits { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "VND";
    [MaxLength(32)] public string Status { get; set; } = CreditPurchaseStatuses.Pending;
    [MaxLength(100)] public string? SePayOrderId { get; set; }
    [MaxLength(100)] public string? SePayTransactionId { get; set; }
    [MaxLength(40)] public string? PaymentMethod { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public int Version { get; set; }
    public CreditPackage? Package { get; set; }
}

public sealed class CreditLedgerEntry
{
    public long Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    [MaxLength(40)] public string Type { get; set; } = string.Empty;
    [MaxLength(60)] public string SourceType { get; set; } = string.Empty;
    [MaxLength(100)] public string SourceId { get; set; } = string.Empty;
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(450)] public string? AdminActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
