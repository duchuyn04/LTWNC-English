using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.Credits;

public sealed class CreditService : ICreditService
{
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(30);
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public CreditService(AppDbContext db, IConfiguration configuration, TimeProvider timeProvider)
    {
        _db = db;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<int> GetBalanceAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.CreditBalance)
            .SingleAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditPackage>> GetActivePackagesAsync(
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        int safeLimit = Math.Clamp(limit, 1, 20);
        return await _db.CreditPackages
            .AsNoTracking()
            .Where(package => package.IsActive && !package.IsArchived)
            .OrderBy(package => package.DisplayOrder)
            .ThenBy(package => package.PriceVnd)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureCanSpendAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await GetBalanceAsync(userId, cancellationToken) < 1)
            throw new InsufficientCreditsException();
    }

    public async Task<int> PrepareMissionTurnDebitAsync(
        string userId,
        int missionId,
        string clientTurnId,
        CancellationToken cancellationToken = default)
    {
        string sourceId = $"{missionId}:{clientTurnId}";
        CreditLedgerEntry? existing = await _db.CreditLedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.SourceType == "EnglishMissionTurn" && entry.SourceId == sourceId, cancellationToken);
        if (existing != null) return existing.BalanceAfter;

        AppUser user = await _db.AppUsers.SingleAsync(item => item.Id == userId, cancellationToken);
        if (user.CreditBalance < 1) throw new InsufficientCreditsException();
        user.CreditBalance--;
        user.CreditVersion++;
        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            UserId = userId,
            Amount = -1,
            BalanceAfter = user.CreditBalance,
            Type = CreditLedgerTypes.MissionTurn,
            SourceType = "EnglishMissionTurn",
            SourceId = sourceId,
            Description = "Phản hồi English Mission",
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        });
        return user.CreditBalance;
    }

    public async Task<CreditAccountSnapshot> GetAccountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        await ExpirePendingPurchasesAsync(userId, now, cancellationToken);

        int balance = await GetBalanceAsync(userId, cancellationToken);
        List<CreditPackage> packages = await _db.CreditPackages
            .AsNoTracking()
            .Where(package => package.IsActive && !package.IsArchived)
            .OrderBy(package => package.DisplayOrder)
            .ThenBy(package => package.PriceVnd)
            .ToListAsync(cancellationToken);
        List<CreditPurchase> purchases = await _db.CreditPurchases
            .AsNoTracking()
            .Where(purchase => purchase.UserId == userId)
            .OrderByDescending(purchase => purchase.CreatedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);
        List<CreditLedgerEntry> ledger = await _db.CreditLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new CreditAccountSnapshot(balance, packages, purchases, ledger);
    }

    public async Task<SePayCheckoutForm> CreateCheckoutAsync(
        string userId,
        int packageId,
        CancellationToken cancellationToken = default)
    {
        CreditPackage package = await _db.CreditPackages
            .SingleOrDefaultAsync(item => item.Id == packageId && item.IsActive && !item.IsArchived, cancellationToken)
            ?? throw new KeyNotFoundException("Gói tín dụng không còn khả dụng.");

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        await ExpirePendingPurchasesAsync(userId, now, cancellationToken);
        CreditPurchase? pending = await _db.CreditPurchases
            .Where(purchase => purchase.UserId == userId
                && purchase.Status == CreditPurchaseStatuses.Pending
                && purchase.ExpiresAtUtc > now)
            .OrderByDescending(purchase => purchase.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (pending != null && pending.CreditPackageId != packageId)
        {
            pending.Status = CreditPurchaseStatuses.Cancelled;
            pending.CancelledAtUtc = now;
            pending.Version++;
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return await CreateCheckoutAsync(userId, packageId, cancellationToken);
            }
            pending = null;
        }

        if (pending == null)
        {
            pending = new CreditPurchase
            {
                UserId = userId,
                CreditPackageId = package.Id,
                InvoiceNumber = $"CRD-{Guid.NewGuid():N}".ToUpperInvariant(),
                PackageName = package.Name,
                PriceVnd = package.PriceVnd,
                Credits = package.Credits,
                Status = CreditPurchaseStatuses.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(PendingLifetime)
            };
            _db.CreditPurchases.Add(pending);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                CreditPurchase? winner = await _db.CreditPurchases.AsNoTracking()
                    .SingleOrDefaultAsync(purchase => purchase.UserId == userId
                        && purchase.Status == CreditPurchaseStatuses.Pending,
                        cancellationToken);
                if (winner == null || winner.CreditPackageId != packageId)
                    throw new InvalidOperationException("Một yêu cầu thanh toán khác đang được xử lý. Vui lòng thử lại.");
                pending = winner;
            }
        }

        return BuildCheckoutForm(pending);
    }

    public Task<CreditPurchase?> GetPurchaseAsync(
        string userId,
        int purchaseId,
        CancellationToken cancellationToken = default)
    {
        return _db.CreditPurchases.AsNoTracking()
            .SingleOrDefaultAsync(purchase => purchase.Id == purchaseId && purchase.UserId == userId, cancellationToken);
    }

    public bool VerifyIpnSecret(string? suppliedSecret)
    {
        string configured = _configuration["SePay:IpnSecret"] ?? string.Empty;
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(suppliedSecret)) return false;
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedSecret));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public async Task HandleIpnAsync(SePayIpnPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.NotificationType == "TRANSACTION_VOID")
        {
            await MarkVoidedAsync(payload, cancellationToken);
            return;
        }
        if (payload.NotificationType != "ORDER_PAID"
            || payload.Order.Status != "CAPTURED"
            || payload.Transaction.Status != "APPROVED")
        {
            throw new ArgumentException("Trạng thái IPN SePay không được hỗ trợ.");
        }

        if (string.IsNullOrWhiteSpace(payload.Order.InvoiceNumber)
            || string.IsNullOrWhiteSpace(payload.Order.Id)
            || string.IsNullOrWhiteSpace(payload.Transaction.TransactionId)
            || !decimal.TryParse(payload.Order.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal orderAmount)
            || !decimal.TryParse(payload.Transaction.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal transactionAmount))
        {
            throw new ArgumentException("Số tiền IPN SePay không hợp lệ.");
        }

        await using IDbContextTransaction? transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            CreditPurchase purchase = await _db.CreditPurchases
                .SingleOrDefaultAsync(item => item.InvoiceNumber == payload.Order.InvoiceNumber, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy đơn tín dụng.");
            if (!string.Equals(payload.Order.Currency, "VND", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(payload.Transaction.Currency, "VND", StringComparison.OrdinalIgnoreCase)
                || orderAmount != purchase.PriceVnd
                || transactionAmount != purchase.PriceVnd)
            {
                throw new ArgumentException("Thông tin tiền tệ IPN không khớp đơn hàng.");
            }
            if (purchase.SePayOrderId != null && purchase.SePayOrderId != payload.Order.Id)
                throw new ArgumentException("Mã đơn SePay không khớp giao dịch đã xử lý.");
            if (purchase.Status == CreditPurchaseStatuses.Paid
                || purchase.Status == CreditPurchaseStatuses.Voided && purchase.PaidAtUtc.HasValue)
            {
                if (purchase.SePayTransactionId != payload.Transaction.TransactionId)
                    throw new ArgumentException("Đơn đã được thanh toán bằng giao dịch khác.");
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return;
            }
            bool transactionUsed = await _db.CreditPurchases.AnyAsync(
                item => item.SePayTransactionId == payload.Transaction.TransactionId && item.Id != purchase.Id,
                cancellationToken);
            if (transactionUsed) throw new ArgumentException("Giao dịch SePay đã được sử dụng.");

            AppUser user = await _db.AppUsers.SingleAsync(item => item.Id == purchase.UserId, cancellationToken);
            user.CreditBalance = checked(user.CreditBalance + purchase.Credits);
            user.CreditVersion++;
            if (purchase.Status != CreditPurchaseStatuses.Voided)
                purchase.Status = CreditPurchaseStatuses.Paid;
            purchase.PaidAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            purchase.SePayOrderId = Limit(payload.Order.Id, 100);
            purchase.SePayTransactionId = Limit(payload.Transaction.TransactionId, 100);
            purchase.PaymentMethod = Limit(payload.Transaction.PaymentMethod, 40);
            purchase.Version++;
            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                UserId = user.Id,
                Amount = purchase.Credits,
                BalanceAfter = user.CreditBalance,
                Type = CreditLedgerTypes.Purchase,
                SourceType = "CreditPurchase",
                SourceId = purchase.Id.ToString(CultureInfo.InvariantCulture),
                Description = $"Mua gói {purchase.PackageName}",
                CreatedAtUtc = purchase.PaidAtUtc.Value
            });
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private SePayCheckoutForm BuildCheckoutForm(CreditPurchase purchase)
    {
        string merchant = RequiredSetting("SePay:MerchantId");
        string secret = RequiredSetting("SePay:SecretKey");
        string baseUrl = RequiredSetting("SePay:PublicBaseUrl").TrimEnd('/');
        string environment = _configuration["SePay:Environment"] ?? "Sandbox";
        string action = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
            ? "https://pay.sepay.vn/v1/checkout/init"
            : "https://pay-sandbox.sepay.vn/v1/checkout/init";
        var fields = new List<KeyValuePair<string, string>>
        {
            new("order_amount", purchase.PriceVnd.ToString(CultureInfo.InvariantCulture)),
            new("merchant", merchant),
            new("currency", "VND"),
            new("operation", "PURCHASE"),
            new("order_description", $"Mua {purchase.Credits} tin dung LTWNC"),
            new("order_invoice_number", purchase.InvoiceNumber),
            new("customer_id", purchase.UserId),
            new("payment_method", "BANK_TRANSFER"),
            new("success_url", $"{baseUrl}/Credits/Payment/{purchase.Id}?result=success"),
            new("error_url", $"{baseUrl}/Credits/Payment/{purchase.Id}?result=error"),
            new("cancel_url", $"{baseUrl}/Credits/Payment/{purchase.Id}?result=cancel")
        };
        string signed = string.Join(',', fields.Select(field => $"{field.Key}={field.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        fields.Add(new("signature", Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)))));
        return new SePayCheckoutForm(purchase.Id, action, fields);
    }

    private async Task ExpirePendingPurchasesAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        if (_db.Database.IsRelational())
        {
            await _db.CreditPurchases
                .Where(purchase => purchase.UserId == userId
                    && purchase.Status == CreditPurchaseStatuses.Pending
                    && purchase.ExpiresAtUtc <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(purchase => purchase.Status, CreditPurchaseStatuses.Expired)
                    .SetProperty(purchase => purchase.Version, purchase => purchase.Version + 1),
                    cancellationToken);
            return;
        }

        List<CreditPurchase> expired = await _db.CreditPurchases
            .Where(purchase => purchase.UserId == userId
                && purchase.Status == CreditPurchaseStatuses.Pending
                && purchase.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (CreditPurchase purchase in expired)
        {
            purchase.Status = CreditPurchaseStatuses.Expired;
            purchase.Version++;
        }
        if (expired.Count > 0) await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkVoidedAsync(SePayIpnPayload payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Order.InvoiceNumber)
            || string.IsNullOrWhiteSpace(payload.Order.Id)
            || string.IsNullOrWhiteSpace(payload.Transaction.TransactionId))
            throw new ArgumentException("Thông tin giao dịch void không hợp lệ.");

        CreditPurchase purchase = await _db.CreditPurchases
            .SingleOrDefaultAsync(item => item.InvoiceNumber == payload.Order.InvoiceNumber, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn tín dụng.");
        if (purchase.SePayTransactionId != null
            && purchase.SePayTransactionId != payload.Transaction.TransactionId)
            throw new ArgumentException("Giao dịch void không khớp đơn đã thanh toán.");
        if (purchase.SePayOrderId != null && purchase.SePayOrderId != payload.Order.Id)
            throw new ArgumentException("Mã đơn void không khớp đơn đã thanh toán.");
        if (purchase.Status == CreditPurchaseStatuses.Voided) return;

        purchase.Status = CreditPurchaseStatuses.Voided;
        purchase.VoidedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        purchase.SePayOrderId ??= Limit(payload.Order.Id, 100);
        purchase.SePayTransactionId ??= Limit(payload.Transaction.TransactionId, 100);
        purchase.PaymentMethod ??= Limit(payload.Transaction.PaymentMethod, 40);
        purchase.Version++;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private string RequiredSetting(string key)
    {
        string? value = _configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Thiếu cấu hình {key}.");
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
