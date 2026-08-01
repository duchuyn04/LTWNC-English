using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Credits;

public sealed class AdminCreditService : IAdminCreditService
{
    private const int RecentPurchaseLimit = 50;
    private const int RecentLedgerLimit = 30;
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _audit;
    private readonly TimeProvider _timeProvider;

    public AdminCreditService(AppDbContext db, IAdminAuditService audit, TimeProvider timeProvider)
    {
        _db = db;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    public async Task<AdminCreditOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        List<CreditPackage> packages = await _db.CreditPackages
            .AsNoTracking()
            .OrderBy(package => package.IsArchived)
            .ThenBy(package => package.DisplayOrder)
            .ThenBy(package => package.Id)
            .ToListAsync(cancellationToken);

        List<AdminCreditPurchase> purchases = await (
            from purchase in _db.CreditPurchases.AsNoTracking()
            join user in _db.AppUsers.AsNoTracking() on purchase.UserId equals user.Id
            orderby purchase.CreatedAtUtc descending, purchase.Id descending
            select new AdminCreditPurchase(
                purchase.Id,
                purchase.InvoiceNumber,
                user.UserName,
                user.Email,
                purchase.PackageName,
                purchase.PriceVnd,
                purchase.Credits,
                purchase.Status,
                purchase.CreatedAtUtc,
                purchase.PaidAtUtc))
            .Take(RecentPurchaseLimit)
            .ToListAsync(cancellationToken);

        return new AdminCreditOverview(packages, purchases);
    }

    public Task<CreditPackage?> GetPackageAsync(int id, CancellationToken cancellationToken = default)
    {
        return _db.CreditPackages.AsNoTracking()
            .SingleOrDefaultAsync(package => package.Id == id, cancellationToken);
    }

    public async Task<AdminCreditOperationResult> SavePackageAsync(
        AdminCreditPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        string name = command.Name.Trim();
        string reason = command.Reason.Trim();
        if (name.Length == 0 || name.Length > 120)
            return AdminCreditOperationResult.Failure("Tên gói phải có từ 1 đến 120 ký tự.");
        if (command.Description?.Trim().Length > 500)
            return AdminCreditOperationResult.Failure("Mô tả không được vượt quá 500 ký tự.");
        if (command.PriceVnd <= 0 || command.Credits <= 0)
            return AdminCreditOperationResult.Failure("Giá và số tín dụng phải lớn hơn 0.");
        if (reason.Length == 0 || reason.Length > 500)
            return AdminCreditOperationResult.Failure("Lý do là bắt buộc và không quá 500 ký tự.");

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        CreditPackage package;
        string action;
        if (command.Id.HasValue)
        {
            CreditPackage? existing = await _db.CreditPackages.SingleOrDefaultAsync(
                candidate => candidate.Id == command.Id.Value,
                cancellationToken);
            if (existing == null)
                return AdminCreditOperationResult.Failure("Gói tín dụng không tồn tại.");
            if (existing.Version != command.Version)
                return Conflict();

            package = existing;
            package.Version++;
            package.UpdatedAtUtc = now;
            action = AdminAuditActions.CreditPackagesUpdate;
        }
        else
        {
            package = new CreditPackage
            {
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.CreditPackages.Add(package);
            action = AdminAuditActions.CreditPackagesCreate;
        }

        package.Name = name;
        package.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        package.PriceVnd = command.PriceVnd;
        package.Credits = command.Credits;
        package.DisplayOrder = command.DisplayOrder;
        package.IsActive = command.IsActive;

        _audit.Enqueue(BuildAudit(command.Actor, action, "CreditPackage",
            command.Id?.ToString(), reason));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        return AdminCreditOperationResult.Success($"Đã lưu gói {package.Name}.");
    }

    public async Task<AdminCreditOperationResult> SetPackageArchivedAsync(
        AdminCreditPackageLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        string reason = command.Reason.Trim();
        if (reason.Length == 0 || reason.Length > 500)
            return AdminCreditOperationResult.Failure("Lý do là bắt buộc và không quá 500 ký tự.");

        CreditPackage? package = await _db.CreditPackages
            .SingleOrDefaultAsync(candidate => candidate.Id == command.Id, cancellationToken);
        if (package == null)
            return AdminCreditOperationResult.Failure("Gói tín dụng không tồn tại.");
        if (package.Version != command.Version)
            return Conflict();
        if (package.IsArchived == command.Archive)
            return AdminCreditOperationResult.Failure(command.Archive ? "Gói đã được lưu trữ." : "Gói chưa bị lưu trữ.");

        package.IsArchived = command.Archive;
        package.Version++;
        package.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _audit.Enqueue(BuildAudit(
            command.Actor,
            command.Archive ? AdminAuditActions.CreditPackagesArchive : AdminAuditActions.CreditPackagesUnarchive,
            "CreditPackage",
            package.Id.ToString(),
            reason));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        return AdminCreditOperationResult.Success(command.Archive
            ? $"Đã lưu trữ gói {package.Name}."
            : $"Đã khôi phục gói {package.Name}.");
    }

    public async Task<AdminCreditUser?> FindUserAsync(
        string exactSearch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exactSearch))
            return null;

        string normalized = exactSearch.Trim().ToUpperInvariant();
        var user = await _db.AppUsers.AsNoTracking()
            .Where(candidate => candidate.NormalizedEmail == normalized
                || candidate.NormalizedUserName == normalized)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.UserName,
                candidate.Email,
                candidate.CreditBalance,
                candidate.CreditVersion
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user == null)
            return null;

        List<CreditLedgerEntry> ledger = await _db.CreditLedgerEntries.AsNoTracking()
            .Where(entry => entry.UserId == user.Id)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Take(RecentLedgerLimit)
            .ToListAsync(cancellationToken);

        return new AdminCreditUser(
            user.Id,
            user.UserName,
            user.Email,
            user.CreditBalance,
            user.CreditVersion,
            ledger);
    }

    public async Task<AdminCreditOperationResult> AdjustBalanceAsync(
        AdminCreditAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        string reason = command.Reason.Trim();
        if (command.Amount == 0)
            return AdminCreditOperationResult.Failure("Số tín dụng điều chỉnh phải khác 0.");
        if (reason.Length == 0 || reason.Length > 500)
            return AdminCreditOperationResult.Failure("Lý do là bắt buộc và không quá 500 ký tự.");

        AppUser? user = await _db.AppUsers
            .SingleOrDefaultAsync(candidate => candidate.Id == command.UserId, cancellationToken);
        if (user == null)
            return AdminCreditOperationResult.Failure("Không tìm thấy người dùng.");
        if (user.CreditVersion != command.CreditVersion)
            return AdminCreditOperationResult.Failure("Số dư đã thay đổi. Vui lòng tìm lại người dùng.");

        long nextBalance = (long)user.CreditBalance + command.Amount;
        if (nextBalance < 0)
            return AdminCreditOperationResult.Failure("Không thể trừ quá số tín dụng hiện có.");
        if (nextBalance > int.MaxValue)
            return AdminCreditOperationResult.Failure("Số dư sau điều chỉnh vượt giới hạn.");

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        string sourceId = Guid.NewGuid().ToString("N");
        user.CreditBalance = (int)nextBalance;
        user.CreditVersion++;
        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            UserId = user.Id,
            Amount = command.Amount,
            BalanceAfter = user.CreditBalance,
            Type = CreditLedgerTypes.AdminAdjustment,
            SourceType = "AdminAdjustment",
            SourceId = sourceId,
            Description = reason,
            AdminActorUserId = command.Actor.UserId,
            CreatedAtUtc = now
        });
        _audit.Enqueue(BuildAudit(
            command.Actor,
            AdminAuditActions.CreditBalanceAdjust,
            "AppUser",
            user.Id,
            reason));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdminCreditOperationResult.Failure("Số dư đã thay đổi. Vui lòng tìm lại người dùng.");
        }

        return AdminCreditOperationResult.Success(
            $"Đã điều chỉnh {command.Amount:+#;-#;0} tín dụng cho {user.UserName}. Số dư mới: {user.CreditBalance}.");
    }

    private static AdminAuditEntry BuildAudit(
        AdminActorContext actor,
        string action,
        string targetType,
        string? targetId,
        string reason)
    {
        return new AdminAuditEntry(
            actor.UserId,
            actor.Display,
            action,
            AdminAuditOutcome.Success,
            targetType,
            targetId,
            reason,
            actor.CorrelationId);
    }

    private static AdminCreditOperationResult Conflict()
    {
        return AdminCreditOperationResult.Failure(
            "Gói tín dụng đã thay đổi. Vui lòng tải lại trang trước khi thao tác.");
    }
}
