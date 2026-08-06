# Credit Purchase Stats Dashboard — Design

**Date:** 2026-08-06  
**Status:** Approved  
**Scope:** Admin + user statistics pages for credit package purchase money (VND) and order counts.

## Goal

Ship clear, production-ready charts for money spent on credit packages:

- **Admin** sees platform revenue and checkout funnel health.
- **User** sees personal top-up spending.

Only **Paid** purchases count toward money metrics. Reuse existing Admin dashboard chart patterns (SSR + Chart.js embed).

## Non-goals

- Embedding full revenue charts into `/Admin` overview or the main Credits buy page
- Separate JSON API / SPA fetch layer
- CSV/export, period-over-period comparison, email reports
- Ledger/usage stats (mission-turn spend) — out of scope; purchases only
- Real-time websocket updates

## Audience & placement

| Surface | Route | Auth |
|--------|--------|------|
| Admin stats | `GET /Admin/Credits/Stats` | Existing Admin area policy |
| User stats | `GET /Credits/Stats` | Authenticated user |

**Entry points (no summary KPI widgets on old pages):**

- Nav / link “Thống kê” from Admin Credits index
- Link “Thống kê nạp” from user Credits account page
- Optional admin sidebar item next to Credits if the layout lists Credits

## Approach

**Extend existing credit services + two new controller actions/views** (not a new stats microservice, not client-fetched API).

- `IAdminCreditService` / `AdminCreditService` — global aggregates
- `ICreditService` / `CreditService` — per-user aggregates
- Shared pure helpers for range validation + bucketing (keep DRY without a third service class unless duplication hurts)
- Views mirror `/Admin` dashboard: filter UI, KPI cards, canvas charts, visually-hidden data tables, JSON script tag, Chart.js

## Metrics (group B)

### KPI cards

| KPI | Admin | User | Definition |
|-----|-------|------|------------|
| Total money (VND) | ✓ | ✓ (đã nạp) | `Sum(PriceVnd)` of Paid in range |
| Paid order count | ✓ | ✓ | `Count` of Paid in range |
| AOV | ✓ | ✓ | Total money / paid count; **0** if count = 0 |
| Pending count | ✓ | — | `Status == Pending` with `CreatedAtUtc` in range |

### Charts (Chart.js)

1. **Combo time series** — bars: VND; line: paid order count. X-axis buckets depend on range length.
2. **By package** — VND (and count in tooltip) grouped by `PackageName` snapshot on the purchase row.
3. **Admin only — status breakdown** — counts for Paid / Pending / Expired / Cancelled / Voided in range (funnel health, not revenue).

### Table

Latest **20** purchases whose **status timestamp** (same rules as status breakdown) falls in range, ordered by that timestamp descending:

- Admin: timestamp, username, package, VND, status  
- User: same without username  
- User table still includes non-Paid rows in range (e.g. pending/expired) so the user can see checkout outcomes; revenue charts remain Paid-only.  

### Empty state

No paid rows in range → hide combo/package charts, show message; user CTA to buy credits; admin CTA to package management.

## Time range & bucketing

**Timezone:** Vietnam (`AdminTimeZone.Vietnam`), same as existing admin dashboard.

**Controls:**

- Presets: 7 days, 30 days, this month, 90 days, year-to-date  
- Custom `from` / `to` (`DateOnly` query params)  
- **Max range:** 365 days  
- **Default:** last 30 days ending today (VN)

**Validation errors** (missing bound, `from > to`, span > 365): show warning and fall back to default 30-day window (same UX pattern as `AdminDashboardService`).

**Bucket granularity:**

| Range length (inclusive days) | Bucket |
|-------------------------------|--------|
| ≤ 31 | Day |
| ≤ 90 | Week (Monday-start, VN calendar) |
| > 90 | Month (`yyyy-MM`) |

Every bucket in the range appears on the axis; missing data = 0.

## Data rules

**Source table:** `CreditPurchases` only.

### Revenue / paid series / AOV / package breakdown

- Include only rows with paid semantics: `Status == Paid` (require `PaidAtUtc` when marking paid — existing payment flow sets it).
- Filter: `PaidAtUtc ∈ [startUtc, endUtcExclusive)` where bounds are VN midnight converted to UTC.
- Money never includes Pending, Expired, Cancelled, or Voided.

### Pending KPI (admin)

- `Status == Pending`
- Filter on `CreatedAtUtc` in the same half-open UTC window.

### Status breakdown (admin)

- Row included if its **status timestamp** falls in range:
  - Paid: `PaidAtUtc` (required)
  - Voided: `VoidedAtUtc` if set, else `PaidAtUtc` if set, else `CreatedAtUtc`
  - Pending / Expired / Cancelled / other: `CreatedAtUtc`
- Breakdown is informational; **do not** sum `PriceVnd` across non-Paid into revenue KPIs.

### User isolation

User stats query **must** constrain `UserId == currentUserId`. No cross-user leakage in aggregates or table.

### Performance

- `AsNoTracking()`  
- Load filtered rows for the range (or project needed columns) and aggregate in memory  
- Acceptable for current volume; switch to SQL `GroupBy` if purchase table grows large (`ponytail` note in code if useful)

## UI structure (both pages)

1. Title + date range filter (presets + from/to form)  
2. Optional range error alert  
3. KPI card row  
4. Chart grid: combo time series; package breakdown; admin status chart  
5. Recent purchases table  
6. `<script type="application/json">` chart payload + Chart.js section scripts  

Reuse admin dashboard CSS classes where they fit; user page follows existing Credits visual language (no need for a new design system).

## Error handling

| Case | Behavior |
|------|----------|
| Invalid / oversized range | Warning + default 30-day data |
| Unauthenticated user | Existing auth challenge |
| Non-admin on admin route | Existing Admin authorization |
| Chart.js unsupported | Visually-hidden HTML tables remain available |

No try/catch theater for empty data — empty collections drive empty states.

## Testing

**Automated (service-level):**

1. Three paid purchases across two days in a 7-day range → correct daily VND and counts  
2. Paid outside range excluded; Pending excluded from revenue  
3. User aggregate only includes that user’s rows  
4. Invalid range (>365 or from>to) → error flag + fallback window  
5. AOV is 0 with zero paid orders; package pie groups by `PackageName`

**Manual smoke:**

- Admin stats with mixed statuses  
- User cannot see another user’s purchases  
- Empty range empty-state  

**Out of scope for v1:** Chart.js visual regression, load tests.

## Files (expected touch set)

- `Services/Credits/AdminCreditService.cs` (+ contracts)  
- `Services/Credits/CreditService.cs` (+ contracts)  
- Shared range/bucket helper (small static class under `Services/Credits` or next to admin timezone reuse)  
- `Areas/Admin/Controllers/CreditsController.cs` — `Stats` action  
- `Controllers/CreditsController.cs` — `Stats` action  
- ViewModels under Admin models + user credit view models  
- `Areas/Admin/Views/Credits/Stats.cshtml`  
- `Views/Credits/Stats.cshtml`  
- Small JS (extend or twin `admin-dashboard-chart.js` for combo/package charts)  
- Nav links in admin credits + user credits views / admin layout  

No new NuGet packages. Chart.js already in wwwroot.

## Success criteria

- Admin can open stats and see paid VND over time, by package, plus pending/status funnel for a chosen range up to 365 days.  
- User can open stats and see only their paid top-ups with the same core charts (no status funnel).  
- Invalid ranges fail safe.  
- Service tests above pass.

## Implementation note

After this spec is confirmed on disk, create an implementation plan (`writing-plans`) before coding.
