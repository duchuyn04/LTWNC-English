using System.Security.Cryptography;
using System.Text;
using ltwnc.Areas.Admin;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Credits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Credits;

public sealed class CreditServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 1, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCheckoutAsync_SnapshotsPackageAndReusesMatchingPendingPurchase()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context);
        CreditPackage package = await SeedPackageAsync(context, "Starter", 25_000, 30);
        CreditService service = CreateService(context);

        SePayCheckoutForm first = await service.CreateCheckoutAsync("user-1", package.Id);
        package.Name = "Changed";
        package.PriceVnd = 99_000;
        package.Credits = 120;
        await context.SaveChangesAsync();

        SePayCheckoutForm retry = await service.CreateCheckoutAsync("user-1", package.Id);

        CreditPurchase purchase = await context.CreditPurchases.SingleAsync();
        Assert.Equal(first.PurchaseId, retry.PurchaseId);
        Assert.Equal("Starter", purchase.PackageName);
        Assert.Equal(25_000, purchase.PriceVnd);
        Assert.Equal(30, purchase.Credits);
        Assert.Equal("25000", Field(retry, "order_amount"));
        Assert.Equal("Mua 30 tin dung LTWNC", Field(retry, "order_description"));
        Assert.Equal(FixedNow.UtcDateTime, purchase.CreatedAtUtc);
        Assert.Equal(FixedNow.AddMinutes(30).UtcDateTime, purchase.ExpiresAtUtc);
    }

    [Fact]
    public async Task CreateCheckoutAsync_DifferentPackageCancelsPendingPurchaseAndCreatesAnother()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context);
        CreditPackage firstPackage = await SeedPackageAsync(context, "Starter", 25_000, 30);
        CreditPackage secondPackage = await SeedPackageAsync(context, "Plus", 50_000, 70);
        CreditService service = CreateService(context);

        SePayCheckoutForm first = await service.CreateCheckoutAsync("user-1", firstPackage.Id);
        SePayCheckoutForm second = await service.CreateCheckoutAsync("user-1", secondPackage.Id);

        List<CreditPurchase> purchases = await context.CreditPurchases.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, purchases.Count);
        Assert.Equal(first.PurchaseId, purchases[0].Id);
        Assert.Equal(CreditPurchaseStatuses.Cancelled, purchases[0].Status);
        Assert.Equal(FixedNow.UtcDateTime, purchases[0].CancelledAtUtc);
        Assert.Equal(second.PurchaseId, purchases[1].Id);
        Assert.Equal(CreditPurchaseStatuses.Pending, purchases[1].Status);
        Assert.Equal(secondPackage.Id, purchases[1].CreditPackageId);
    }

    [Fact]
    public async Task CreateCheckoutAsync_BuildsExpectedSePayFieldsAndHmac()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context);
        CreditPackage package = await SeedPackageAsync(context, "Starter", 25_000, 30);
        CreditService service = CreateService(context);

        SePayCheckoutForm form = await service.CreateCheckoutAsync("user-1", package.Id);
        CreditPurchase purchase = await context.CreditPurchases.SingleAsync();

        Assert.Equal("https://pay-sandbox.sepay.vn/v1/checkout/init", form.ActionUrl);
        Assert.Equal(
            ["order_amount", "merchant", "currency", "operation", "order_description",
                "order_invoice_number", "customer_id", "payment_method", "success_url",
                "error_url", "cancel_url", "signature"],
            form.Fields.Select(field => field.Key));
        Assert.Equal("merchant-123", Field(form, "merchant"));
        Assert.Equal("VND", Field(form, "currency"));
        Assert.Equal("PURCHASE", Field(form, "operation"));
        Assert.Equal(purchase.InvoiceNumber, Field(form, "order_invoice_number"));
        Assert.Equal("user-1", Field(form, "customer_id"));
        Assert.Equal("BANK_TRANSFER", Field(form, "payment_method"));
        Assert.Equal($"https://example.test/Credits/Payment/{purchase.Id}?result=success", Field(form, "success_url"));
        Assert.Equal($"https://example.test/Credits/Payment/{purchase.Id}?result=error", Field(form, "error_url"));
        Assert.Equal($"https://example.test/Credits/Payment/{purchase.Id}?result=cancel", Field(form, "cancel_url"));

        string signed = string.Join(',', form.Fields
            .Where(field => field.Key != "signature")
            .Select(field => $"{field.Key}={field.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("checkout-secret"));
        string expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)));
        Assert.Equal(expected, Field(form, "signature"));
    }

    [Fact]
    public void VerifyIpnSecret_RejectsMissingAndIncorrectSecrets()
    {
        using AppDbContext context = CreateContext();
        CreditService service = CreateService(context);

        Assert.False(service.VerifyIpnSecret(null));
        Assert.False(service.VerifyIpnSecret(string.Empty));
        Assert.False(service.VerifyIpnSecret("wrong-secret"));
        Assert.True(service.VerifyIpnSecret("ipn-secret"));
    }

    [Fact]
    public async Task HandleIpnAsync_RejectsAmountMismatchWithoutFulfillingPurchase()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25001.00", "25000.00");

        await Assert.ThrowsAsync<ArgumentException>(() => service.HandleIpnAsync(payload));

        Assert.Equal(CreditPurchaseStatuses.Pending, purchase.Status);
        Assert.Equal(4, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Empty(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HandleIpnAsync_RejectsFractionalAmountInsteadOfTruncatingIt()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.99", "25000.99");

        await Assert.ThrowsAsync<ArgumentException>(() => service.HandleIpnAsync(payload));

        Assert.Equal(CreditPurchaseStatuses.Pending, purchase.Status);
        Assert.Equal(4, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Empty(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HandleIpnAsync_ExactAmountFulfillsOnlyOnceForRepeatedNotification()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.00", "25000.00");

        await service.HandleIpnAsync(payload);
        await service.HandleIpnAsync(payload);

        AppUser user = await context.AppUsers.SingleAsync();
        CreditLedgerEntry entry = await context.CreditLedgerEntries.SingleAsync();
        Assert.Equal(34, user.CreditBalance);
        Assert.Equal(1, user.CreditVersion);
        Assert.Equal(CreditPurchaseStatuses.Paid, purchase.Status);
        Assert.Equal(FixedNow.UtcDateTime, purchase.PaidAtUtc);
        Assert.Equal("transaction-1", purchase.SePayTransactionId);
        Assert.Equal(30, entry.Amount);
        Assert.Equal(34, entry.BalanceAfter);
        Assert.Equal(CreditLedgerTypes.Purchase, entry.Type);
        Assert.Equal(purchase.Id.ToString(), entry.SourceId);
    }

    [Fact]
    public async Task HandleIpnAsync_ReplayStillRequiresMatchingAmountAndOrder()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.00", "25000.00");
        await service.HandleIpnAsync(payload);

        payload.Order.Amount = "25001.00";
        await Assert.ThrowsAsync<ArgumentException>(() => service.HandleIpnAsync(payload));
        payload.Order.Amount = "25000.00";
        payload.Order.Id = "different-order";
        await Assert.ThrowsAsync<ArgumentException>(() => service.HandleIpnAsync(payload));

        Assert.Equal(34, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Single(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HandleIpnAsync_VoidRecordsStatusWithoutAutomaticallyReclaimingCredits()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.00", "25000.00");
        await service.HandleIpnAsync(payload);

        payload.NotificationType = "TRANSACTION_VOID";
        await service.HandleIpnAsync(payload);

        Assert.Equal(CreditPurchaseStatuses.Voided, purchase.Status);
        Assert.Equal(FixedNow.UtcDateTime, purchase.VoidedAtUtc);
        Assert.Equal(34, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Single(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HandleIpnAsync_PaidAfterVoidStillFulfillsOnceAndKeepsReviewStatus()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.00", "25000.00");

        payload.NotificationType = "TRANSACTION_VOID";
        await service.HandleIpnAsync(payload);
        payload.NotificationType = "ORDER_PAID";
        await service.HandleIpnAsync(payload);
        await service.HandleIpnAsync(payload);

        Assert.Equal(CreditPurchaseStatuses.Voided, purchase.Status);
        Assert.Equal(FixedNow.UtcDateTime, purchase.PaidAtUtc);
        Assert.Equal(34, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Single(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HandleIpnAsync_RejectsVoidForDifferentTransaction()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 4);
        CreditPurchase purchase = await SeedPurchaseAsync(context, priceVnd: 25_000, credits: 30);
        CreditService service = CreateService(context);
        SePayIpnPayload payload = CreatePaidPayload(purchase, "25000.00", "25000.00");
        await service.HandleIpnAsync(payload);

        payload.NotificationType = "TRANSACTION_VOID";
        payload.Transaction.TransactionId = "different-transaction";

        await Assert.ThrowsAsync<ArgumentException>(() => service.HandleIpnAsync(payload));
        Assert.Equal(CreditPurchaseStatuses.Paid, purchase.Status);
        Assert.Equal(34, (await context.AppUsers.SingleAsync()).CreditBalance);
    }

    [Fact]
    public async Task PrepareMissionTurnDebitAsync_InsufficientBalanceDoesNotCreateDebit()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 0);
        CreditService service = CreateService(context);

        await Assert.ThrowsAsync<InsufficientCreditsException>(() =>
            service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-1"));

        Assert.Equal(0, (await context.AppUsers.SingleAsync()).CreditBalance);
        Assert.Empty(await context.CreditLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task PrepareMissionTurnDebitAsync_CommitsImmediatelyAndIsIdempotent()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 2);
        CreditService service = CreateService(context);

        MissionTurnDebitResult first = await service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-1");
        MissionTurnDebitResult retry = await service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-1");

        AppUser user = await context.AppUsers.AsNoTracking().SingleAsync();
        CreditLedgerEntry entry = await context.CreditLedgerEntries.AsNoTracking().SingleAsync();
        Assert.True(first.WasNewlyCharged);
        Assert.False(retry.WasNewlyCharged);
        Assert.Equal(1, first.BalanceAfter);
        Assert.Equal(1, retry.BalanceAfter);
        Assert.Equal(1, user.CreditBalance);
        Assert.Equal(1, user.CreditVersion);
        Assert.Equal(-1, entry.Amount);
        Assert.Equal(1, entry.BalanceAfter);
        Assert.Equal(CreditLedgerTypes.MissionTurn, entry.Type);
        Assert.Equal("12:turn-1", entry.SourceId);
    }

    [Fact]
    public async Task RefundMissionTurnDebitAsync_RestoresBalanceAndAllowsRedebit()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 2);
        CreditService service = CreateService(context);

        await service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-1");
        await service.RefundMissionTurnDebitAsync("user-1", 12, "turn-1");
        await service.RefundMissionTurnDebitAsync("user-1", 12, "turn-1"); // idempotent

        AppUser afterRefund = await context.AppUsers.AsNoTracking().SingleAsync();
        Assert.Equal(2, afterRefund.CreditBalance);
        Assert.Empty(await context.CreditLedgerEntries.AsNoTracking().ToListAsync());

        MissionTurnDebitResult redebit = await service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-1");
        Assert.True(redebit.WasNewlyCharged);
        Assert.Equal(1, redebit.BalanceAfter);
        Assert.Single(await context.CreditLedgerEntries.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PrepareMissionTurnDebitAsync_SecondDistinctTurnWithBalanceOneFails()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 1);
        CreditService service = CreateService(context);

        MissionTurnDebitResult first = await service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-a");
        Assert.True(first.WasNewlyCharged);
        Assert.Equal(0, first.BalanceAfter);

        await Assert.ThrowsAsync<InsufficientCreditsException>(() =>
            service.PrepareMissionTurnDebitAsync("user-1", 12, "turn-b"));

        Assert.Equal(0, (await context.AppUsers.AsNoTracking().SingleAsync()).CreditBalance);
        Assert.Single(await context.CreditLedgerEntries.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetAccountAsync_ComputesEnglishMissionUsageByUtcMonthAndIgnoresOtherLedgerTypes()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 6);
        await SeedPackageAsync(context, "Starter", 25_000, 30);
        context.CreditLedgerEntries.AddRange(
            new CreditLedgerEntry
            {
                UserId = "user-1",
                Amount = -1,
                BalanceAfter = 5,
                Type = CreditLedgerTypes.MissionTurn,
                SourceType = "EnglishMissionTurn",
                SourceId = "current-turn",
                Description = "English Mission",
                CreatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new CreditLedgerEntry
            {
                UserId = "user-1",
                Amount = -1,
                BalanceAfter = 4,
                Type = CreditLedgerTypes.MissionTurn,
                SourceType = "EnglishMissionTurn",
                SourceId = "previous-turn",
                Description = "English Mission",
                CreatedAtUtc = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc)
            },
            new CreditLedgerEntry
            {
                UserId = "user-1",
                Amount = 30,
                BalanceAfter = 34,
                Type = CreditLedgerTypes.Purchase,
                SourceType = "CreditPurchase",
                SourceId = "purchase-entry",
                Description = "Mua gói Starter",
                CreatedAtUtc = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc)
            },
            new CreditLedgerEntry
            {
                UserId = "user-1",
                Amount = -2,
                BalanceAfter = 32,
                Type = CreditLedgerTypes.AdminAdjustment,
                SourceType = "AdminAdjustment",
                SourceId = "admin-entry",
                Description = "Điều chỉnh",
                CreatedAtUtc = new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc)
            },
            new CreditLedgerEntry
            {
                UserId = "user-1",
                Amount = -3,
                BalanceAfter = 29,
                Type = CreditLedgerTypes.MissionTurn,
                SourceType = "EnglishMissionTurn",
                SourceId = "next-turn",
                Description = "English Mission",
                CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();

        CreditAccountSnapshot snapshot = await CreateService(context).GetAccountAsync("user-1");

        Assert.Equal(1, snapshot.Usage.CreditsUsedThisMonth);
        Assert.Equal(1, snapshot.Usage.CreditsUsedPreviousMonth);
        Assert.Equal(0, snapshot.Usage.ChangePercent);
        CreditUsageBreakdown breakdown = Assert.Single(snapshot.Usage.Breakdown);
        Assert.Equal("EnglishMissionTurn", breakdown.Key);
        Assert.Equal("English Mission", breakdown.Label);
        Assert.Equal(1, breakdown.Credits);
        Assert.Equal(100, breakdown.Percentage);
    }

    [Fact]
    public async Task GetAccountAsync_LeavesChangePercentEmptyWhenPreviousMonthHasNoUsage()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 2);
        context.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            UserId = "user-1",
            Amount = -1,
            BalanceAfter = 1,
            Type = CreditLedgerTypes.MissionTurn,
            SourceType = "EnglishMissionTurn",
            SourceId = "current-only",
            Description = "English Mission",
            CreatedAtUtc = FixedNow.UtcDateTime
        });
        await context.SaveChangesAsync();

        CreditUsageSummary usage = (await CreateService(context).GetAccountAsync("user-1")).Usage;

        Assert.Equal(1, usage.CreditsUsedThisMonth);
        Assert.Equal(0, usage.CreditsUsedPreviousMonth);
        Assert.Null(usage.ChangePercent);
        Assert.Equal(100, Assert.Single(usage.Breakdown).Percentage);
    }

    [Fact]
    public async Task GetPurchaseStatsAsync_OnlyIncludesCurrentUserPaidRows()
    {
        await using AppDbContext context = CreateContext();
        await SeedUserAsync(context, balance: 0);
        context.AppUsers.Add(new AppUser
        {
            Id = "user-2",
            Email = "other@example.com",
            NormalizedEmail = "OTHER@EXAMPLE.COM",
            UserName = "other",
            NormalizedUserName = "OTHER",
            CreditBalance = 0
        });
        context.CreditPurchases.AddRange(
            new CreditPurchase
            {
                UserId = "user-1",
                InvoiceNumber = "U1",
                PackageName = "Basic",
                PriceVnd = 25_000,
                Credits = 30,
                Status = CreditPurchaseStatuses.Paid,
                CreatedAtUtc = FixedNow.UtcDateTime.AddHours(-3),
                ExpiresAtUtc = FixedNow.UtcDateTime.AddHours(-2),
                PaidAtUtc = FixedNow.UtcDateTime.AddHours(-2)
            },
            new CreditPurchase
            {
                UserId = "user-2",
                InvoiceNumber = "U2",
                PackageName = "Pro",
                PriceVnd = 100_000,
                Credits = 150,
                Status = CreditPurchaseStatuses.Paid,
                CreatedAtUtc = FixedNow.UtcDateTime.AddHours(-3),
                ExpiresAtUtc = FixedNow.UtcDateTime.AddHours(-2),
                PaidAtUtc = FixedNow.UtcDateTime.AddHours(-2)
            });
        await context.SaveChangesAsync();
        CreditService service = CreateService(context);

        DateOnly todayVn = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(FixedNow.UtcDateTime).DateTime);
        CreditPurchaseStatsSnapshot snap = await service.GetPurchaseStatsAsync(
            "user-1", todayVn.AddDays(-6), todayVn);

        Assert.Equal(25_000, snap.TotalPaidVnd);
        Assert.Equal(1, snap.PaidOrderCount);
        Assert.Null(snap.PendingCount);
        Assert.Null(snap.StatusCounts);
        Assert.DoesNotContain(snap.Packages, package => package.PackageName == "Pro");
    }

    private static CreditService CreateService(AppDbContext context, DateTimeOffset? now = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SePay:MerchantId"] = "merchant-123",
                ["SePay:SecretKey"] = "checkout-secret",
                ["SePay:IpnSecret"] = "ipn-secret",
                ["SePay:PublicBaseUrl"] = "https://example.test/",
                ["SePay:Environment"] = "Sandbox"
            })
            .Build();
        return new CreditService(context, configuration, new FixedTimeProvider(now ?? FixedNow));
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUserAsync(AppDbContext context, int balance = 10)
    {
        context.AppUsers.Add(new AppUser
        {
            Id = "user-1",
            Email = "learner@example.com",
            NormalizedEmail = "LEARNER@EXAMPLE.COM",
            UserName = "learner",
            NormalizedUserName = "LEARNER",
            CreditBalance = balance
        });
        await context.SaveChangesAsync();
    }

    private static async Task<CreditPackage> SeedPackageAsync(
        AppDbContext context,
        string name,
        long priceVnd,
        int credits)
    {
        CreditPackage package = new()
        {
            Name = name,
            PriceVnd = priceVnd,
            Credits = credits,
            IsActive = true,
            CreatedAtUtc = FixedNow.UtcDateTime,
            UpdatedAtUtc = FixedNow.UtcDateTime
        };
        context.CreditPackages.Add(package);
        await context.SaveChangesAsync();
        return package;
    }

    private static async Task<CreditPurchase> SeedPurchaseAsync(
        AppDbContext context,
        long priceVnd,
        int credits)
    {
        CreditPurchase purchase = new()
        {
            UserId = "user-1",
            InvoiceNumber = "CRD-TEST-1",
            PackageName = "Starter",
            PriceVnd = priceVnd,
            Credits = credits,
            Status = CreditPurchaseStatuses.Pending,
            CreatedAtUtc = FixedNow.AddMinutes(-5).UtcDateTime,
            ExpiresAtUtc = FixedNow.AddMinutes(25).UtcDateTime
        };
        context.CreditPurchases.Add(purchase);
        await context.SaveChangesAsync();
        return purchase;
    }

    private static SePayIpnPayload CreatePaidPayload(
        CreditPurchase purchase,
        string orderAmount,
        string transactionAmount) =>
        new()
        {
            NotificationType = "ORDER_PAID",
            Order = new SePayIpnOrder
            {
                Id = "order-1",
                Status = "CAPTURED",
                Currency = "VND",
                Amount = orderAmount,
                InvoiceNumber = purchase.InvoiceNumber
            },
            Transaction = new SePayIpnTransaction
            {
                TransactionId = "transaction-1",
                Status = "APPROVED",
                Currency = "VND",
                Amount = transactionAmount,
                PaymentMethod = "BANK_TRANSFER"
            }
        };

    private static string Field(SePayCheckoutForm form, string name) =>
        form.Fields.Single(field => field.Key == name).Value;

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
