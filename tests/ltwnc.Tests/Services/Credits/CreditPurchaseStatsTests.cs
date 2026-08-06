using ltwnc.Models.Entities;
using ltwnc.Services.Credits;

namespace ltwnc.Tests.Services.Credits;

public sealed class CreditPurchaseStatsTests
{
    private static readonly DateOnly Today = new(2026, 8, 6);

    [Fact]
    public void ResolveRange_Default_IsLast30Days()
    {
        var (from, to, error) = CreditPurchaseStatsBuilder.ResolveRange(null, null, Today);
        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 7, 8), from);
        Assert.Equal(Today, to);
    }

    [Fact]
    public void ResolveRange_Over365_FallsBackWithError()
    {
        var (from, to, error) = CreditPurchaseStatsBuilder.ResolveRange(
            new DateOnly(2025, 1, 1), Today, Today);
        Assert.NotNull(error);
        Assert.Equal(new DateOnly(2026, 7, 8), from);
        Assert.Equal(Today, to);
    }

    [Fact]
    public void ResolveRange_FromAfterTo_FallsBackWithError()
    {
        var (_, _, error) = CreditPurchaseStatsBuilder.ResolveRange(
            Today, Today.AddDays(-1), Today);
        Assert.NotNull(error);
    }

    [Fact]
    public void Build_PaidAcrossTwoDays_BucketsDailyMoneyAndCount()
    {
        DateOnly from = new(2026, 8, 1);
        DateOnly to = new(2026, 8, 7);
        var rows = new[]
        {
            // 03:00 UTC = 10:00 VN same day; 10:00 UTC = 17:00 VN same day
            Paid(1, "Basic", 10_000, new DateTime(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc)),
            Paid(2, "Basic", 10_000, new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc)),
            Paid(3, "Pro", 50_000, new DateTime(2026, 8, 3, 5, 0, 0, DateTimeKind.Utc)),
            Paid(4, "Pro", 50_000, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            new CreditPurchaseStatsSourceRow(
                5, "u1", "learner", "Basic", 10_000, CreditPurchaseStatuses.Pending,
                new DateTime(2026, 8, 3, 1, 0, 0, DateTimeKind.Utc), null, null)
        };

        CreditPurchaseStatsSnapshot snap = CreditPurchaseStatsBuilder.Build(
            rows, from, to, Today, rangeError: null, includeAdminExtras: true);

        Assert.Equal("day", snap.BucketKind);
        Assert.Equal(70_000, snap.TotalPaidVnd);
        Assert.Equal(3, snap.PaidOrderCount);
        Assert.Equal(70_000 / 3, snap.AverageOrderValueVnd);
        Assert.Equal(1, snap.PendingCount);

        Assert.Contains(snap.Buckets, b => b.PaidVnd == 20_000 && b.PaidCount == 2);
        Assert.Contains(snap.Buckets, b => b.PaidVnd == 50_000 && b.PaidCount == 1);

        Assert.Equal(2, snap.Packages.Count);
        Assert.Equal(20_000, snap.Packages.Single(p => p.PackageName == "Basic").PaidVnd);
        Assert.Equal(50_000, snap.Packages.Single(p => p.PackageName == "Pro").PaidVnd);

        Assert.NotNull(snap.StatusCounts);
        Assert.Equal(3, snap.StatusCounts!.Single(s => s.Status == CreditPurchaseStatuses.Paid).Count);
        Assert.Equal(1, snap.StatusCounts.Single(s => s.Status == CreditPurchaseStatuses.Pending).Count);
    }

    [Fact]
    public void Build_AovZero_WhenNoPaid()
    {
        CreditPurchaseStatsSnapshot snap = CreditPurchaseStatsBuilder.Build(
            [], new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 7), Today, null, false);
        Assert.Equal(0, snap.AverageOrderValueVnd);
        Assert.Null(snap.PendingCount);
        Assert.Null(snap.StatusCounts);
    }

    [Fact]
    public void Build_UserExtrasOff_OmitsPendingAndStatus()
    {
        var rows = new[]
        {
            Paid(1, "Basic", 10_000, new DateTime(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc)),
            new CreditPurchaseStatsSourceRow(
                2, "u1", null, "Basic", 10_000, CreditPurchaseStatuses.Pending,
                new DateTime(2026, 8, 2, 4, 0, 0, DateTimeKind.Utc), null, null)
        };
        CreditPurchaseStatsSnapshot snap = CreditPurchaseStatsBuilder.Build(
            rows, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 7), Today, null, includeAdminExtras: false);
        Assert.Null(snap.PendingCount);
        Assert.Null(snap.StatusCounts);
        Assert.Equal(10_000, snap.TotalPaidVnd);
    }

    [Fact]
    public void StatusTimestamp_VoidedPrefersVoidedAt()
    {
        var row = new CreditPurchaseStatsSourceRow(
            1, "u", null, "P", 1, CreditPurchaseStatuses.Voided,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(row.VoidedAtUtc, CreditPurchaseStatsBuilder.StatusTimestamp(row));
    }

    private static CreditPurchaseStatsSourceRow Paid(
        int id, string package, long vnd, DateTime paidAtUtc) =>
        new(id, "u1", "learner", package, vnd, CreditPurchaseStatuses.Paid,
            paidAtUtc.AddHours(-1), paidAtUtc, null);
}
