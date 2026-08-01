using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Services.Credits;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Credits;

public sealed class AdminCreditServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 1, 6, 0, 0, TimeSpan.Zero);

    private static readonly AdminActorContext Actor =
        new("admin-1", "Admin", "correlation-1");

    [Fact]
    public async Task SavePackageAsync_Create_PersistsPackageAndAudit()
    {
        await using AppDbContext context = CreateContext();
        AdminCreditService service = CreateService(context);

        AdminCreditOperationResult result = await service.SavePackageAsync(
            PackageCommand(reason: "Launch package"));

        CreditPackage package = await context.CreditPackages.SingleAsync();
        AdminAuditLog audit = await context.AdminAuditLogs.SingleAsync();
        Assert.True(result.Succeeded);
        Assert.Equal("Starter", package.Name);
        Assert.Equal("Entry package", package.Description);
        Assert.Equal(25_000, package.PriceVnd);
        Assert.Equal(30, package.Credits);
        Assert.Equal(FixedNow.UtcDateTime, package.CreatedAtUtc);
        Assert.Equal(FixedNow.UtcDateTime, package.UpdatedAtUtc);
        Assert.Equal(AdminAuditActions.CreditPackagesCreate, audit.Action);
        Assert.Equal("Launch package", audit.Reason);
        Assert.Equal(Actor.CorrelationId, audit.CorrelationId);
    }

    [Fact]
    public async Task SavePackageAsync_Update_RequiresMatchingVersionAndIncrementsIt()
    {
        await using AppDbContext context = CreateContext();
        CreditPackage package = await SeedPackageAsync(context, version: 4);
        AdminCreditService service = CreateService(context);

        AdminCreditOperationResult result = await service.SavePackageAsync(
            PackageCommand(package.Id, package.Version, "Update pricing", name: "Starter Plus"));

        AdminAuditLog audit = await context.AdminAuditLogs.SingleAsync();
        Assert.True(result.Succeeded);
        Assert.Equal("Starter Plus", package.Name);
        Assert.Equal(5, package.Version);
        Assert.Equal(FixedNow.UtcDateTime, package.UpdatedAtUtc);
        Assert.Equal(AdminAuditActions.CreditPackagesUpdate, audit.Action);
        Assert.Equal(package.Id.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task SetPackageArchivedAsync_RequiresMatchingVersionAndIncrementsIt()
    {
        await using AppDbContext context = CreateContext();
        CreditPackage package = await SeedPackageAsync(context, version: 2);
        AdminCreditService service = CreateService(context);

        AdminCreditOperationResult result = await service.SetPackageArchivedAsync(
            new(package.Id, package.Version, true, "Retired offer", Actor));

        AdminAuditLog audit = await context.AdminAuditLogs.SingleAsync();
        Assert.True(result.Succeeded);
        Assert.True(package.IsArchived);
        Assert.Equal(3, package.Version);
        Assert.Equal(AdminAuditActions.CreditPackagesArchive, audit.Action);
        Assert.Equal("Retired offer", audit.Reason);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("archive")]
    public async Task PackageMutation_RejectsMissingReason(string operation)
    {
        await using AppDbContext context = CreateContext();
        CreditPackage? package = operation == "create"
            ? null
            : await SeedPackageAsync(context, version: 3);
        Mock<IAdminAuditService> audit = new();
        AdminCreditService service = CreateService(context, audit.Object);

        AdminCreditOperationResult result = operation == "archive"
            ? await service.SetPackageArchivedAsync(
                new(package!.Id, package.Version, true, "  ", Actor))
            : await service.SavePackageAsync(PackageCommand(
                package?.Id,
                package?.Version ?? 0,
                "  "));

        Assert.False(result.Succeeded);
        Assert.Contains("Lý do", result.Message);
        Assert.Equal(operation == "create" ? 0 : 1, await context.CreditPackages.CountAsync());
        audit.Verify(item => item.Enqueue(It.IsAny<AdminAuditEntry>()), Times.Never);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("archive")]
    public async Task ExistingPackageMutation_RejectsStaleVersion(string operation)
    {
        await using AppDbContext context = CreateContext();
        CreditPackage package = await SeedPackageAsync(context, version: 5);
        Mock<IAdminAuditService> audit = new();
        AdminCreditService service = CreateService(context, audit.Object);

        AdminCreditOperationResult result = operation == "archive"
            ? await service.SetPackageArchivedAsync(
                new(package.Id, 4, true, "Retired offer", Actor))
            : await service.SavePackageAsync(
                PackageCommand(package.Id, 4, "Update pricing", name: "Changed"));

        Assert.False(result.Succeeded);
        Assert.Contains("đã thay đổi", result.Message);
        Assert.Equal("Starter", package.Name);
        Assert.False(package.IsArchived);
        Assert.Equal(5, package.Version);
        audit.Verify(item => item.Enqueue(It.IsAny<AdminAuditEntry>()), Times.Never);
    }

    [Fact]
    public async Task AdjustBalanceAsync_PersistsBalanceLedgerAndAuditInSharedContext()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = await SeedUserAsync(context, balance: 20, creditVersion: 7);
        AdminCreditService service = CreateService(context);

        AdminCreditOperationResult result = await service.AdjustBalanceAsync(
            new(user.Id, 7, -6, "Manual correction", Actor));

        CreditLedgerEntry ledger = await context.CreditLedgerEntries.SingleAsync();
        AdminAuditLog audit = await context.AdminAuditLogs.SingleAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(14, user.CreditBalance);
        Assert.Equal(8, user.CreditVersion);
        Assert.Equal(-6, ledger.Amount);
        Assert.Equal(14, ledger.BalanceAfter);
        Assert.Equal(CreditLedgerTypes.AdminAdjustment, ledger.Type);
        Assert.Equal("AdminAdjustment", ledger.SourceType);
        Assert.Equal("Manual correction", ledger.Description);
        Assert.Equal(Actor.UserId, ledger.AdminActorUserId);
        Assert.Equal(FixedNow.UtcDateTime, ledger.CreatedAtUtc);
        Assert.Equal(AdminAuditActions.CreditBalanceAdjust, audit.Action);
        Assert.Equal(user.Id, audit.TargetId);
        Assert.Equal("Manual correction", audit.Reason);
        Assert.Equal(FixedNow.UtcDateTime, audit.OccurredAtUtc);
    }

    [Fact]
    public async Task AdjustBalanceAsync_RejectsNegativeFinalBalance()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = await SeedUserAsync(context, balance: 5, creditVersion: 2);
        Mock<IAdminAuditService> audit = new();
        AdminCreditService service = CreateService(context, audit.Object);

        AdminCreditOperationResult result = await service.AdjustBalanceAsync(
            new(user.Id, 2, -6, "Too large", Actor));

        Assert.False(result.Succeeded);
        Assert.Equal(5, user.CreditBalance);
        Assert.Equal(2, user.CreditVersion);
        Assert.Empty(await context.CreditLedgerEntries.ToListAsync());
        audit.Verify(item => item.Enqueue(It.IsAny<AdminAuditEntry>()), Times.Never);
    }

    [Fact]
    public async Task AdjustBalanceAsync_RejectsStaleCreditVersion()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = await SeedUserAsync(context, balance: 5, creditVersion: 3);
        Mock<IAdminAuditService> audit = new();
        AdminCreditService service = CreateService(context, audit.Object);

        AdminCreditOperationResult result = await service.AdjustBalanceAsync(
            new(user.Id, 2, 4, "Manual grant", Actor));

        Assert.False(result.Succeeded);
        Assert.Contains("Số dư đã thay đổi", result.Message);
        Assert.Equal(5, user.CreditBalance);
        Assert.Equal(3, user.CreditVersion);
        Assert.Empty(await context.CreditLedgerEntries.ToListAsync());
        audit.Verify(item => item.Enqueue(It.IsAny<AdminAuditEntry>()), Times.Never);
    }

    private static AdminCreditPackageCommand PackageCommand(
        int? id = null,
        int version = 0,
        string reason = "Reason",
        string name = "Starter") =>
        new(id, version, name, "Entry package", 25_000, 30, 1, true, reason, Actor);

    private static AdminCreditService CreateService(
        AppDbContext context,
        IAdminAuditService? audit = null)
    {
        FixedTimeProvider timeProvider = new(FixedNow);
        audit ??= new AdminAuditService(context, timeProvider);
        return new AdminCreditService(context, audit, timeProvider);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<CreditPackage> SeedPackageAsync(
        AppDbContext context,
        int version)
    {
        CreditPackage package = new()
        {
            Name = "Starter",
            PriceVnd = 20_000,
            Credits = 25,
            IsActive = true,
            Version = version,
            CreatedAtUtc = FixedNow.AddDays(-1).UtcDateTime,
            UpdatedAtUtc = FixedNow.AddDays(-1).UtcDateTime
        };
        context.CreditPackages.Add(package);
        await context.SaveChangesAsync();
        return package;
    }

    private static async Task<AppUser> SeedUserAsync(
        AppDbContext context,
        int balance,
        int creditVersion)
    {
        AppUser user = new()
        {
            Id = "user-1",
            Email = "learner@example.com",
            NormalizedEmail = "LEARNER@EXAMPLE.COM",
            UserName = "learner",
            NormalizedUserName = "LEARNER",
            CreditBalance = balance,
            CreditVersion = creditVersion
        };
        context.AppUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
