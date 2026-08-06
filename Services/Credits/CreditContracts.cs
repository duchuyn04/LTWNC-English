using System.Text.Json.Serialization;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Credits;

public sealed record CreditUsageBreakdown(
    string Key,
    string Label,
    int Credits,
    int Percentage);

public sealed record CreditUsageSummary(
    int CreditsUsedThisMonth,
    int CreditsUsedPreviousMonth,
    int? ChangePercent,
    IReadOnlyList<CreditUsageBreakdown> Breakdown);

public sealed record CreditAccountSnapshot(
    int Balance,
    IReadOnlyList<CreditPackage> Packages,
    IReadOnlyList<CreditPurchase> Purchases,
    IReadOnlyList<CreditLedgerEntry> Ledger,
    CreditUsageSummary Usage);

public sealed record SePayCheckoutForm(
    int PurchaseId,
    string ActionUrl,
    IReadOnlyList<KeyValuePair<string, string>> Fields);

public sealed class SePayIpnPayload
{
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("notification_type")] public string NotificationType { get; set; } = string.Empty;
    [JsonPropertyName("order")] public SePayIpnOrder Order { get; set; } = new();
    [JsonPropertyName("transaction")] public SePayIpnTransaction Transaction { get; set; } = new();
}

public sealed class SePayIpnOrder
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("order_status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("order_currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("order_amount")] public string Amount { get; set; } = string.Empty;
    [JsonPropertyName("order_invoice_number")] public string InvoiceNumber { get; set; } = string.Empty;
}

public sealed class SePayIpnTransaction
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("payment_method")] public string PaymentMethod { get; set; } = string.Empty;
    [JsonPropertyName("transaction_id")] public string TransactionId { get; set; } = string.Empty;
    [JsonPropertyName("transaction_status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("transaction_amount")] public string Amount { get; set; } = string.Empty;
    [JsonPropertyName("transaction_currency")] public string Currency { get; set; } = string.Empty;
}

public sealed class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException() : base("Bạn đã hết tín dụng để chat. Hãy mua thêm tín dụng để tiếp tục.")
    {
    }
}

/// <summary>
/// Kết quả trừ tín dụng cho một lượt English Mission (đã commit DB).
/// WasNewlyCharged = false khi lượt này đã trừ trước đó (idempotent).
/// </summary>
public sealed record MissionTurnDebitResult(int BalanceAfter, bool WasNewlyCharged);

public interface ICreditService
{
    Task<int> GetBalanceAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditPackage>> GetActivePackagesAsync(int limit = 3, CancellationToken cancellationToken = default);
    Task EnsureCanSpendAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trừ 1 tín dụng và commit ngay (idempotent theo missionId+clientTurnId).
    /// Gọi trước khi gọi AI để tránh race hai request cùng balance=1.
    /// </summary>
    Task<MissionTurnDebitResult> PrepareMissionTurnDebitAsync(
        string userId,
        int missionId,
        string clientTurnId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn 1 tín dụng đã trừ cho lượt chưa lưu turn thành công (AI lỗi / lưu mission fail).
    /// Idempotent: không có debit thì no-op.
    /// </summary>
    Task RefundMissionTurnDebitAsync(
        string userId,
        int missionId,
        string clientTurnId,
        CancellationToken cancellationToken = default);

    Task<CreditAccountSnapshot> GetAccountAsync(string userId, CancellationToken cancellationToken = default);
    Task<CreditPurchaseStatsSnapshot> GetPurchaseStatsAsync(
        string userId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
    Task<SePayCheckoutForm> CreateCheckoutAsync(string userId, int packageId, CancellationToken cancellationToken = default);
    Task<CreditPurchase?> GetPurchaseAsync(string userId, int purchaseId, CancellationToken cancellationToken = default);
    bool VerifyIpnSecret(string? suppliedSecret);
    Task HandleIpnAsync(SePayIpnPayload payload, CancellationToken cancellationToken = default);
}
