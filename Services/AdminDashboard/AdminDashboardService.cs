using ltwnc.Areas.Admin;
using ltwnc.Areas.Admin.Models;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ltwnc.Services.AdminDashboard;

public sealed class AdminDashboardService
{
    public const int MaximumRangeDays = 31;
    private const int DefaultRangeDays = 7;
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(30);

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly AiProvidersOptions _aiProviders;

    public AdminDashboardService(
        AppDbContext context,
        TimeProvider timeProvider,
        IOptions<AiProvidersOptions> aiProviders)
    {
        _context = context;
        _timeProvider = timeProvider;
        _aiProviders = aiProviders.Value;
    }

    public async Task<AdminDashboardViewModel> GetAsync(
        DateOnly? requestedFrom,
        DateOnly? requestedTo,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(nowUtc).Date);
        (DateOnly from, DateOnly to, string? error) = ValidateRange(
            requestedFrom,
            requestedTo,
            today);

        DateTime startUtc = ToUtc(from, TimeOnly.MinValue);
        DateTime endUtcExclusive = ToUtc(to.AddDays(1), TimeOnly.MinValue);

        List<StudySessionRow> sessions = await _context.StudySessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= startUtc
                && session.StartedAt < endUtcExclusive)
            .Select(session => new StudySessionRow(session.StartedAt, session.CompletedAt))
            .ToListAsync(cancellationToken);

        List<DateTime> newUserCreatedAt = await _context.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.CreatedAt >= startUtc
                && profile.CreatedAt < endUtcExclusive)
            .Select(profile => profile.CreatedAt)
            .ToListAsync(cancellationToken);

        List<DateTime> reportCreatedAt = await _context.ContentReports
            .AsNoTracking()
            .Where(report => report.CreatedAtUtc >= startUtc
                && report.CreatedAtUtc < endUtcExclusive)
            .Select(report => report.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        int pendingReportCount = await _context.ContentReports
            .AsNoTracking()
            .CountAsync(report => report.Status == ContentReportStatus.Pending, cancellationToken);

        return new AdminDashboardViewModel
        {
            From = from,
            To = to,
            Today = today,
            RangeError = error,
            PendingReportCount = pendingReportCount,
            AiStatus = BuildAiStatus(_aiProviders),
            Activity = BuildActivity(from, to, sessions, nowUtc),
            NewUsers = BuildNewUsers(from, to, newUserCreatedAt),
            Reports = BuildReports(from, to, reportCreatedAt)
        };
    }

    private static (DateOnly From, DateOnly To, string? Error) ValidateRange(
        DateOnly? requestedFrom,
        DateOnly? requestedTo,
        DateOnly today)
    {
        if (!requestedFrom.HasValue && !requestedTo.HasValue)
        {
            return (today.AddDays(1 - DefaultRangeDays), today, null);
        }

        if (!requestedFrom.HasValue || !requestedTo.HasValue)
        {
            return (today, today, "Vui lòng chọn đủ ngày bắt đầu và ngày kết thúc.");
        }

        DateOnly from = requestedFrom.Value;
        DateOnly to = requestedTo.Value;
        if (from > to)
        {
            return (today, today, "Ngày bắt đầu không được sau ngày kết thúc.");
        }

        int dayCount = to.DayNumber - from.DayNumber + 1;
        if (dayCount > MaximumRangeDays)
        {
            return (today, today, $"Khoảng thời gian không được vượt quá {MaximumRangeDays} ngày.");
        }

        return (from, to, null);
    }

    private static IReadOnlyList<AdminDashboardActivityDay> BuildActivity(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<StudySessionRow> sessions,
        DateTime nowUtc)
    {
        var counts = new Dictionary<DateOnly, (int Completed, int Abandoned)>();
        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            counts[day] = (0, 0);
        }

        DateTime abandonedCutoffUtc = nowUtc - AbandonedAfter;
        foreach (StudySessionRow session in sessions)
        {
            DateOnly day = DateOnly.FromDateTime(
                AdminTimeZone.ToVietnamTime(session.StartedAt).Date);
            if (!counts.TryGetValue(day, out var count))
            {
                continue;
            }

            if (session.CompletedAt.HasValue)
            {
                count.Completed++;
            }
            else if (session.StartedAt < abandonedCutoffUtc)
            {
                count.Abandoned++;
            }

            counts[day] = count;
        }

        return counts.Select(item => new AdminDashboardActivityDay(
            item.Key,
            item.Value.Completed,
            item.Value.Abandoned)).ToArray();
    }

    private static IReadOnlyList<AdminDashboardNewUserDay> BuildNewUsers(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<DateTime> createdAtUtc)
    {
        var counts = new Dictionary<DateOnly, int>();
        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            counts[day] = 0;
        }

        foreach (DateTime createdAt in createdAtUtc)
        {
            DateOnly day = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(createdAt).Date);
            if (counts.ContainsKey(day))
            {
                counts[day]++;
            }
        }

        return counts.Select(item => new AdminDashboardNewUserDay(item.Key, item.Value)).ToArray();
    }

    private static IReadOnlyList<AdminDashboardReportDay> BuildReports(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<DateTime> createdAtUtc)
    {
        var counts = new Dictionary<DateOnly, int>();
        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            counts[day] = 0;
        }

        foreach (DateTime createdAt in createdAtUtc)
        {
            DateOnly day = DateOnly.FromDateTime(AdminTimeZone.ToVietnamTime(createdAt).Date);
            if (counts.ContainsKey(day))
            {
                counts[day]++;
            }
        }

        return counts.Select(item => new AdminDashboardReportDay(item.Key, item.Value)).ToArray();
    }

    private static AdminDashboardAiStatus BuildAiStatus(AiProvidersOptions options)
    {
        AiProviderOptions? provider = (options.Providers ?? [])
            .Where(candidate => candidate.IsEnabled)
            .OrderByDescending(candidate => candidate.IsPrimary)
            .ThenBy(candidate => candidate.Priority)
            .FirstOrDefault();

        if (provider == null)
        {
            return new AdminDashboardAiStatus(
                false,
                "AI chưa cấu hình",
                "Thêm provider trong appsettings.json.");
        }

        return new AdminDashboardAiStatus(
            true,
            "AI đã cấu hình",
            $"Provider đang ưu tiên: {provider.Name}.");
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, AdminTimeZone.Vietnam);
    }

    private sealed record StudySessionRow(DateTime StartedAt, DateTime? CompletedAt);

}
