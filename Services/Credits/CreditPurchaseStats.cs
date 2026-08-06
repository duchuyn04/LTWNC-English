using ltwnc.Areas.Admin;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Credits;

public sealed record CreditPurchaseStatsSnapshot(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    string? RangeError,
    string BucketKind,
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
        DateOnly todayVn)
    {
        if (!requestedFrom.HasValue && !requestedTo.HasValue)
            return DefaultWindow(todayVn, null);

        if (!requestedFrom.HasValue || !requestedTo.HasValue)
            return DefaultWindow(todayVn, "Vui lòng chọn đủ ngày bắt đầu và ngày kết thúc.");

        DateOnly from = requestedFrom.Value;
        DateOnly to = requestedTo.Value;
        if (from > to)
            return DefaultWindow(todayVn, "Ngày bắt đầu không được sau ngày kết thúc.");

        int dayCount = to.DayNumber - from.DayNumber + 1;
        if (dayCount > MaximumRangeDays)
            return DefaultWindow(todayVn, $"Khoảng thời gian không được vượt quá {MaximumRangeDays} ngày.");

        return (from, to, null);
    }

    public static CreditPurchaseStatsSnapshot Build(
        IReadOnlyList<CreditPurchaseStatsSourceRow> rows,
        DateOnly from,
        DateOnly to,
        DateOnly todayVn,
        string? rangeError,
        bool includeAdminExtras)
    {
        int dayCount = to.DayNumber - from.DayNumber + 1;
        string bucketKind = dayCount <= 31 ? "day" : dayCount <= 90 ? "week" : "month";

        DateTime startUtc = ToUtc(from, TimeOnly.MinValue);
        DateTime endUtc = ToUtc(to.AddDays(1), TimeOnly.MinValue);

        List<BucketSlot> slots = CreateSlots(from, to, bucketKind);
        Dictionary<string, int> slotIndex = slots
            .Select((slot, index) => (slot.Key, index))
            .ToDictionary(item => item.Key, item => item.index);

        long totalPaidVnd = 0;
        int paidOrderCount = 0;
        Dictionary<string, (long Vnd, int Count)> packages = new(StringComparer.Ordinal);
        Dictionary<string, int> statusCounts = new(StringComparer.Ordinal);
        int pendingCount = 0;
        List<(CreditPurchaseStatsSourceRow Row, DateTime Timestamp)> recentCandidates = [];

        foreach (CreditPurchaseStatsSourceRow row in rows)
        {
            DateTime statusTs = StatusTimestamp(row);
            if (statusTs >= startUtc && statusTs < endUtc)
            {
                recentCandidates.Add((row, statusTs));
                if (includeAdminExtras)
                {
                    statusCounts[row.Status] = statusCounts.GetValueOrDefault(row.Status) + 1;
                    if (row.Status == CreditPurchaseStatuses.Pending
                        && row.CreatedAtUtc >= startUtc
                        && row.CreatedAtUtc < endUtc)
                    {
                        pendingCount++;
                    }
                }
            }

            if (row.Status != CreditPurchaseStatuses.Paid || !row.PaidAtUtc.HasValue)
                continue;

            DateTime paidAt = row.PaidAtUtc.Value;
            if (paidAt < startUtc || paidAt >= endUtc)
                continue;

            DateOnly paidDay = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(paidAt).DateTime);
            string key = BucketKey(paidDay, bucketKind);
            if (!slotIndex.TryGetValue(key, out int index))
                continue;

            BucketSlot slot = slots[index];
            slots[index] = slot with { PaidVnd = slot.PaidVnd + row.PriceVnd, PaidCount = slot.PaidCount + 1 };
            totalPaidVnd += row.PriceVnd;
            paidOrderCount++;

            if (packages.TryGetValue(row.PackageName, out var pkg))
                packages[row.PackageName] = (pkg.Vnd + row.PriceVnd, pkg.Count + 1);
            else
                packages[row.PackageName] = (row.PriceVnd, 1);
        }

        // Pending KPI uses CreatedAt only (spec); recount cleanly for pending created in range
        if (includeAdminExtras)
        {
            pendingCount = rows.Count(row =>
                row.Status == CreditPurchaseStatuses.Pending
                && row.CreatedAtUtc >= startUtc
                && row.CreatedAtUtc < endUtc);
        }

        long aov = paidOrderCount == 0 ? 0 : totalPaidVnd / paidOrderCount;

        IReadOnlyList<CreditPurchaseStatsPackage> packageList = packages
            .Select(item => new CreditPurchaseStatsPackage(item.Key, item.Value.Vnd, item.Value.Count))
            .OrderByDescending(item => item.PaidVnd)
            .ThenBy(item => item.PackageName, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<CreditPurchaseStatsStatusCount>? statusList = includeAdminExtras
            ? statusCounts
                .Where(item => item.Value > 0)
                .OrderBy(item => StatusOrder(item.Key))
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new CreditPurchaseStatsStatusCount(item.Key, item.Value))
                .ToArray()
            : null;

        IReadOnlyList<CreditPurchaseStatsRow> recent = recentCandidates
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Row.Id)
            .Take(RecentRowLimit)
            .Select(item => new CreditPurchaseStatsRow(
                item.Row.Id,
                item.Row.UserName,
                item.Row.PackageName,
                item.Row.PriceVnd,
                item.Row.Status,
                item.Timestamp))
            .ToArray();

        return new CreditPurchaseStatsSnapshot(
            from,
            to,
            todayVn,
            rangeError,
            bucketKind,
            totalPaidVnd,
            paidOrderCount,
            aov,
            includeAdminExtras ? pendingCount : null,
            slots.Select(slot => new CreditPurchaseStatsBucket(slot.Key, slot.Label, slot.PaidVnd, slot.PaidCount)).ToArray(),
            packageList,
            statusList,
            recent);
    }

    public static DateTime StatusTimestamp(CreditPurchaseStatsSourceRow row)
    {
        if (row.Status == CreditPurchaseStatuses.Paid && row.PaidAtUtc.HasValue)
            return row.PaidAtUtc.Value;

        if (row.Status == CreditPurchaseStatuses.Voided)
            return row.VoidedAtUtc ?? row.PaidAtUtc ?? row.CreatedAtUtc;

        return row.CreatedAtUtc;
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, AdminTimeZone.Vietnam);
    }

    private static (DateOnly From, DateOnly To, string? Error) DefaultWindow(DateOnly todayVn, string? error)
    {
        return (todayVn.AddDays(1 - DefaultRangeDays), todayVn, error);
    }

    private static List<BucketSlot> CreateSlots(DateOnly from, DateOnly to, string bucketKind)
    {
        var slots = new List<BucketSlot>();
        if (bucketKind == "day")
        {
            for (DateOnly day = from; day <= to; day = day.AddDays(1))
                slots.Add(new BucketSlot(day.ToString("yyyy-MM-dd"), day.ToString("dd/MM"), 0, 0));
            return slots;
        }

        if (bucketKind == "week")
        {
            DateOnly cursor = StartOfWeekMonday(from);
            DateOnly last = StartOfWeekMonday(to);
            for (DateOnly week = cursor; week <= last; week = week.AddDays(7))
                slots.Add(new BucketSlot(week.ToString("yyyy-MM-dd"), week.ToString("dd/MM"), 0, 0));
            return slots;
        }

        DateOnly monthCursor = new(from.Year, from.Month, 1);
        DateOnly lastMonth = new(to.Year, to.Month, 1);
        for (DateOnly month = monthCursor; month <= lastMonth; month = month.AddMonths(1))
            slots.Add(new BucketSlot(month.ToString("yyyy-MM"), month.ToString("MM/yyyy"), 0, 0));
        return slots;
    }

    private static string BucketKey(DateOnly day, string bucketKind) =>
        bucketKind switch
        {
            "week" => StartOfWeekMonday(day).ToString("yyyy-MM-dd"),
            "month" => day.ToString("yyyy-MM"),
            _ => day.ToString("yyyy-MM-dd")
        };

    private static DateOnly StartOfWeekMonday(DateOnly day)
    {
        int offset = ((int)day.DayOfWeek + 6) % 7; // Monday=0
        return day.AddDays(-offset);
    }

    private static int StatusOrder(string status) => status switch
    {
        CreditPurchaseStatuses.Pending => 0,
        CreditPurchaseStatuses.Paid => 1,
        CreditPurchaseStatuses.Expired => 2,
        CreditPurchaseStatuses.Cancelled => 3,
        CreditPurchaseStatuses.Failed => 4,
        CreditPurchaseStatuses.Voided => 5,
        _ => 99
    };

    private sealed record BucketSlot(string Key, string Label, long PaidVnd, int PaidCount);
}
