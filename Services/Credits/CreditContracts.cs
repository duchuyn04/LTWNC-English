using System.Text.Json.Serialization;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Credits;

public sealed record CreditAccountSnapshot(
    int Balance,
    IReadOnlyList<CreditPackage> Packages,
    IReadOnlyList<CreditPurchase> Purchases,
    IReadOnlyList<CreditLedgerEntry> Ledger);

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

public interface ICreditService
{
    Task<int> GetBalanceAsync(string userId, CancellationToken cancellationToken = default);
    Task EnsureCanSpendAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> PrepareMissionTurnDebitAsync(string userId, int missionId, string clientTurnId, CancellationToken cancellationToken = default);
    Task<CreditAccountSnapshot> GetAccountAsync(string userId, CancellationToken cancellationToken = default);
    Task<SePayCheckoutForm> CreateCheckoutAsync(string userId, int packageId, CancellationToken cancellationToken = default);
    Task<CreditPurchase?> GetPurchaseAsync(string userId, int purchaseId, CancellationToken cancellationToken = default);
    bool VerifyIpnSecret(string? suppliedSecret);
    Task HandleIpnAsync(SePayIpnPayload payload, CancellationToken cancellationToken = default);
}
