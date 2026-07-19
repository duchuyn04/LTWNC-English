using Microsoft.EntityFrameworkCore;
using ltwnc.Areas.Admin;
using ltwnc.Areas.Admin.Models;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.AdminDashboard;

public interface IAdminDashboardKpiService
{
    Task<AdminDashboardSnapshot> GetSnapshotAsync(int? days, CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardKpiService : IAdminDashboardKpiService
{
    private static readonly int[] AllowedDays = [7, 30, 90];
    private const int RecentActiveSessionMinutes = 30;
    private const int MinimumAiSampleSize = 20;
    private const int EnglishMissionModeValue = 5;
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AdminDashboardKpiService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<AdminDashboardSnapshot> GetSnapshotAsync(
        int? days,
        CancellationToken cancellationToken = default)
    {
        int requestedDays = 30;
        if (days.HasValue)
        {
            requestedDays = days.Value;
        }

        int selectedDays = 30;
        if (AllowedDays.Contains(requestedDays))
        {
            selectedDays = requestedDays;
        }

        AdminDashboardPeriod current = BuildPeriod(selectedDays, 0);
        AdminDashboardPeriod previous = BuildPeriod(selectedDays, -selectedDays);

        PeriodMetricSet currentMetrics = await LoadMetricsAsync(current, cancellationToken);
        PeriodMetricSet previousMetrics = await LoadMetricsAsync(previous, cancellationToken);

        return new AdminDashboardSnapshot(
            selectedDays,
            current,
            previous,
            currentMetrics,
            previousMetrics,
            AdminTimeZone.ToVietnamTime(_timeProvider.GetUtcNow().UtcDateTime));
    }

    public static AdminDashboardViewModel ToViewModel(AdminDashboardSnapshot snapshot)
    {
        return new AdminDashboardViewModel
        {
            Days = snapshot.Days,
            PeriodStartVietnam = snapshot.Current.StartVietnam,
            PeriodEndVietnam = snapshot.Current.EndVietnamExclusive.AddTicks(-1),
            GeneratedAtVietnam = snapshot.GeneratedAtVietnam,
            Kpis =
            [
                CountCard(
                    "Người dùng hoạt động",
                    snapshot.CurrentMetrics.ActiveUsers,
                    snapshot.PreviousMetrics.ActiveUsers,
                    "người dùng có hoạt động học",
                    "ph-users-three"),
                CountCard(
                    "Đăng ký mới",
                    snapshot.CurrentMetrics.NewRegistrations,
                    snapshot.PreviousMetrics.NewRegistrations,
                    "hồ sơ tạo trong khoảng",
                    "ph-user-plus"),
                CountCard(
                    "Phiên học",
                    snapshot.CurrentMetrics.StudySessions,
                    snapshot.PreviousMetrics.StudySessions,
                    "phiên bắt đầu trong khoảng",
                    "ph-graduation-cap"),
                PercentCard(
                    "Tỷ lệ hoàn thành",
                    snapshot.CurrentMetrics.CompletionRatePercent,
                    snapshot.PreviousMetrics.CompletionRatePercent,
                    snapshot.CurrentMetrics.CompletionRateDenominator,
                    "loại phiên đang học dưới 30 phút",
                    "ph-check-circle"),
                CountCard(
                    "Nhiệm vụ tiếng Anh",
                    snapshot.CurrentMetrics.EnglishMissions,
                    snapshot.PreviousMetrics.EnglishMissions,
                    "mission tạo trong khoảng",
                    "ph-chats-circle"),
                AiErrorCard(
                    snapshot.CurrentMetrics.AiErrorRatePercent,
                    snapshot.PreviousMetrics.AiErrorRatePercent,
                    snapshot.CurrentMetrics.AiSampleSize,
                    snapshot.PreviousMetrics.AiSampleSize)
            ]
        };
    }

    private async Task<PeriodMetricSet> LoadMetricsAsync(
        AdminDashboardPeriod period,
        CancellationToken cancellationToken)
    {
        DateTime startUtc = period.StartUtc;
        DateTime endUtc = period.EndUtcExclusive;
        DateTime activeSessionCutoffUtc = period.EvaluationUtc.AddMinutes(-RecentActiveSessionMinutes);

        IQueryable<StudySession> sessionsInPeriod = _context.StudySessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= startUtc && session.StartedAt < endUtc);

        int studySessions = await sessionsInPeriod.CountAsync(cancellationToken);
        int eligibleSessions = await sessionsInPeriod
            .CountAsync(session => session.CompletedAt.HasValue || session.StartedAt < activeSessionCutoffUtc, cancellationToken);
        int completedSessions = await sessionsInPeriod
            .CountAsync(session => session.CompletedAt.HasValue, cancellationToken);

        // Đếm distinct trên database để một người học nhiều lần vẫn chỉ là một người dùng hoạt động.
        IQueryable<string> usersFromSessions = _context.StudySessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= startUtc && session.StartedAt < endUtc)
            .Select(session => session.UserId);
        IQueryable<string> usersFromProgress = _context.UserProgresses
            .AsNoTracking()
            .Where(progress => progress.LastReviewed >= startUtc && progress.LastReviewed < endUtc)
            .Select(progress => progress.UserId);
        int activeUsers = await usersFromSessions
            .Union(usersFromProgress)
            .CountAsync(cancellationToken);

        int newRegistrations = await _context.UserProfiles
            .AsNoTracking()
            .CountAsync(profile => profile.CreatedAt >= startUtc && profile.CreatedAt < endUtc, cancellationToken);

        int englishMissions = await _context.StudySessions
            .AsNoTracking()
            .CountAsync(session => session.Mode == (StudyMode)EnglishMissionModeValue
                && session.StartedAt >= startUtc
                && session.StartedAt < endUtc, cancellationToken);

        AiOperationAggregate? aiAggregate = await _context.AiOperationLogs
            .AsNoTracking()
            .Where(log => log.OccurredAtUtc >= startUtc && log.OccurredAtUtc < endUtc)
            .GroupBy(_ => 1)
            .Select(group => new AiOperationAggregate(
                group.Count(),
                group.Count(log => !log.Succeeded)))
            .FirstOrDefaultAsync(cancellationToken);

        AiOperationAggregate ai = new(0, 0);
        if (aiAggregate != null)
        {
            ai = aiAggregate;
        }

        decimal? completionRatePercent = null;
        if (eligibleSessions > 0)
        {
            decimal rawCompletionRate = completedSessions * 100m / eligibleSessions;
            completionRatePercent = decimal.Round(rawCompletionRate, 1);
        }

        decimal? aiErrorRatePercent = null;
        if (ai.TotalRequests >= MinimumAiSampleSize)
        {
            decimal rawAiErrorRate = ai.FailedRequests * 100m / ai.TotalRequests;
            aiErrorRatePercent = decimal.Round(rawAiErrorRate, 1);
        }

        return new PeriodMetricSet(
            activeUsers,
            newRegistrations,
            studySessions,
            completionRatePercent,
            eligibleSessions,
            englishMissions,
            aiErrorRatePercent,
            ai.TotalRequests);
    }

    private AdminDashboardPeriod BuildPeriod(int days, int offsetDays)
    {
        DateTimeOffset nowVietnam = AdminTimeZone.ToVietnamTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTime endLocalDate = nowVietnam.Date.AddDays(1 + offsetDays);
        DateTime startLocalDate = endLocalDate.AddDays(-days);

        // Ranh giới ngày được tính theo Việt Nam rồi mới đổi sang UTC để query dữ liệu lưu trữ.
        DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(startLocalDate, DateTimeKind.Unspecified),
            AdminTimeZone.Vietnam);
        DateTime endUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(endLocalDate, DateTimeKind.Unspecified),
            AdminTimeZone.Vietnam);

        DateTime evaluationUtc = endUtc;
        if (offsetDays == 0)
        {
            evaluationUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }

        return new AdminDashboardPeriod(
            startUtc,
            endUtc,
            evaluationUtc,
            new DateTimeOffset(startLocalDate, AdminTimeZone.Vietnam.GetUtcOffset(startLocalDate)),
            new DateTimeOffset(endLocalDate, AdminTimeZone.Vietnam.GetUtcOffset(endLocalDate)));
    }

    private static AdminDashboardKpiCardViewModel CountCard(
        string label,
        int current,
        int previous,
        string detail,
        string icon)
    {
        int delta = current - previous;
        string value = current.ToString("N0");
        string comparison = FormatCountComparison(delta);
        string tone = DeltaTone(delta, lowerIsBetter: false);

        return new AdminDashboardKpiCardViewModel
        {
            Label = label,
            Value = value,
            Detail = detail,
            Comparison = comparison,
            Tone = tone,
            Icon = icon
        };
    }

    private static AdminDashboardKpiCardViewModel PercentCard(
        string label,
        decimal? current,
        decimal? previous,
        int denominator,
        string detail,
        string icon)
    {
        decimal? delta = CalculatePercentDelta(current, previous);

        string value = "Chưa có dữ liệu";
        if (current.HasValue)
        {
            value = $"{current:0.#}%";
        }

        string resolvedDetail = detail;
        if (denominator == 0)
        {
            resolvedDetail = "chưa có phiên đủ điều kiện";
        }

        string tone = "neutral";
        if (delta.HasValue)
        {
            tone = DeltaTone(delta.Value, lowerIsBetter: false);
        }

        return new AdminDashboardKpiCardViewModel
        {
            Label = label,
            Value = value,
            Detail = resolvedDetail,
            Comparison = FormatPercentComparison(delta, previous),
            Tone = tone,
            Icon = icon
        };
    }

    private static AdminDashboardKpiCardViewModel AiErrorCard(
        decimal? current,
        decimal? previous,
        int currentSample,
        int previousSample)
    {
        decimal? delta = CalculatePercentDelta(current, previous);

        string value = "Chưa đủ dữ liệu";
        if (current.HasValue)
        {
            value = $"{current:0.#}%";
        }

        string detail;
        if (currentSample < MinimumAiSampleSize)
        {
            detail = $"{currentSample:N0}/{MinimumAiSampleSize:N0} yêu cầu tối thiểu";
        }
        else
        {
            detail = $"{currentSample:N0} yêu cầu AI";
        }

        bool previousInsufficient = previousSample < MinimumAiSampleSize;

        string tone = "neutral";
        if (delta.HasValue)
        {
            tone = DeltaTone(delta.Value, lowerIsBetter: true);
        }

        return new AdminDashboardKpiCardViewModel
        {
            Label = "Tỷ lệ lỗi AI",
            Value = value,
            Detail = detail,
            Comparison = FormatPercentComparison(delta, previous, previousInsufficient),
            Tone = tone,
            Icon = "ph-warning-circle"
        };
    }

    private static string FormatCountComparison(int delta)
    {
        if (delta == 0) return "Không đổi so với kỳ trước";
        if (delta > 0) return $"+{delta:N0} so với kỳ trước";
        return $"{delta:N0} so với kỳ trước";
    }

    private static string FormatPercentComparison(
        decimal? delta,
        decimal? previous,
        bool previousInsufficient = false)
    {
        if (previousInsufficient) return "Kỳ trước chưa đủ dữ liệu";
        if (!delta.HasValue || !previous.HasValue) return "Chưa có kỳ so sánh";
        if (delta.Value == 0) return "Không đổi so với kỳ trước";
        if (delta.Value > 0) return $"+{delta:0.#} điểm % so với kỳ trước";
        return $"{delta:0.#} điểm % so với kỳ trước";
    }

    private static string DeltaTone(decimal delta, bool lowerIsBetter)
    {
        if (delta == 0) return "neutral";
        bool isPositiveSignal;
        if (lowerIsBetter)
        {
            isPositiveSignal = delta < 0;
        }
        else
        {
            isPositiveSignal = delta > 0;
        }

        if (isPositiveSignal) return "positive";
        return "negative";
    }

    private static decimal? CalculatePercentDelta(decimal? current, decimal? previous)
    {
        if (!current.HasValue || !previous.HasValue)
        {
            return null;
        }

        return current.Value - previous.Value;
    }
}

public sealed record AdminDashboardPeriod(
    DateTime StartUtc,
    DateTime EndUtcExclusive,
    DateTime EvaluationUtc,
    DateTimeOffset StartVietnam,
    DateTimeOffset EndVietnamExclusive);

public sealed record PeriodMetricSet(
    int ActiveUsers,
    int NewRegistrations,
    int StudySessions,
    decimal? CompletionRatePercent,
    int CompletionRateDenominator,
    int EnglishMissions,
    decimal? AiErrorRatePercent,
    int AiSampleSize);

public sealed record AdminDashboardSnapshot(
    int Days,
    AdminDashboardPeriod Current,
    AdminDashboardPeriod Previous,
    PeriodMetricSet CurrentMetrics,
    PeriodMetricSet PreviousMetrics,
    DateTimeOffset GeneratedAtVietnam);

internal sealed record AiOperationAggregate(int TotalRequests, int FailedRequests);
