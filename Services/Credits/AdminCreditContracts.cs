using ltwnc.Models.Entities;
using ltwnc.Services.Audit;

namespace ltwnc.Services.Credits;

public interface IAdminCreditService
{
    Task<AdminCreditOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<CreditPackage?> GetPackageAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminCreditOperationResult> SavePackageAsync(AdminCreditPackageCommand command, CancellationToken cancellationToken = default);
    Task<AdminCreditOperationResult> SetPackageArchivedAsync(AdminCreditPackageLifecycleCommand command, CancellationToken cancellationToken = default);
    Task<AdminCreditUser?> FindUserAsync(string exactSearch, CancellationToken cancellationToken = default);
    Task<AdminCreditOperationResult> AdjustBalanceAsync(AdminCreditAdjustmentCommand command, CancellationToken cancellationToken = default);
}

public sealed record AdminCreditOverview(
    IReadOnlyList<CreditPackage> Packages,
    IReadOnlyList<AdminCreditPurchase> RecentPurchases);

public sealed record AdminCreditPurchase(
    int Id,
    string InvoiceNumber,
    string UserName,
    string Email,
    string PackageName,
    long PriceVnd,
    int Credits,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc);

public sealed record AdminCreditUser(
    string Id,
    string UserName,
    string Email,
    int Balance,
    int CreditVersion,
    IReadOnlyList<CreditLedgerEntry> RecentLedger);

public sealed record AdminCreditPackageCommand(
    int? Id,
    int Version,
    string Name,
    string? Description,
    long PriceVnd,
    int Credits,
    int DisplayOrder,
    bool IsActive,
    string Reason,
    AdminActorContext Actor);

public sealed record AdminCreditPackageLifecycleCommand(
    int Id,
    int Version,
    bool Archive,
    string Reason,
    AdminActorContext Actor);

public sealed record AdminCreditAdjustmentCommand(
    string UserId,
    int CreditVersion,
    int Amount,
    string Reason,
    AdminActorContext Actor);

public sealed record AdminCreditOperationResult(bool Succeeded, string Message)
{
    public static AdminCreditOperationResult Success(string message) => new(true, message);
    public static AdminCreditOperationResult Failure(string message) => new(false, message);
}
