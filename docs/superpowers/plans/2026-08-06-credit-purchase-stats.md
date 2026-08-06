# Credit Purchase Stats Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Admin + User SSR pages with Chart.js for Paid credit-package money (VND) and order counts over a selectable range (max 365 days).

**Architecture:** Shared pure helper validates range / builds day-week-month buckets and aggregates `CreditPurchase` rows. `AdminCreditService` and `CreditService` load filtered rows and call the helper. Controllers return view models; views embed JSON + Chart.js like `/Admin` dashboard. No new packages, no JSON API.

**Tech Stack:** ASP.NET Core MVC, EF Core, xUnit, Chart.js (existing `wwwroot/lib/chart.js`), Vietnam TZ via `AdminTimeZone`.

**Spec:** `docs/superpowers/specs/2026-08-06-credit-purchase-stats-design.md`

## Global Constraints

- Money KPIs/charts: only `Status == Paid` with `PaidAtUtc` in range (VN midnight bounds → UTC).
- Pending KPI (admin only): `Status == Pending`, filter `CreatedAtUtc` in range.
- Status breakdown + recent table: status-timestamp rules from spec (`VoidedAtUtc` → `PaidAtUtc` → `CreatedAtUtc`).
- Max range 365 days; default last 30 days; invalid range → warning + fallback 30 days.
- Buckets: ≤31 day, ≤90 week (Monday-start VN), else month.
- User queries MUST filter `UserId == currentUserId`.
- No new NuGet packages. Reuse Chart.js + admin dashboard CSS patterns where they fit.
- TDD: failing test → implement → pass → commit per task.

## File map

| File | Responsibility |
|------|----------------|
| `Services/Credits/CreditPurchaseStats.cs` | DTOs + pure range/bucket/aggregate helper |
| `Services/Credits/AdminCreditContracts.cs` | `GetStatsAsync` on interface |
| `Services/Credits/AdminCreditService.cs` | Load global purchases + user names → helper |
| `Services/Credits/CreditContracts.cs` | `GetPurchaseStatsAsync` on interface |
| `Services/Credits/CreditService.cs` | Load user purchases → helper |
| `Areas/Admin/Models/AdminCreditViewModels.cs` | `AdminCreditStatsViewModel` (+ row types if needed) |
| `Areas/Admin/Controllers/CreditsController.cs` | `Stats` GET |
| `Areas/Admin/Views/Credits/Stats.cshtml` | Admin UI |
| `Areas/Admin/Views/Credits/Index.cshtml` | Link to Stats |
| `Areas/Admin/Views/Shared/_AdminLayout.cshtml` | Optional secondary nav highlight stays on Credits |
| `Controllers/CreditsController.cs` | `Stats` GET |
| `Views/Credits/Stats.cshtml` | User UI |
| `Views/Credits/Index.cshtml` | Link to Stats |
| `wwwroot/js/credit-purchase-stats-chart.js` | Combo bar+line, package doughnut, status bar |
| `tests/ltwnc.Tests/Services/Credits/CreditPurchaseStatsTests.cs` | Pure helper tests |
| `tests/ltwnc.Tests/Services/Credits/AdminCreditServiceTests.cs` | Admin stats integration |
| `tests/ltwnc.Tests/Services/Credits/CreditServiceTests.cs` | User stats isolation |

---

### Task 1: Pure stats helper + unit tests

**Files:**
- Create: `Services/Credits/CreditPurchaseStats.cs`
- Create: `tests/ltwnc.Tests/Services/Credits/CreditPurchaseStatsTests.cs`

**Interfaces:**
- Consumes: `ltwnc.Areas.Admin.AdminTimeZone`, `CreditPurchaseStatuses`
- Produces:
  - `CreditPurchaseStatsSnapshot` record (full shape below)
  - `CreditPurchaseStatsBuilder.ResolveRange(DateOnly? from, DateOnly? to, DateOnly todayVn)`
  - `CreditPurchaseStatsBuilder.Build(IReadOnlyList<CreditPurchaseStatsSourceRow> rows, DateOnly from, DateOnly to, DateOnly todayVn, string? rangeError, bool includeAdminExtras)`

**DTO shape (put in same file):**

```csharp
namespace ltwnc.Services.Credits;

public sealed record CreditPurchaseStatsSnapshot(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    string? RangeError,
    string BucketKind, // "day" | "week" | "month"
    long TotalPaidVnd,
    int PaidOrderCount,
    long AverageOrderValueVnd,
    int? PendingCount,
    IReadOnlyList<CreditPurchaseStatsBucket> Buckets,
    IReadOnlyList<CreditPurchaseStatsPackage> Packages,
    IReadOnlyList<CreditPurchaseStatsStatusCount>? StatusCounts,
    IReadOnlyList<CreditPurchaseStatsRow> RecentRows);

public sealed record CreditPurchaseStatsBucket(string Key, string Label, long PaidVnd, int PaidCount);
public sealed record CreditPurchaseStatsPackage(string PackageName, long PaidVnd, int PaidCount);
public sealed record CreditPurchaseStatsStatusCount(string Status, int Count);
public sealed record CreditPurchaseStatsRow(
    int Id,
    string? UserName,
    string PackageName,
    long PriceVnd,
    string Status,
    DateTime TimestampUtc);

public sealed record CreditPurchaseStatsSourceRow(
    int Id,
    string UserId,
    string? UserName,
    string PackageName,
    long PriceVnd,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? VoidedAtUtc);

public static class CreditPurchaseStatsBuilder
{
    public const int MaximumRangeDays = 365;
    public const int DefaultRangeDays = 30;
    public const int RecentRowLimit = 20;

    public static (DateOnly From, DateOnly To, string? Error) ResolveRange(
        DateOnly? requestedFrom,
        DateOnly? requestedTo,
        DateOnly todayVn) { /* ... */ }

    public static CreditPurchaseStatsSnapshot Build(
        IReadOnlyList<CreditPurchaseStatsSourceRow> rows,
        DateOnly from,
        DateOnly to,
        DateOnly todayVn,
        string? rangeError,
        bool includeAdminExtras) { /* ... */ }

    public static DateTime StatusTimestamp(CreditPurchaseStatsSourceRow row) { /* ... */ }
    public static DateTime ToUtc(DateOnly date, TimeOnly time) { /* same as AdminDashboardService */ }
}
```

**ResolveRange rules (copy dashboard pattern, new limits):**
- Both null → `(today - 29 days, today, null)` i.e. 30 inclusive days
- One null → error + default 30 days
- `from > to` → error + default
- day count > 365 → error + default
- else `(from, to, null)`

**BucketKind:** `dayCount <= 31` → day; `<= 90` → week; else month.

**Week key:** Monday-start containing the VN date. Label e.g. `dd/MM` of Monday or `dd/MM–dd/MM`.

**StatusTimestamp:**
- Paid → `PaidAtUtc` (if null, skip row for paid series; still use CreatedAt for status table only if needed — prefer skip paid without PaidAtUtc from revenue)
- Voided → `VoidedAtUtc ?? PaidAtUtc ?? CreatedAtUtc`
- else → `CreatedAtUtc`

**Build logic:**
1. `startUtc = ToUtc(from, MinValue)`, `endUtc = ToUtc(to.AddDays(1), MinValue)`
2. Emit full bucket list for [from, to]
3. For each row with `Status == Paid` and `PaidAtUtc` in `[startUtc, endUtc)`: add to bucket + package map + totals
4. AOV = paidCount == 0 ? 0 : totalVnd / paidCount (integer division long)
5. If `includeAdminExtras`: PendingCount = count Pending with CreatedAt in window; StatusCounts group by Status where StatusTimestamp in window
6. RecentRows: rows with StatusTimestamp in window, order desc by timestamp then Id, take 20; UserName passthrough

- [ ] **Step 1: Write failing tests**

Create `tests/ltwnc.Tests/Services/Credits/CreditPurchaseStatsTests.cs`:

```csharp
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
        // 2026-08-02 03:00 UTC = 10:00 VN Aug 2
        // 2026-08-02 20:00 UTC = 03:00 VN Aug 3
        // 2026-08-03 05:00 UTC = 12:00 VN Aug 3
        var rows = new[]
        {
            Paid(1, "Basic", 10_000, new DateTime(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc)),
            Paid(2, "Basic", 10_000, new DateTime(2026, 8, 2, 20, 0, 0, DateTimeKind.Utc)),
            Paid(3, "Pro", 50_000, new DateTime(2026, 8, 3, 5, 0, 0, DateTimeKind.Utc)),
            // outside range
            Paid(4, "Pro", 50_000, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            // pending must not count as revenue
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

        CreditPurchaseStatsBucket aug2 = snap.Buckets.Single(b => b.Label == "02/08" || b.Key.Contains("2026-08-02"));
        // Prefer assert by finding bucket whose PaidVnd matches day totals:
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
```

Adjust bucket assertions if labels differ — after implementing, keep asserts on `PaidVnd`/`PaidCount` totals which are stable.

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~CreditPurchaseStatsTests" --no-restore
```

Expected: compile fail or missing type `CreditPurchaseStatsBuilder`.

- [ ] **Step 3: Implement `Services/Credits/CreditPurchaseStats.cs`**

Implement DTOs + builder as specified. Reuse timezone conversion:

```csharp
private static DateTime ToUtc(DateOnly date, TimeOnly time)
{
    DateTime local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
    return TimeZoneInfo.ConvertTimeToUtc(local, AdminTimeZone.Vietnam);
}
```

For day labels use `dd/MM`. For week: start Monday (`DayOfWeek` math), key `$"{monday:yyyy-MM-dd}"`, label `$"{monday:dd/MM}"`. For month: key `yyyy-MM`, label `MM/yyyy`.

Fill every bucket in range with zeros first, then increment.

Package list: order by `PaidVnd` desc, then name.

Status counts: stable order Pending, Paid, Expired, Cancelled, Failed, Voided (only include statuses with count > 0 OR always include known set with zeros — prefer **only count > 0** to keep chart clean).

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~CreditPurchaseStatsTests"
```

- [ ] **Step 5: Commit**

```bash
git add Services/Credits/CreditPurchaseStats.cs tests/ltwnc.Tests/Services/Credits/CreditPurchaseStatsTests.cs
git commit -m "feat(credits): add purchase stats range and aggregate helper"
```

---

### Task 2: Admin service `GetStatsAsync`

**Files:**
- Modify: `Services/Credits/AdminCreditContracts.cs`
- Modify: `Services/Credits/AdminCreditService.cs`
- Modify: `tests/ltwnc.Tests/Services/Credits/AdminCreditServiceTests.cs`

**Interfaces:**
- Consumes: `CreditPurchaseStatsBuilder`
- Produces: `Task<CreditPurchaseStatsSnapshot> GetStatsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)` on `IAdminCreditService`

- [ ] **Step 1: Write failing tests** in `AdminCreditServiceTests.cs`

```csharp
[Fact]
public async Task GetStatsAsync_AggregatesPaidInRange_AndIgnoresOtherUsersPendingOutsideRevenue()
{
    await using AppDbContext context = CreateContext();
    await SeedUserAsync(context, "user-1", "learner", "LEARNER");
    await SeedUserAsync(context, "user-2", "other", "OTHER");
    // FixedNow = 2026-08-01 06:00 UTC → VN date 2026-08-01
    context.CreditPurchases.AddRange(
        new CreditPurchase
        {
            UserId = "user-1",
            InvoiceNumber = "A1",
            PackageName = "Basic",
            PriceVnd = 25_000,
            Credits = 30,
            Status = CreditPurchaseStatuses.Paid,
            CreatedAtUtc = FixedNow.UtcDateTime.AddDays(-2),
            ExpiresAtUtc = FixedNow.UtcDateTime.AddDays(-2).AddMinutes(30),
            PaidAtUtc = FixedNow.UtcDateTime.AddHours(-5)
        },
        new CreditPurchase
        {
            UserId = "user-2",
            InvoiceNumber = "A2",
            PackageName = "Pro",
            PriceVnd = 100_000,
            Credits = 150,
            Status = CreditPurchaseStatuses.Paid,
            CreatedAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            ExpiresAtUtc = FixedNow.UtcDateTime.AddDays(-1).AddMinutes(30),
            PaidAtUtc = FixedNow.UtcDateTime.AddHours(-2)
        },
        new CreditPurchase
        {
            UserId = "user-1",
            InvoiceNumber = "A3",
            PackageName = "Basic",
            PriceVnd = 25_000,
            Credits = 30,
            Status = CreditPurchaseStatuses.Pending,
            CreatedAtUtc = FixedNow.UtcDateTime.AddHours(-1),
            ExpiresAtUtc = FixedNow.UtcDateTime.AddMinutes(29)
        });
    await context.SaveChangesAsync();
    AdminCreditService service = CreateService(context);

    DateOnly todayVn = DateOnly.FromDateTime(
        AdminTimeZone.ToVietnamTime(FixedNow.UtcDateTime).Date);
    CreditPurchaseStatsSnapshot snap = await service.GetStatsAsync(
        todayVn.AddDays(-6), todayVn);

    Assert.Equal(125_000, snap.TotalPaidVnd);
    Assert.Equal(2, snap.PaidOrderCount);
    Assert.Equal(1, snap.PendingCount);
    Assert.Contains(snap.RecentRows, r => r.UserName == "learner");
    Assert.Contains(snap.Packages, p => p.PackageName == "Pro" && p.PaidVnd == 100_000);
}

// Add SeedUserAsync helper if missing:
private static async Task SeedUserAsync(
    AppDbContext context, string id, string userName, string normalized)
{
    context.AppUsers.Add(new AppUser
    {
        Id = id,
        Email = $"{userName}@example.com",
        NormalizedEmail = $"{normalized}@EXAMPLE.COM",
        UserName = userName,
        NormalizedUserName = normalized,
        CreditBalance = 0
    });
    await context.SaveChangesAsync();
}
```

Add `using ltwnc.Areas.Admin;` for `AdminTimeZone`.

- [ ] **Step 2: Run test — expect FAIL**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AdminCreditServiceTests.GetStatsAsync"
```

- [ ] **Step 3: Implement**

`AdminCreditContracts.cs` — add method to interface.

`AdminCreditService.GetStatsAsync`:

```csharp
public async Task<CreditPurchaseStatsSnapshot> GetStatsAsync(
    DateOnly? requestedFrom,
    DateOnly? requestedTo,
    CancellationToken cancellationToken = default)
{
    DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    DateOnly todayVn = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(nowUtc).Date);
    (DateOnly from, DateOnly to, string? error) = CreditPurchaseStatsBuilder.ResolveRange(
        requestedFrom, requestedTo, todayVn);

    DateTime startUtc = CreditPurchaseStatsBuilder.ToUtc(from, TimeOnly.MinValue);
    DateTime endUtc = CreditPurchaseStatsBuilder.ToUtc(to.AddDays(1), TimeOnly.MinValue);

    // Broad fetch: anything that might fall in range by created/paid/voided
    List<CreditPurchaseStatsSourceRow> rows = await (
        from p in _db.CreditPurchases.AsNoTracking()
        join u in _db.AppUsers.AsNoTracking() on p.UserId equals u.Id
        where (p.PaidAtUtc != null && p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc)
            || (p.CreatedAtUtc >= startUtc && p.CreatedAtUtc < endUtc)
            || (p.VoidedAtUtc != null && p.VoidedAtUtc >= startUtc && p.VoidedAtUtc < endUtc)
        select new CreditPurchaseStatsSourceRow(
            p.Id, p.UserId, u.UserName, p.PackageName, p.PriceVnd, p.Status,
            p.CreatedAtUtc, p.PaidAtUtc, p.VoidedAtUtc))
        .ToListAsync(cancellationToken);

    return CreditPurchaseStatsBuilder.Build(rows, from, to, todayVn, error, includeAdminExtras: true);
}
```

Make `ToUtc` **public** on the builder (used here).

- [ ] **Step 4: Run tests — PASS**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AdminCreditServiceTests"
```

- [ ] **Step 5: Commit**

```bash
git add Services/Credits/AdminCreditContracts.cs Services/Credits/AdminCreditService.cs Services/Credits/CreditPurchaseStats.cs tests/ltwnc.Tests/Services/Credits/AdminCreditServiceTests.cs
git commit -m "feat(admin-credits): aggregate purchase stats for dashboard"
```

---

### Task 3: User service `GetPurchaseStatsAsync`

**Files:**
- Modify: `Services/Credits/CreditContracts.cs`
- Modify: `Services/Credits/CreditService.cs`
- Modify: `tests/ltwnc.Tests/Services/Credits/CreditServiceTests.cs`

**Interfaces:**
- Produces: `Task<CreditPurchaseStatsSnapshot> GetPurchaseStatsAsync(string userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)`

- [ ] **Step 1: Failing tests**

```csharp
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

    DateOnly todayVn = DateOnly.FromDateTime(
        AdminTimeZone.ToVietnamTime(FixedNow.UtcDateTime).Date);
    CreditPurchaseStatsSnapshot snap = await service.GetPurchaseStatsAsync(
        "user-1", todayVn.AddDays(-6), todayVn);

    Assert.Equal(25_000, snap.TotalPaidVnd);
    Assert.Equal(1, snap.PaidOrderCount);
    Assert.Null(snap.PendingCount);
    Assert.Null(snap.StatusCounts);
    Assert.All(snap.RecentRows, r => Assert.True(r.UserName is null or "learner"));
    Assert.DoesNotContain(snap.Packages, p => p.PackageName == "Pro");
}
```

- [ ] **Step 2: Run — FAIL**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~GetPurchaseStatsAsync_OnlyIncludesCurrentUserPaidRows"
```

- [ ] **Step 3: Implement**

Interface method + `CreditService`:

```csharp
public async Task<CreditPurchaseStatsSnapshot> GetPurchaseStatsAsync(
    string userId,
    DateOnly? requestedFrom,
    DateOnly? requestedTo,
    CancellationToken cancellationToken = default)
{
    DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    DateOnly todayVn = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(nowUtc).Date);
    (DateOnly from, DateOnly to, string? error) = CreditPurchaseStatsBuilder.ResolveRange(
        requestedFrom, requestedTo, todayVn);
    DateTime startUtc = CreditPurchaseStatsBuilder.ToUtc(from, TimeOnly.MinValue);
    DateTime endUtc = CreditPurchaseStatsBuilder.ToUtc(to.AddDays(1), TimeOnly.MinValue);

    List<CreditPurchaseStatsSourceRow> rows = await _db.CreditPurchases.AsNoTracking()
        .Where(p => p.UserId == userId
            && ((p.PaidAtUtc != null && p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc)
                || (p.CreatedAtUtc >= startUtc && p.CreatedAtUtc < endUtc)
                || (p.VoidedAtUtc != null && p.VoidedAtUtc >= startUtc && p.VoidedAtUtc < endUtc)))
        .Select(p => new CreditPurchaseStatsSourceRow(
            p.Id, p.UserId, null, p.PackageName, p.PriceVnd, p.Status,
            p.CreatedAtUtc, p.PaidAtUtc, p.VoidedAtUtc))
        .ToListAsync(cancellationToken);

    return CreditPurchaseStatsBuilder.Build(rows, from, to, todayVn, error, includeAdminExtras: false);
}
```

Add `using ltwnc.Areas.Admin;` in CreditService if not present.

- [ ] **Step 4: Run — PASS**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~CreditServiceTests"
```

- [ ] **Step 5: Commit**

```bash
git add Services/Credits/CreditContracts.cs Services/Credits/CreditService.cs tests/ltwnc.Tests/Services/Credits/CreditServiceTests.cs
git commit -m "feat(credits): user purchase stats scoped to current user"
```

---

### Task 4: Admin Stats page (controller, view model, view, JS, links)

**Files:**
- Modify: `Areas/Admin/Models/AdminCreditViewModels.cs` (add view model or map snapshot directly)
- Modify: `Areas/Admin/Controllers/CreditsController.cs`
- Create: `Areas/Admin/Views/Credits/Stats.cshtml`
- Create: `wwwroot/js/credit-purchase-stats-chart.js`
- Modify: `Areas/Admin/Views/Credits/Index.cshtml` — header link
- Modify: `Areas/Admin/Views/Shared/_AdminLayout.cshtml` only if needed for active menu (Credits controller already highlights)

**Interfaces:**
- Consumes: `IAdminCreditService.GetStatsAsync`
- Route: `GET /Admin/Credits/Stats?from=&to=`

- [ ] **Step 1: Controller action**

```csharp
[HttpGet("Stats")]
public async Task<IActionResult> Stats(
    DateOnly? from,
    DateOnly? to,
    CancellationToken cancellationToken)
{
    CreditPurchaseStatsSnapshot snap = await _service.GetStatsAsync(from, to, cancellationToken);
    return View(snap); // use snapshot directly as model to avoid mapping bloat
}
```

If views prefer Admin.Models namespace only, thin wrapper is OK — prefer **direct snapshot** (YAGNI).

Ensure `_ViewImports` already imports `ltwnc.Services.Credits` or add `@model CreditPurchaseStatsSnapshot` with full name.

- [ ] **Step 2: View `Areas/Admin/Views/Credits/Stats.cshtml`**

Structure (mirror dashboard filter + cards):

1. Heading `Thống kê mua gói`
2. Preset links: 7d, 30d, tháng này, 90d, năm nay + from/to form posting GET to `/Admin/Credits/Stats`
3. Range error alert if `Model.RangeError != null`
4. KPI grid: Tổng tiền · Đơn paid · AOV · Pending  
   Format VND with `CultureInfo.GetCultureInfo("vi-VN")`
5. Charts section:
   - canvas `data-credit-stats-chart="revenue"`
   - canvas `data-credit-stats-chart="packages"`
   - canvas `data-credit-stats-chart="status"`
6. Empty state when `PaidOrderCount == 0`
7. Table recent rows (UserName, Package, Price, Status, time VN)
8. JSON:

```csharp
string chartDataJson = System.Text.Json.JsonSerializer.Serialize(new
{
    labels = Model.Buckets.Select(b => b.Label),
    paidVnd = Model.Buckets.Select(b => b.PaidVnd),
    paidCount = Model.Buckets.Select(b => b.PaidCount),
    packages = Model.Packages.Select(p => new { label = p.PackageName, vnd = p.PaidVnd, count = p.PaidCount }),
    statuses = (Model.StatusCounts ?? []).Select(s => new { label = s.Status, count = s.Count })
});
```

9. Scripts section: chart.umd + `credit-purchase-stats-chart.js`

**Preset URL helpers in view:**

```csharp
DateOnly today = Model.Today;
// 7 days: today.AddDays(-6) .. today
// 30 days: today.AddDays(-29) .. today
// month: new DateOnly(today.Year, today.Month, 1) .. today
// 90: today.AddDays(-89) .. today
// ytd: new DateOnly(today.Year, 1, 1) .. today
```

- [ ] **Step 3: JS `wwwroot/js/credit-purchase-stats-chart.js`**

```javascript
(function () {
    const dataEl = document.getElementById('credit-purchase-stats-data');
    if (!dataEl || typeof Chart === 'undefined') return;
    let data;
    try { data = JSON.parse(dataEl.textContent); } catch { return; }

    const revenueCanvas = document.querySelector('[data-credit-stats-chart="revenue"]');
    if (revenueCanvas && data.labels && data.labels.length) {
        new Chart(revenueCanvas, {
            type: 'bar',
            data: {
                labels: data.labels,
                datasets: [
                    {
                        type: 'bar',
                        label: 'Doanh thu (VND)',
                        data: data.paidVnd,
                        yAxisID: 'y',
                        order: 2
                    },
                    {
                        type: 'line',
                        label: 'Số đơn',
                        data: data.paidCount,
                        yAxisID: 'y1',
                        order: 1,
                        tension: 0.3
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                scales: {
                    y: { beginAtZero: true, position: 'left' },
                    y1: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, ticks: { precision: 0 } }
                }
            }
        });
    }

    const pkgCanvas = document.querySelector('[data-credit-stats-chart="packages"]');
    if (pkgCanvas && data.packages && data.packages.length) {
        new Chart(pkgCanvas, {
            type: 'doughnut',
            data: {
                labels: data.packages.map(p => p.label),
                datasets: [{ data: data.packages.map(p => p.vnd) }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }

    const statusCanvas = document.querySelector('[data-credit-stats-chart="status"]');
    if (statusCanvas && data.statuses && data.statuses.length) {
        new Chart(statusCanvas, {
            type: 'bar',
            data: {
                labels: data.statuses.map(s => s.label),
                datasets: [{ label: 'Đơn', data: data.statuses.map(s => s.count) }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
            }
        });
    }
}());
```

Reuse CSS tokens from admin dashboard if easy; functional charts first.

- [ ] **Step 4: Link from Admin Credits Index**

Near page title / actions:

```html
<a href="/Admin/Credits/Stats">Thống kê</a>
```

- [ ] **Step 5: Manual smoke**

```bash
dotnet build
# run app, open /Admin/Credits/Stats as admin
```

Expected: page renders; empty state without data; with seeded paid rows, charts show.

- [ ] **Step 6: Commit**

```bash
git add Areas/Admin/Controllers/CreditsController.cs Areas/Admin/Views/Credits/Stats.cshtml Areas/Admin/Views/Credits/Index.cshtml wwwroot/js/credit-purchase-stats-chart.js
git commit -m "feat(admin-credits): stats page for package purchase revenue"
```

---

### Task 5: User Stats page

**Files:**
- Modify: `Controllers/CreditsController.cs`
- Create: `Views/Credits/Stats.cshtml`
- Modify: `Views/Credits/Index.cshtml`

**Interfaces:**
- Route: `GET /Credits/Stats?from=&to=`
- Auth: existing `[Authorize]` on controller

- [ ] **Step 1: Controller**

```csharp
[HttpGet("Stats")]
public async Task<IActionResult> Stats(
    DateOnly? from,
    DateOnly? to,
    CancellationToken cancellationToken)
{
    string? userId = _currentUser.UserId;
    if (userId == null) return Challenge();
    return View(await _credits.GetPurchaseStatsAsync(userId, from, to, cancellationToken));
}
```

- [ ] **Step 2: View `Views/Credits/Stats.cshtml`**

Same filter + KPI (3 cards: tiền đã nạp, đơn paid, AOV) + revenue combo + package doughnut. **No** status chart, **no** pending KPI.

- Reuse `credit-purchase-stats-chart.js` (status canvas absent → JS skips)
- Style with existing `credits` CSS classes where possible; borrow admin chart card layout sparingly
- Empty CTA: link to `/Credits` mua gói
- Table without username column
- JSON same shape; `statuses: []`

- [ ] **Step 3: Link on user Credits index**

Near header “Tín dụng AI”:

```html
<a href="/Credits/Stats">Thống kê nạp</a>
```

- [ ] **Step 4: Run full credit tests + build**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~Credits"
dotnet build
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Controllers/CreditsController.cs Views/Credits/Stats.cshtml Views/Credits/Index.cshtml
git commit -m "feat(credits): user top-up stats page"
```

---

### Task 6: Spec coverage check + final verify

**Files:** none new (fix only if gaps)

- [ ] **Step 1: Checklist vs spec**

| Spec item | Task |
|-----------|------|
| Admin route `/Admin/Credits/Stats` | 4 |
| User route `/Credits/Stats` | 5 |
| Metrics B: VND + orders + AOV + pending admin | 1–5 |
| Range presets + max 365 + default 30 | 1, 4, 5 |
| Buckets day/week/month | 1 |
| Paid-only revenue | 1 |
| Status funnel admin | 1, 4 |
| Recent 20 table | 1, 4, 5 |
| User isolation | 3 |
| Entry links | 4, 5 |
| Chart.js no new deps | 4 |
| Service tests | 1–3 |

- [ ] **Step 2: Full test run**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

- [ ] **Step 3: Fix any failures; commit only if fixes needed**

```bash
git add -A  # only intentional fix files
git commit -m "fix(credits): close stats dashboard gaps from verify"
```

---

## Self-review (plan author)

1. **Spec coverage:** All approved sections mapped to tasks 1–6. Non-goals (API, export, overview embed) excluded.
2. **Placeholders:** None — concrete types, tests, routes, JS.
3. **Type consistency:** Single snapshot type `CreditPurchaseStatsSnapshot` end-to-end; `ToUtc` public on builder; `includeAdminExtras` gates pending/status.
4. **Ponytail:** One helper file, no third service class, views can bind snapshot directly, one shared JS file.

## Execution handoff

Plan saved to `docs/superpowers/plans/2026-08-06-credit-purchase-stats.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — this session with executing-plans checkpoints  

Which approach?
