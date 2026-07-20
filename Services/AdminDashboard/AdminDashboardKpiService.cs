using ltwnc.Areas.Admin;
using ltwnc.Areas.Admin.Models;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.AdminDashboard;

public interface IAdminDashboardKpiService
{
    // Lấy snapshot KPI server-side cho trang dashboard ban đầu.
    Task<AdminDashboardSnapshot> GetSnapshotAsync(int? days, CancellationToken cancellationToken = default);

    // Lấy snapshot JSON cho AJAX, gồm KPI và các cảnh báo vận hành an toàn.
    Task<AdminDashboardLiveSnapshot> GetLiveSnapshotAsync(int? days, CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardKpiService : IAdminDashboardKpiService
{
    private static readonly int[] AllowedDays = [7, 30, 90];
    private const int RecentActiveSessionMinutes = 30;
    private const int MinimumAiSampleSize = 20;
    private const int DefaultAiHealthWindowMinutes = 5;
    private const decimal DefaultAiErrorRateThresholdPercent = 10m;
    private const int DefaultAiUnstableFailureThreshold = 3;
    private const int OverdueContentReportHours = 24;
    private const int EnglishMissionModeValue = 5;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration? _configuration;

    // Nhận DbContext, đồng hồ và cấu hình để mọi số liệu dùng cùng một mốc thời gian.
    public AdminDashboardKpiService(
        AppDbContext context,
        TimeProvider timeProvider,
        IConfiguration? configuration = null)
    {
        _context = context;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    // Tạo snapshot KPI theo khoảng 7/30/90 ngày, mặc định 30 ngày nếu input không hợp lệ.
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

    // Tạo snapshot JSON cho AJAX, chỉ gồm dữ liệu tổng hợp an toàn và cảnh báo có thể hành động.
    public async Task<AdminDashboardLiveSnapshot> GetLiveSnapshotAsync(
        int? days,
        CancellationToken cancellationToken = default)
    {
        AdminDashboardSnapshot snapshot = await GetSnapshotAsync(days, cancellationToken);
        AdminDashboardViewModel viewModel = ToViewModel(snapshot);
        AdminDashboardAiStatus aiStatus = await LoadAiStatusAsync(cancellationToken);
        AdminDashboardContentReportStatus contentReports =
            await LoadContentReportStatusAsync(cancellationToken);
        bool hasAchievementFailure = await HasCurrentAchievementSyncFailureAsync(cancellationToken);
        IReadOnlyList<AdminDashboardAlert> alerts = BuildAlerts(
            aiStatus,
            contentReports,
            hasAchievementFailure);

        return new AdminDashboardLiveSnapshot(
            viewModel.Days,
            new AdminDashboardLivePeriod(
                viewModel.PeriodStartVietnam,
                viewModel.PeriodEndVietnam,
                viewModel.GeneratedAtVietnam),
            viewModel.Kpis,
            aiStatus,
            contentReports,
            alerts);
    }

    // Chuyển snapshot nghiệp vụ sang view model server-rendered cho Razor.
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
                    "Đang hoạt động",
                    snapshot.CurrentMetrics.ActiveUsers,
                    snapshot.PreviousMetrics.ActiveUsers,
                    "ph-users-three",
                    "Xem người dùng",
                    "/Admin/Users"),
                CountCard(
                    "Mới đăng ký",
                    snapshot.CurrentMetrics.NewRegistrations,
                    snapshot.PreviousMetrics.NewRegistrations,
                    "ph-user-plus",
                    "Xem người dùng",
                    "/Admin/Users"),
                CountCard(
                    "Phiên học",
                    snapshot.CurrentMetrics.StudySessions,
                    snapshot.PreviousMetrics.StudySessions,
                    "ph-graduation-cap",
                    "Xem phiên học",
                    "/Admin/Learning"),
                PercentCard(
                    "Hoàn thành",
                    snapshot.CurrentMetrics.CompletionRatePercent,
                    snapshot.PreviousMetrics.CompletionRatePercent,
                    snapshot.CurrentMetrics.CompletionRateDenominator,
                    "ph-check-circle",
                    "Xem phiên học",
                    "/Admin/Learning"),
                CountCard(
                    "Nhiệm vụ",
                    snapshot.CurrentMetrics.EnglishMissions,
                    snapshot.PreviousMetrics.EnglishMissions,
                    "ph-chats-circle",
                    "Xem nhiệm vụ",
                    "/Admin/EnglishMissions"),
                AiErrorCard(
                    snapshot.CurrentMetrics.AiErrorRatePercent,
                    snapshot.PreviousMetrics.AiErrorRatePercent,
                    snapshot.CurrentMetrics.AiSampleSize,
                    snapshot.PreviousMetrics.AiSampleSize)
            ]
        };
    }

    // Tải các metric KPI chính trong một khoảng thời gian đã đổi sang UTC.
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
            .CountAsync(session => session.CompletedAt.HasValue
                || session.StartedAt < activeSessionCutoffUtc, cancellationToken);
        int completedSessions = await sessionsInPeriod
            .CountAsync(session => session.CompletedAt.HasValue, cancellationToken);

        // Đếm distinct trên database để một người học nhiều lần vẫn chỉ là một active user.
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

    // Đọc trạng thái AI từ provider hiện tại và log vận hành trong cửa sổ cấu hình.
    private async Task<AdminDashboardAiStatus> LoadAiStatusAsync(CancellationToken cancellationToken)
    {
        int healthWindowMinutes = ReadAiHealthWindowMinutes();
        int minimumSampleSize = ReadAiMinimumSampleSize();
        decimal thresholdPercent = ReadAiErrorRateThresholdPercent();
        int unstableFailureThreshold = ReadAiUnstableFailureThreshold();
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime windowStartUtc = nowUtc.AddMinutes(-healthWindowMinutes);

        List<AiProvider> providers = await _context.AiProviders
            .AsNoTracking()
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
        AiProvider? primaryProvider = providers.FirstOrDefault(provider => provider.IsPrimary);

        AiOperationAggregate? aggregate = await _context.AiOperationLogs
            .AsNoTracking()
            .Where(log => log.OccurredAtUtc >= windowStartUtc && log.OccurredAtUtc <= nowUtc)
            .GroupBy(_ => 1)
            .Select(group => new AiOperationAggregate(
                group.Count(),
                group.Count(log => !log.Succeeded)))
            .FirstOrDefaultAsync(cancellationToken);

        int totalRequests = 0;
        int failedRequests = 0;
        if (aggregate != null)
        {
            totalRequests = aggregate.TotalRequests;
            failedRequests = aggregate.FailedRequests;
        }

        decimal? errorRatePercent = null;
        bool errorRateExceeded = false;
        if (totalRequests >= minimumSampleSize)
        {
            decimal rawRate = failedRequests * 100m / totalRequests;
            errorRatePercent = decimal.Round(rawRate, 1);
            errorRateExceeded = errorRatePercent.Value > thresholdPercent;
        }

        bool primaryIsUnstable = IsPrimaryProviderUnstable(
            primaryProvider,
            unstableFailureThreshold);
        int readyProviders = providers.Count(provider =>
            provider.IsEnabled && provider.LastCheckSucceeded == true);
        int unstableProviders = providers.Count(provider =>
            provider.ConsecutiveFailureCount >= unstableFailureThreshold);
        string summary = BuildAiSummary(
            primaryProvider,
            primaryIsUnstable,
            errorRateExceeded,
            readyProviders,
            providers.Count);

        return new AdminDashboardAiStatus(
            summary,
            providers.Count,
            readyProviders,
            unstableProviders,
            primaryProvider?.Name,
            primaryIsUnstable,
            errorRatePercent,
            totalRequests,
            minimumSampleSize,
            thresholdPercent,
            errorRateExceeded);
    }

    // Đếm báo cáo đang chờ và báo cáo đã quá hạn 24 giờ để tạo cảnh báo xử lý nội dung.
    private async Task<AdminDashboardContentReportStatus> LoadContentReportStatusAsync(
        CancellationToken cancellationToken)
    {
        DateTime overdueCutoffUtc = _timeProvider.GetUtcNow().UtcDateTime
            .AddHours(-OverdueContentReportHours);
        int pendingCount = await _context.ContentReports
            .AsNoTracking()
            .CountAsync(report => report.Status == ContentReportStatus.Pending, cancellationToken);
        int overdueCount = await _context.ContentReports
            .AsNoTracking()
            .CountAsync(report => report.Status == ContentReportStatus.Pending
                && report.CreatedAtUtc <= overdueCutoffUtc, cancellationToken);

        return new AdminDashboardContentReportStatus(pendingCount, overdueCount);
    }

    // Chỉ cảnh báo đồng bộ thành tích khi audit mới nhất của resync là Failure.
    private async Task<bool> HasCurrentAchievementSyncFailureAsync(CancellationToken cancellationToken)
    {
        AdminAuditLog? latestAchievementSync = await _context.AdminAuditLogs
            .AsNoTracking()
            .Where(log => log.Action == AdminAuditActions.AchievementsResyncUser
                || log.Action == AdminAuditActions.AchievementsResyncAll)
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAchievementSync == null)
        {
            return false;
        }

        return latestAchievementSync.Outcome == AdminAuditOutcome.Failure;
    }

    // Ghép các tín hiệu vận hành thành danh sách cảnh báo không cần nút đóng thủ công.
    private static IReadOnlyList<AdminDashboardAlert> BuildAlerts(
        AdminDashboardAiStatus aiStatus,
        AdminDashboardContentReportStatus contentReports,
        bool hasAchievementFailure)
    {
        List<AdminDashboardAlert> alerts = new();
        if (aiStatus.PrimaryProviderUnstable)
        {
            alerts.Add(new AdminDashboardAlert(
                "ai-primary-unstable",
                "danger",
                "Nhà cung cấp AI chính không ổn định",
                "Nhà cung cấp chính chưa sẵn sàng hoặc đã lỗi kiểm tra trạng thái liên tiếp.",
                "Kiểm tra nhà cung cấp",
                "/Admin/AiProviders"));
        }

        if (aiStatus.ErrorRateExceeded)
        {
            string detail = $"Tỷ lệ lỗi AI {aiStatus.ErrorRatePercent:0.#}% vượt ngưỡng {aiStatus.ErrorRateThresholdPercent:0.#}% trong cửa sổ gần nhất.";
            alerts.Add(new AdminDashboardAlert(
                "ai-error-rate",
                "danger",
                "Tỷ lệ lỗi AI vượt ngưỡng",
                detail,
                "Mở nhà cung cấp AI",
                "/Admin/AiProviders"));
        }

        if (contentReports.OverdueCount > 0)
        {
            string detail = $"{contentReports.OverdueCount:N0} báo cáo đang chờ quá 24 giờ.";
            alerts.Add(new AdminDashboardAlert(
                "content-report-overdue",
                "warning",
                "Báo cáo nội dung quá hạn",
                detail,
                "Xem hàng đợi",
                "/Admin/ContentReports?sort=oldest"));
        }

        if (hasAchievementFailure)
        {
            alerts.Add(new AdminDashboardAlert(
                "achievement-resync-failed",
                "warning",
                "Đồng bộ thành tích thất bại",
                "Lần đồng bộ thành tích gần nhất thất bại và cần kiểm tra audit.",
                "Xem thành tích",
                "/Admin/Achievements"));
        }

        return alerts;
    }

    // Xem nhà cung cấp chính là không ổn định nếu bị thiếu, bị tắt hoặc kiểm tra trạng thái thất bại liên tiếp.
    private static bool IsPrimaryProviderUnstable(
        AiProvider? primaryProvider,
        int unstableFailureThreshold)
    {
        if (primaryProvider == null)
        {
            return true;
        }

        if (!primaryProvider.IsEnabled)
        {
            return true;
        }

        if (primaryProvider.LastCheckSucceeded != true)
        {
            return true;
        }

        return primaryProvider.ConsecutiveFailureCount >= unstableFailureThreshold;
    }

    // Tóm tắt trạng thái AI bằng tiếng Việt nhưng không đưa lỗi kỹ thuật thô ra dashboard.
    private static string BuildAiSummary(
        AiProvider? primaryProvider,
        bool primaryIsUnstable,
        bool errorRateExceeded,
        int readyProviders,
        int totalProviders)
    {
        if (totalProviders == 0)
        {
            return "Chưa cấu hình nhà cung cấp AI";
        }

        if (primaryIsUnstable)
        {
            return "Nhà cung cấp AI chính cần kiểm tra";
        }

        if (errorRateExceeded)
        {
            return "Tỷ lệ lỗi AI đang vượt ngưỡng";
        }

        if (readyProviders == 0)
        {
            return "Không có nhà cung cấp AI sẵn sàng";
        }

        string primaryName = primaryProvider?.Name ?? "Nhà cung cấp chính";
        return $"{primaryName} đang sẵn sàng";
    }

    // Dựng ranh giới ngày theo múi giờ Việt Nam rồi đổi sang UTC để query dữ liệu lưu trữ.
    private AdminDashboardPeriod BuildPeriod(int days, int offsetDays)
    {
        DateTimeOffset nowVietnam = AdminTimeZone.ToVietnamTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTime endLocalDate = nowVietnam.Date.AddDays(1 + offsetDays);
        DateTime startLocalDate = endLocalDate.AddDays(-days);

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

    // Tạo KPI dạng số đếm và so sánh với kỳ trước.
    private static AdminDashboardKpiCardViewModel CountCard(
        string label,
        int current,
        int previous,
        string icon,
        string actionLabel,
        string actionHref)
    {
        int delta = current - previous;
        string value = current.ToString("N0");
        string comparison = FormatCountComparison(delta);
        string tone = DeltaTone(delta, lowerIsBetter: false);

        return new AdminDashboardKpiCardViewModel
        {
            Label = label,
            Value = value,
            Detail = string.Empty,
            Comparison = comparison,
            Tone = tone,
            Icon = icon,
            ActionLabel = actionLabel,
            ActionHref = actionHref
        };
    }

    // Tạo KPI dạng phần trăm, giữ trạng thái chưa có dữ liệu khi mẫu rỗng.
    private static AdminDashboardKpiCardViewModel PercentCard(
        string label,
        decimal? current,
        decimal? previous,
        int denominator,
        string icon,
        string actionLabel,
        string actionHref)
    {
        decimal? delta = CalculatePercentDelta(current, previous);

        string value = "—";
        if (current.HasValue)
        {
            value = $"{current:0.#}%";
        }

        string resolvedDetail = denominator == 0 || !current.HasValue
            ? "Chưa đủ dữ liệu"
            : string.Empty;

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
            Icon = icon,
            ActionLabel = actionLabel,
            ActionHref = actionHref
        };
    }

    // Tạo KPI tỷ lệ lỗi AI với ngưỡng mẫu tối thiểu để tránh hiển thị 0% gây hiểu nhầm.
    private static AdminDashboardKpiCardViewModel AiErrorCard(
        decimal? current,
        decimal? previous,
        int currentSample,
        int previousSample)
    {
        decimal? delta = CalculatePercentDelta(current, previous);

        string value = "—";
        if (current.HasValue)
        {
            value = $"{current:0.#}%";
        }

        string detail;
        if (currentSample < MinimumAiSampleSize)
        {
            detail = "Chưa đủ dữ liệu";
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
            Label = "Lỗi AI",
            Value = value,
            Detail = detail,
            Comparison = FormatPercentComparison(delta, previous, previousInsufficient),
            Tone = tone,
            Icon = "ph-warning-circle",
            ActionLabel = "Kiểm tra AI",
            ActionHref = "/Admin/AiProviders"
        };
    }

    // Định dạng so sánh số đếm với kỳ trước bằng tiếng Việt.
    private static string FormatCountComparison(int delta)
    {
        if (delta == 0)
        {
            return string.Empty;
        }

        if (delta > 0)
        {
            return $"+{delta:N0} so với kỳ trước";
        }

        return $"{delta:N0} so với kỳ trước";
    }

    // Định dạng so sánh phần trăm và giữ trạng thái thiếu mẫu rõ ràng.
    private static string FormatPercentComparison(
        decimal? delta,
        decimal? previous,
        bool previousInsufficient = false)
    {
        if (previousInsufficient)
        {
            return string.Empty;
        }

        if (!delta.HasValue || !previous.HasValue)
        {
            return string.Empty;
        }

        if (delta.Value == 0)
        {
            return string.Empty;
        }

        if (delta.Value > 0)
        {
            return $"+{delta:0.#} điểm % so với kỳ trước";
        }

        return $"{delta:0.#} điểm % so với kỳ trước";
    }

    // Quy đổi delta thành tone giao diện; một số chỉ số càng thấp càng tốt.
    private static string DeltaTone(decimal delta, bool lowerIsBetter)
    {
        if (delta == 0)
        {
            return "neutral";
        }

        bool isPositiveSignal;
        if (lowerIsBetter)
        {
            isPositiveSignal = delta < 0;
        }
        else
        {
            isPositiveSignal = delta > 0;
        }

        if (isPositiveSignal)
        {
            return "positive";
        }

        return "negative";
    }

    // Tính chênh lệch phần trăm; thiếu một vế thì không so sánh.
    private static decimal? CalculatePercentDelta(decimal? current, decimal? previous)
    {
        if (!current.HasValue || !previous.HasValue)
        {
            return null;
        }

        return current.Value - previous.Value;
    }

    // Đọc cửa sổ health AI từ cấu hình; giá trị sai quay về 5 phút.
    private int ReadAiHealthWindowMinutes()
    {
        int value = DefaultAiHealthWindowMinutes;
        int? configuredValue = _configuration?.GetValue<int?>("AiProviders:Health:WindowMinutes");
        if (configuredValue.HasValue)
        {
            value = configuredValue.Value;
        }

        return Math.Clamp(value, 1, 60);
    }

    // Đọc số mẫu tối thiểu từ cấu hình; dùng chung contract dashboard và cảnh báo.
    private int ReadAiMinimumSampleSize()
    {
        int value = MinimumAiSampleSize;
        int? configuredValue = _configuration?.GetValue<int?>("AiProviders:Health:MinimumSampleSize");
        if (configuredValue.HasValue)
        {
            value = configuredValue.Value;
        }

        return Math.Clamp(value, 1, 10_000);
    }

    // Đọc ngưỡng tỷ lệ lỗi AI từ cấu hình hệ thống.
    private decimal ReadAiErrorRateThresholdPercent()
    {
        decimal value = DefaultAiErrorRateThresholdPercent;
        decimal? configuredValue =
            _configuration?.GetValue<decimal?>("AiProviders:Health:ErrorRateThresholdPercent");
        if (configuredValue.HasValue)
        {
            value = configuredValue.Value;
        }

        return Math.Clamp(value, 0m, 100m);
    }

    // Đọc ngưỡng fail health check liên tiếp để đánh dấu provider không ổn định.
    private int ReadAiUnstableFailureThreshold()
    {
        int value = DefaultAiUnstableFailureThreshold;
        int? configuredValue =
            _configuration?.GetValue<int?>("AiProviders:Health:UnstableFailureThreshold");
        if (configuredValue.HasValue)
        {
            value = configuredValue.Value;
        }

        return Math.Clamp(value, 1, 100);
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

public sealed record AdminDashboardLiveSnapshot(
    int Days,
    AdminDashboardLivePeriod Period,
    IReadOnlyList<AdminDashboardKpiCardViewModel> Kpis,
    AdminDashboardAiStatus AiStatus,
    AdminDashboardContentReportStatus ContentReports,
    IReadOnlyList<AdminDashboardAlert> Alerts);

public sealed record AdminDashboardLivePeriod(
    DateTimeOffset StartVietnam,
    DateTimeOffset EndVietnam,
    DateTimeOffset GeneratedAtVietnam);

public sealed record AdminDashboardAiStatus(
    string Summary,
    int TotalProviders,
    int ReadyProviders,
    int UnstableProviders,
    string? PrimaryProviderName,
    bool PrimaryProviderUnstable,
    decimal? ErrorRatePercent,
    int SampleSize,
    int MinimumSampleSize,
    decimal ErrorRateThresholdPercent,
    bool ErrorRateExceeded);

public sealed record AdminDashboardContentReportStatus(
    int PendingCount,
    int OverdueCount);

public sealed record AdminDashboardAlert(
    string Code,
    string Tone,
    string Title,
    string Detail,
    string ActionText,
    string Href);

internal sealed record AiOperationAggregate(int TotalRequests, int FailedRequests);
