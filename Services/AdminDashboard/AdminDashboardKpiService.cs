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
    private const decimal CompletionDropAlertThresholdPoints = 10m;
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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
        // 3. Lưu dependency `_configuration` để các phương thức khác sử dụng.
        _configuration = configuration;
    }

    // Tạo snapshot KPI theo khoảng 7/30/90 ngày, mặc định 30 ngày nếu input không hợp lệ.
    public async Task<AdminDashboardSnapshot> GetSnapshotAsync(
        int? days,
        CancellationToken cancellationToken = default)
    {
        // 1. Tính giá trị và lưu vào `requestedDays` để dùng ở bước tiếp theo.
        int requestedDays = 30;
        // 2. Kiểm tra `days.HasValue` để chọn nhánh xử lý phù hợp.
        if (days.HasValue)
        {
            // 3. Cập nhật `requestedDays` bằng giá trị mới.
            requestedDays = days.Value;
        }

        // 4. Tính giá trị và lưu vào `selectedDays` để dùng ở bước tiếp theo.
        int selectedDays = 30;
        // 5. Kiểm tra `AllowedDays.Contains(requestedDays)` để chọn nhánh xử lý phù hợp.
        if (AllowedDays.Contains(requestedDays))
        {
            // 6. Cập nhật `selectedDays` bằng giá trị mới.
            selectedDays = requestedDays;
        }

        // 7. Gọi `BuildPeriod` và lưu kết quả vào `current`.
        AdminDashboardPeriod current = BuildPeriod(selectedDays, 0);
        // 8. Gọi `BuildPeriod` và lưu kết quả vào `previous`.
        AdminDashboardPeriod previous = BuildPeriod(selectedDays, -selectedDays);

        // 9. Gọi `LoadMetricsAsync` và lưu kết quả vào `currentMetrics`.
        PeriodMetricSet currentMetrics = await LoadMetricsAsync(current, cancellationToken);
        // 10. Gọi `LoadMetricsAsync` và lưu kết quả vào `previousMetrics`.
        PeriodMetricSet previousMetrics = await LoadMetricsAsync(previous, cancellationToken);

        // 11. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `GetSnapshotAsync` và lưu kết quả vào `snapshot`.
        AdminDashboardSnapshot snapshot = await GetSnapshotAsync(days, cancellationToken);
        // 2. Gọi `ToViewModel` và lưu kết quả vào `viewModel`.
        AdminDashboardViewModel viewModel = ToViewModel(snapshot);
        // 3. Gọi `LoadAiStatusAsync` và lưu kết quả vào `aiStatus`.
        AdminDashboardAiStatus aiStatus = await LoadAiStatusAsync(cancellationToken);
        // 4. Gọi `LoadContentReportStatusAsync` và lưu kết quả vào `contentReports`.
        AdminDashboardContentReportStatus contentReports =
            await LoadContentReportStatusAsync(cancellationToken);
        // 5. Gọi `HasCurrentAchievementSyncFailureAsync` và lưu kết quả vào `hasAchievementFailure`.
        bool hasAchievementFailure = await HasCurrentAchievementSyncFailureAsync(cancellationToken);
        // 6. Gọi `BuildAlerts` và lưu kết quả vào `alerts`.
        IReadOnlyList<AdminDashboardAlert> alerts = BuildAlerts(
            aiStatus,
            contentReports,
            hasAchievementFailure,
            snapshot.CurrentMetrics,
            snapshot.PreviousMetrics);

        // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
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
                    "ph-users-three",
                    "Xem người dùng",
                    "/Admin/Users",
                    "Đã học ít nhất một lần"),
                CountCard(
                    "Mới đăng ký",
                    snapshot.CurrentMetrics.NewRegistrations,
                    snapshot.PreviousMetrics.NewRegistrations,
                    "ph-user-plus",
                    "Xem người dùng",
                    "/Admin/Users",
                    "Tài khoản mới"),
                CountCard(
                    "Phiên bắt đầu",
                    snapshot.CurrentMetrics.StudySessions,
                    snapshot.PreviousMetrics.StudySessions,
                    "ph-graduation-cap",
                    "Xem phiên học",
                    "/Admin/Learning",
                    "Lượt mở phiên học"),
                PercentCard(
                    "Hoàn thành",
                    snapshot.CurrentMetrics.CompletionRatePercent,
                    snapshot.PreviousMetrics.CompletionRatePercent,
                    snapshot.CurrentMetrics.CompletionRateDenominator,
                    "ph-check-circle",
                    "Xem phiên học",
                    "/Admin/Learning"),
                CountCard(
                    "Hội thoại AI",
                    snapshot.CurrentMetrics.EnglishMissions,
                    snapshot.PreviousMetrics.EnglishMissions,
                    "ph-chats-circle",
                    "Xem hội thoại",
                    "/Admin/EnglishMissions",
                    "Lượt bắt đầu hội thoại"),
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
        // 1. Tính giá trị và lưu vào `startUtc` để dùng ở bước tiếp theo.
        DateTime startUtc = period.StartUtc;
        // 2. Tính giá trị và lưu vào `endUtc` để dùng ở bước tiếp theo.
        DateTime endUtc = period.EndUtcExclusive;
        // 3. Gọi `AddMinutes` và lưu kết quả vào `activeSessionCutoffUtc`.
        DateTime activeSessionCutoffUtc = period.EvaluationUtc.AddMinutes(-RecentActiveSessionMinutes);

        // 4. Gọi `Where` và lưu kết quả vào `sessionsInPeriod`.
        IQueryable<StudySession> sessionsInPeriod = _context.StudySessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= startUtc && session.StartedAt < endUtc);

        // 5. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `sessionAggregate`.
        SessionMetricAggregate? sessionAggregate = await sessionsInPeriod
            .GroupBy(_ => 1)
            .Select(group => new SessionMetricAggregate(
                group.Count(),
                group.Count(session => session.CompletedAt.HasValue
                    || session.StartedAt < activeSessionCutoffUtc),
                group.Count(session => session.CompletedAt.HasValue),
                group.Count(session => session.Mode == (StudyMode)EnglishMissionModeValue)))
            .FirstOrDefaultAsync(cancellationToken);

        // 6. Tính giá trị và lưu vào `studySessions` để dùng ở bước tiếp theo.
        int studySessions = sessionAggregate?.StudySessions ?? 0;
        // 7. Tính giá trị và lưu vào `eligibleSessions` để dùng ở bước tiếp theo.
        int eligibleSessions = sessionAggregate?.EligibleSessions ?? 0;
        // 8. Tính giá trị và lưu vào `completedSessions` để dùng ở bước tiếp theo.
        int completedSessions = sessionAggregate?.CompletedSessions ?? 0;

        // Đếm distinct trên database để một người học nhiều lần vẫn chỉ là một active user.
        // 9. Gọi `Select` và lưu kết quả vào `usersFromSessions`.
        IQueryable<string> usersFromSessions = _context.StudySessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= startUtc && session.StartedAt < endUtc)
            .Select(session => session.UserId);
        // 10. Gọi `Select` và lưu kết quả vào `usersFromProgress`.
        IQueryable<string> usersFromProgress = _context.UserProgresses
            .AsNoTracking()
            .Where(progress => progress.LastReviewed >= startUtc && progress.LastReviewed < endUtc)
            .Select(progress => progress.UserId);
        // 11. Gọi `CountAsync` và lưu kết quả vào `activeUsers`.
        int activeUsers = await usersFromSessions
            .Union(usersFromProgress)
            .CountAsync(cancellationToken);

        // 12. Gọi `CountAsync` và lưu kết quả vào `newRegistrations`.
        int newRegistrations = await _context.UserProfiles
            .AsNoTracking()
            .CountAsync(profile => profile.CreatedAt >= startUtc && profile.CreatedAt < endUtc, cancellationToken);

        // 13. Tính giá trị và lưu vào `englishMissions` để dùng ở bước tiếp theo.
        int englishMissions = sessionAggregate?.EnglishMissions ?? 0;

        // 14. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `aiAggregate`.
        AiOperationAggregate? aiAggregate = await _context.AiOperationLogs
            .AsNoTracking()
            .Where(log => log.OccurredAtUtc >= startUtc && log.OccurredAtUtc < endUtc)
            .GroupBy(_ => 1)
            .Select(group => new AiOperationAggregate(
                group.Count(),
                group.Count(log => !log.Succeeded)))
            .FirstOrDefaultAsync(cancellationToken);

        // 15. Khởi tạo `ai` với dữ liệu ban đầu cần thiết.
        AiOperationAggregate ai = new(0, 0);
        // 16. Kiểm tra `aiAggregate != null` để chọn nhánh xử lý phù hợp.
        if (aiAggregate != null)
        {
            // 17. Cập nhật `ai` bằng giá trị mới.
            ai = aiAggregate;
        }

        // 18. Tính giá trị và lưu vào `completionRatePercent` để dùng ở bước tiếp theo.
        decimal? completionRatePercent = null;
        // 19. Kiểm tra `eligibleSessions > 0` để chọn nhánh xử lý phù hợp.
        if (eligibleSessions > 0)
        {
            // 20. Tính giá trị và lưu vào `rawCompletionRate` để dùng ở bước tiếp theo.
            decimal rawCompletionRate = completedSessions * 100m / eligibleSessions;
            // 21. Cập nhật `completionRatePercent` bằng giá trị mới.
            completionRatePercent = decimal.Round(rawCompletionRate, 1);
        }

        // 22. Tính giá trị và lưu vào `aiErrorRatePercent` để dùng ở bước tiếp theo.
        decimal? aiErrorRatePercent = null;
        // 23. Kiểm tra `ai.TotalRequests >= MinimumAiSampleSize` để chọn nhánh xử lý phù hợp.
        if (ai.TotalRequests >= MinimumAiSampleSize)
        {
            // 24. Tính giá trị và lưu vào `rawAiErrorRate` để dùng ở bước tiếp theo.
            decimal rawAiErrorRate = ai.FailedRequests * 100m / ai.TotalRequests;
            // 25. Cập nhật `aiErrorRatePercent` bằng giá trị mới.
            aiErrorRatePercent = decimal.Round(rawAiErrorRate, 1);
        }

        // 26. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `ReadAiHealthWindowMinutes` và lưu kết quả vào `healthWindowMinutes`.
        int healthWindowMinutes = ReadAiHealthWindowMinutes();
        // 2. Gọi `ReadAiMinimumSampleSize` và lưu kết quả vào `minimumSampleSize`.
        int minimumSampleSize = ReadAiMinimumSampleSize();
        // 3. Gọi `ReadAiErrorRateThresholdPercent` và lưu kết quả vào `thresholdPercent`.
        decimal thresholdPercent = ReadAiErrorRateThresholdPercent();
        // 4. Gọi `ReadAiUnstableFailureThreshold` và lưu kết quả vào `unstableFailureThreshold`.
        int unstableFailureThreshold = ReadAiUnstableFailureThreshold();
        // 5. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 6. Gọi `AddMinutes` và lưu kết quả vào `windowStartUtc`.
        DateTime windowStartUtc = nowUtc.AddMinutes(-healthWindowMinutes);

        // 7. Gọi `ToListAsync` và lưu kết quả vào `providers`.
        List<AiProvider> providers = await _context.AiProviders
            .AsNoTracking()
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
        // 8. Gọi `FirstOrDefault` và lưu kết quả vào `primaryProvider`.
        AiProvider? primaryProvider = providers.FirstOrDefault(provider => provider.IsPrimary);

        // 9. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `aggregate`.
        AiOperationAggregate? aggregate = await _context.AiOperationLogs
            .AsNoTracking()
            .Where(log => log.OccurredAtUtc >= windowStartUtc && log.OccurredAtUtc <= nowUtc)
            .GroupBy(_ => 1)
            .Select(group => new AiOperationAggregate(
                group.Count(),
                group.Count(log => !log.Succeeded)))
            .FirstOrDefaultAsync(cancellationToken);

        // 10. Tính giá trị và lưu vào `totalRequests` để dùng ở bước tiếp theo.
        int totalRequests = 0;
        // 11. Tính giá trị và lưu vào `failedRequests` để dùng ở bước tiếp theo.
        int failedRequests = 0;
        // 12. Kiểm tra `aggregate != null` để chọn nhánh xử lý phù hợp.
        if (aggregate != null)
        {
            // 13. Cập nhật `totalRequests` bằng giá trị mới.
            totalRequests = aggregate.TotalRequests;
            // 14. Cập nhật `failedRequests` bằng giá trị mới.
            failedRequests = aggregate.FailedRequests;
        }

        // 15. Tính giá trị và lưu vào `errorRatePercent` để dùng ở bước tiếp theo.
        decimal? errorRatePercent = null;
        // 16. Tính giá trị và lưu vào `errorRateExceeded` để dùng ở bước tiếp theo.
        bool errorRateExceeded = false;
        // 17. Kiểm tra `totalRequests >= minimumSampleSize` để chọn nhánh xử lý phù hợp.
        if (totalRequests >= minimumSampleSize)
        {
            // 18. Tính giá trị và lưu vào `rawRate` để dùng ở bước tiếp theo.
            decimal rawRate = failedRequests * 100m / totalRequests;
            // 19. Cập nhật `errorRatePercent` bằng giá trị mới.
            errorRatePercent = decimal.Round(rawRate, 1);
            // 20. Cập nhật `errorRateExceeded` bằng giá trị mới.
            errorRateExceeded = errorRatePercent.Value > thresholdPercent;
        }

        // 21. Gọi `IsPrimaryProviderUnstable` và lưu kết quả vào `primaryIsUnstable`.
        bool primaryIsUnstable = IsPrimaryProviderUnstable(
            primaryProvider,
            unstableFailureThreshold);
        // 22. Gọi `Count` và lưu kết quả vào `readyProviders`.
        int readyProviders = providers.Count(provider =>
            provider.IsEnabled && provider.LastCheckSucceeded == true);
        // 23. Gọi `Count` và lưu kết quả vào `unstableProviders`.
        int unstableProviders = providers.Count(provider =>
            provider.ConsecutiveFailureCount >= unstableFailureThreshold);
        // 24. Gọi `BuildAiSummary` và lưu kết quả vào `summary`.
        string summary = BuildAiSummary(
            primaryProvider,
            primaryIsUnstable,
            errorRateExceeded,
            readyProviders,
            providers.Count);

        // 25. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `AddHours` và lưu kết quả vào `overdueCutoffUtc`.
        DateTime overdueCutoffUtc = _timeProvider.GetUtcNow().UtcDateTime
            .AddHours(-OverdueContentReportHours);
        // 2. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `aggregate`.
        ContentReportAggregate? aggregate = await _context.ContentReports
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new ContentReportAggregate(
                group.Count(report => report.Status == ContentReportStatus.Pending),
                group.Count(report => report.Status == ContentReportStatus.Pending
                    && report.CreatedAtUtc <= overdueCutoffUtc)))
            .FirstOrDefaultAsync(cancellationToken);

        // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminDashboardContentReportStatus(
            aggregate?.PendingCount ?? 0,
            aggregate?.OverdueCount ?? 0);
    }

    // Chỉ cảnh báo đồng bộ thành tích khi audit mới nhất của resync là Failure.
    private async Task<bool> HasCurrentAchievementSyncFailureAsync(CancellationToken cancellationToken)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `latestAchievementSync`.
        AdminAuditLog? latestAchievementSync = await _context.AdminAuditLogs
            .AsNoTracking()
            .Where(log => log.Action == AdminAuditActions.AchievementsResyncUser
                || log.Action == AdminAuditActions.AchievementsResyncAll)
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // 2. Kiểm tra `latestAchievementSync == null` để chọn nhánh xử lý phù hợp.
        if (latestAchievementSync == null)
        {
            // 3. Trả `false` cho nơi gọi.
            return false;
        }

        // 4. Trả `latestAchievementSync.Outcome == AdminAuditOutcome.Failure` cho nơi gọi.
        return latestAchievementSync.Outcome == AdminAuditOutcome.Failure;
    }

    // Ghép các tín hiệu vận hành thành danh sách cảnh báo không cần nút đóng thủ công.
    private static IReadOnlyList<AdminDashboardAlert> BuildAlerts(
        AdminDashboardAiStatus aiStatus,
        AdminDashboardContentReportStatus contentReports,
        bool hasAchievementFailure,
        PeriodMetricSet currentMetrics,
        PeriodMetricSet previousMetrics)
    {
        // 1. Khởi tạo `alerts` với dữ liệu ban đầu cần thiết.
        List<AdminDashboardAlert> alerts = new();
        // 2. Kiểm tra `aiStatus.PrimaryProviderUnstable` để chọn nhánh xử lý phù hợp.
        if (aiStatus.PrimaryProviderUnstable)
        {
            // 3. Gọi `Add` để thực hiện bước nghiệp vụ này.
            alerts.Add(new AdminDashboardAlert(
                "ai-primary-unstable",
                "danger",
                "Kết nối AI chính cần kiểm tra",
                "Kết nối chính đang tắt, chưa được kiểm tra hoặc đã thất bại nhiều lần.",
                "Kiểm tra kết nối",
                "/Admin/AiProviders"));
        }

        // 4. Kiểm tra `aiStatus.ErrorRateExceeded` để chọn nhánh xử lý phù hợp.
        if (aiStatus.ErrorRateExceeded)
        {
            // 5. Tính giá trị và lưu vào `detail` để dùng ở bước tiếp theo.
            string detail = $"Tỷ lệ lỗi AI {aiStatus.ErrorRatePercent:0.#}% vượt ngưỡng {aiStatus.ErrorRateThresholdPercent:0.#}% trong cửa sổ gần nhất.";
            // 6. Gọi `Add` để thực hiện bước nghiệp vụ này.
            alerts.Add(new AdminDashboardAlert(
                "ai-error-rate",
                "danger",
                "AI đang có nhiều lỗi",
                detail,
                "Xem cấu hình AI",
                "/Admin/AiProviders"));
        }

        // 7. Kiểm tra `contentReports.OverdueCount > 0` để chọn nhánh xử lý phù hợp.
        if (contentReports.OverdueCount > 0)
        {
            // 8. Tính giá trị và lưu vào `detail` để dùng ở bước tiếp theo.
            string detail = $"{contentReports.OverdueCount:N0} báo cáo đang chờ quá 24 giờ.";
            // 9. Gọi `Add` để thực hiện bước nghiệp vụ này.
            alerts.Add(new AdminDashboardAlert(
                "content-report-overdue",
                "warning",
                "Báo cáo nội dung quá hạn",
                detail,
                "Xem hàng đợi",
                "/Admin/ContentReports?sort=oldest"));
        }

        // 10. Cảnh báo khi tỷ lệ hoàn thành giảm mạnh và cả hai kỳ đều đủ dữ liệu.
        if (currentMetrics.CompletionRatePercent.HasValue
            && previousMetrics.CompletionRatePercent.HasValue)
        {
            decimal completionDrop = previousMetrics.CompletionRatePercent.Value
                - currentMetrics.CompletionRatePercent.Value;
            if (completionDrop >= CompletionDropAlertThresholdPoints)
            {
                alerts.Add(new AdminDashboardAlert(
                    "completion-rate-drop",
                    "warning",
                    "Tỷ lệ hoàn thành giảm mạnh",
                    $"Giảm {completionDrop:0.#} điểm % so với kỳ trước.",
                    "Xem phiên học",
                    "/Admin/Learning"));
            }
        }

        // 11. Kiểm tra `hasAchievementFailure` để chọn nhánh xử lý phù hợp.
        if (hasAchievementFailure)
        {
            // 12. Gọi `Add` để thực hiện bước nghiệp vụ này.
            alerts.Add(new AdminDashboardAlert(
                "achievement-resync-failed",
                "warning",
                "Đồng bộ thành tích thất bại",
                "Lần đồng bộ thành tích gần nhất thất bại và cần kiểm tra audit.",
                "Xem thành tích",
                "/Admin/Achievements"));
        }

        // 13. Trả `alerts` cho nơi gọi.
        return alerts;
    }

    // Xem nhà cung cấp chính là không ổn định nếu bị thiếu, bị tắt hoặc kiểm tra trạng thái thất bại liên tiếp.
    private static bool IsPrimaryProviderUnstable(
        AiProvider? primaryProvider,
        int unstableFailureThreshold)
    {
        // 1. Kiểm tra `primaryProvider == null` để chọn nhánh xử lý phù hợp.
        if (primaryProvider == null)
        {
            // 2. Trả `true` cho nơi gọi.
            return true;
        }

        // 3. Kiểm tra `!primaryProvider.IsEnabled` để chọn nhánh xử lý phù hợp.
        if (!primaryProvider.IsEnabled)
        {
            // 4. Trả `true` cho nơi gọi.
            return true;
        }

        // 5. Kiểm tra `primaryProvider.LastCheckSucceeded != true` để chọn nhánh xử lý phù hợp.
        if (primaryProvider.LastCheckSucceeded != true)
        {
            // 6. Trả `true` cho nơi gọi.
            return true;
        }

        // 7. Trả `primaryProvider.ConsecutiveFailureCount >= unstableFailureThreshold` cho nơi gọi.
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
        // 1. Kiểm tra `totalProviders == 0` để chọn nhánh xử lý phù hợp.
        if (totalProviders == 0)
        {
            // 2. Trả `"Chưa cấu hình nhà cung cấp AI"` cho nơi gọi.
            return "Chưa cấu hình nhà cung cấp AI";
        }

        // 3. Kiểm tra `primaryIsUnstable` để chọn nhánh xử lý phù hợp.
        if (primaryIsUnstable)
        {
            // 4. Trả `"Nhà cung cấp AI chính cần kiểm tra"` cho nơi gọi.
            return "Nhà cung cấp AI chính cần kiểm tra";
        }

        // 5. Kiểm tra `errorRateExceeded` để chọn nhánh xử lý phù hợp.
        if (errorRateExceeded)
        {
            // 6. Trả `"Tỷ lệ lỗi AI đang vượt ngưỡng"` cho nơi gọi.
            return "Tỷ lệ lỗi AI đang vượt ngưỡng";
        }

        // 7. Kiểm tra `readyProviders == 0` để chọn nhánh xử lý phù hợp.
        if (readyProviders == 0)
        {
            // 8. Trả `"Không có nhà cung cấp AI sẵn sàng"` cho nơi gọi.
            return "Không có nhà cung cấp AI sẵn sàng";
        }

        // 9. Tính giá trị và lưu vào `primaryName` để dùng ở bước tiếp theo.
        string primaryName = primaryProvider?.Name ?? "Nhà cung cấp chính";
        // 10. Trả `$"{primaryName} đang sẵn sàng"` cho nơi gọi.
        return $"{primaryName} đang sẵn sàng";
    }

    // Dựng ranh giới ngày theo múi giờ Việt Nam rồi đổi sang UTC để query dữ liệu lưu trữ.
    private AdminDashboardPeriod BuildPeriod(int days, int offsetDays)
    {
        // 1. Gọi `ToVietnamTime` và lưu kết quả vào `nowVietnam`.
        DateTimeOffset nowVietnam = AdminTimeZone.ToVietnamTime(_timeProvider.GetUtcNow().UtcDateTime);
        // 2. Gọi `AddDays` và lưu kết quả vào `endLocalDate`.
        DateTime endLocalDate = nowVietnam.Date.AddDays(1 + offsetDays);
        // 3. Gọi `AddDays` và lưu kết quả vào `startLocalDate`.
        DateTime startLocalDate = endLocalDate.AddDays(-days);

        // 4. Gọi `ConvertTimeToUtc` và lưu kết quả vào `startUtc`.
        DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(startLocalDate, DateTimeKind.Unspecified),
            AdminTimeZone.Vietnam);
        // 5. Gọi `ConvertTimeToUtc` và lưu kết quả vào `endUtc`.
        DateTime endUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(endLocalDate, DateTimeKind.Unspecified),
            AdminTimeZone.Vietnam);

        // 6. Tính giá trị và lưu vào `evaluationUtc` để dùng ở bước tiếp theo.
        DateTime evaluationUtc = endUtc;
        // 7. Kiểm tra `offsetDays == 0` để chọn nhánh xử lý phù hợp.
        if (offsetDays == 0)
        {
            // 8. Cập nhật `evaluationUtc` bằng giá trị mới.
            evaluationUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }

        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        string actionHref,
        string detail)
    {
        // 1. Tính giá trị và lưu vào `delta` để dùng ở bước tiếp theo.
        int delta = current - previous;
        // 2. Gọi `ToString` và lưu kết quả vào `value`.
        string value = current.ToString("N0");
        // 3. Gọi `FormatCountComparison` và lưu kết quả vào `comparison`.
        string comparison = FormatCountComparison(delta);
        // 4. Gọi `DeltaTone` và lưu kết quả vào `tone`.
        string tone = DeltaTone(delta, lowerIsBetter: false);

        // 5. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminDashboardKpiCardViewModel
        {
            Label = label,
            Value = value,
            Detail = detail,
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
        // 1. Gọi `CalculatePercentDelta` và lưu kết quả vào `delta`.
        decimal? delta = CalculatePercentDelta(current, previous);

        // 2. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        string value = "—";
        // 3. Kiểm tra `current.HasValue` để chọn nhánh xử lý phù hợp.
        if (current.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = $"{current:0.#}%";
        }

        // 5. Tính giá trị và lưu vào `resolvedDetail` để dùng ở bước tiếp theo.
        string resolvedDetail = denominator == 0 || !current.HasValue
            ? "Chưa đủ dữ liệu để tính"
            : $"{denominator:N0} phiên đủ điều kiện";

        // 6. Tính giá trị và lưu vào `tone` để dùng ở bước tiếp theo.
        string tone = "neutral";
        // 7. Kiểm tra `delta.HasValue` để chọn nhánh xử lý phù hợp.
        if (delta.HasValue)
        {
            // 8. Cập nhật `tone` bằng giá trị mới.
            tone = DeltaTone(delta.Value, lowerIsBetter: false);
        }

        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `CalculatePercentDelta` và lưu kết quả vào `delta`.
        decimal? delta = CalculatePercentDelta(current, previous);

        // 2. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        string value = "—";
        // 3. Kiểm tra `current.HasValue` để chọn nhánh xử lý phù hợp.
        if (current.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = $"{current:0.#}%";
        }

        // 5. Khai báo `detail` để lưu dữ liệu dùng ở các bước sau.
        string detail;
        // 6. Kiểm tra `currentSample < MinimumAiSampleSize` để chọn nhánh xử lý phù hợp.
        if (currentSample < MinimumAiSampleSize)
        {
            // 7. Cập nhật `detail` bằng giá trị mới.
            detail = "Chưa đủ dữ liệu";
        }
        else
        {
            // 8. Cập nhật `detail` bằng giá trị mới.
            detail = $"{currentSample:N0} yêu cầu AI";
        }

        // 9. Tính giá trị và lưu vào `previousInsufficient` để dùng ở bước tiếp theo.
        bool previousInsufficient = previousSample < MinimumAiSampleSize;

        // 10. Tính giá trị và lưu vào `tone` để dùng ở bước tiếp theo.
        string tone = "neutral";
        // 11. Kiểm tra `delta.HasValue` để chọn nhánh xử lý phù hợp.
        if (delta.HasValue)
        {
            // 12. Cập nhật `tone` bằng giá trị mới.
            tone = DeltaTone(delta.Value, lowerIsBetter: true);
        }

        // 13. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminDashboardKpiCardViewModel
        {
            Label = "Lỗi AI trong kỳ",
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
        // 1. Kiểm tra `delta == 0` để chọn nhánh xử lý phù hợp.
        if (delta == 0)
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Kiểm tra `delta > 0` để chọn nhánh xử lý phù hợp.
        if (delta > 0)
        {
            // 4. Trả `$"+{delta:N0} so với kỳ trước"` cho nơi gọi.
            return $"+{delta:N0} so với kỳ trước";
        }

        // 5. Trả `$"{delta:N0} so với kỳ trước"` cho nơi gọi.
        return $"{delta:N0} so với kỳ trước";
    }

    // Định dạng so sánh phần trăm và giữ trạng thái thiếu mẫu rõ ràng.
    private static string FormatPercentComparison(
        decimal? delta,
        decimal? previous,
        bool previousInsufficient = false)
    {
        // 1. Kiểm tra `previousInsufficient` để chọn nhánh xử lý phù hợp.
        if (previousInsufficient)
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Kiểm tra `!delta.HasValue || !previous.HasValue` để chọn nhánh xử lý phù hợp.
        if (!delta.HasValue || !previous.HasValue)
        {
            // 4. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 5. Kiểm tra `delta.Value == 0` để chọn nhánh xử lý phù hợp.
        if (delta.Value == 0)
        {
            // 6. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 7. Kiểm tra `delta.Value > 0` để chọn nhánh xử lý phù hợp.
        if (delta.Value > 0)
        {
            // 8. Trả `$"+{delta:0.#} điểm % so với kỳ trước"` cho nơi gọi.
            return $"+{delta:0.#} điểm % so với kỳ trước";
        }

        // 9. Trả `$"{delta:0.#} điểm % so với kỳ trước"` cho nơi gọi.
        return $"{delta:0.#} điểm % so với kỳ trước";
    }

    // Quy đổi delta thành tone giao diện; một số chỉ số càng thấp càng tốt.
    private static string DeltaTone(decimal delta, bool lowerIsBetter)
    {
        // 1. Kiểm tra `delta == 0` để chọn nhánh xử lý phù hợp.
        if (delta == 0)
        {
            // 2. Trả `"neutral"` cho nơi gọi.
            return "neutral";
        }

        // 3. Khai báo `isPositiveSignal` để lưu dữ liệu dùng ở các bước sau.
        bool isPositiveSignal;
        // 4. Kiểm tra `lowerIsBetter` để chọn nhánh xử lý phù hợp.
        if (lowerIsBetter)
        {
            // 5. Cập nhật `isPositiveSignal` bằng giá trị mới.
            isPositiveSignal = delta < 0;
        }
        else
        {
            // 6. Cập nhật `isPositiveSignal` bằng giá trị mới.
            isPositiveSignal = delta > 0;
        }

        // 7. Kiểm tra `isPositiveSignal` để chọn nhánh xử lý phù hợp.
        if (isPositiveSignal)
        {
            // 8. Trả `"positive"` cho nơi gọi.
            return "positive";
        }

        // 9. Trả `"negative"` cho nơi gọi.
        return "negative";
    }

    // Tính chênh lệch phần trăm; thiếu một vế thì không so sánh.
    private static decimal? CalculatePercentDelta(decimal? current, decimal? previous)
    {
        // 1. Kiểm tra `!current.HasValue || !previous.HasValue` để chọn nhánh xử lý phù hợp.
        if (!current.HasValue || !previous.HasValue)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Trả `current.Value - previous.Value` cho nơi gọi.
        return current.Value - previous.Value;
    }

    // Đọc cửa sổ health AI từ cấu hình; giá trị sai quay về 5 phút.
    private int ReadAiHealthWindowMinutes()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = DefaultAiHealthWindowMinutes;
        // 2. Tính giá trị và lưu vào `configuredValue` để dùng ở bước tiếp theo.
        int? configuredValue = _configuration?.GetValue<int?>("AiProviders:Health:WindowMinutes");
        // 3. Kiểm tra `configuredValue.HasValue` để chọn nhánh xử lý phù hợp.
        if (configuredValue.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = configuredValue.Value;
        }

        // 5. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 1, 60);
    }

    // Đọc số mẫu tối thiểu từ cấu hình; dùng chung contract dashboard và cảnh báo.
    private int ReadAiMinimumSampleSize()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = MinimumAiSampleSize;
        // 2. Tính giá trị và lưu vào `configuredValue` để dùng ở bước tiếp theo.
        int? configuredValue = _configuration?.GetValue<int?>("AiProviders:Health:MinimumSampleSize");
        // 3. Kiểm tra `configuredValue.HasValue` để chọn nhánh xử lý phù hợp.
        if (configuredValue.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = configuredValue.Value;
        }

        // 5. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 1, 10_000);
    }

    // Đọc ngưỡng tỷ lệ lỗi AI từ cấu hình hệ thống.
    private decimal ReadAiErrorRateThresholdPercent()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        decimal value = DefaultAiErrorRateThresholdPercent;
        // 2. Tính giá trị và lưu vào `configuredValue` để dùng ở bước tiếp theo.
        decimal? configuredValue =
            _configuration?.GetValue<decimal?>("AiProviders:Health:ErrorRateThresholdPercent");
        // 3. Kiểm tra `configuredValue.HasValue` để chọn nhánh xử lý phù hợp.
        if (configuredValue.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = configuredValue.Value;
        }

        // 5. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 0m, 100m);
    }

    // Đọc ngưỡng fail health check liên tiếp để đánh dấu provider không ổn định.
    private int ReadAiUnstableFailureThreshold()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = DefaultAiUnstableFailureThreshold;
        // 2. Tính giá trị và lưu vào `configuredValue` để dùng ở bước tiếp theo.
        int? configuredValue =
            _configuration?.GetValue<int?>("AiProviders:Health:UnstableFailureThreshold");
        // 3. Kiểm tra `configuredValue.HasValue` để chọn nhánh xử lý phù hợp.
        if (configuredValue.HasValue)
        {
            // 4. Cập nhật `value` bằng giá trị mới.
            value = configuredValue.Value;
        }

        // 5. Trả kết quả từ `Clamp` cho nơi gọi.
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

internal sealed record SessionMetricAggregate(
    int StudySessions,
    int EligibleSessions,
    int CompletedSessions,
    int EnglishMissions);

internal sealed record ContentReportAggregate(int PendingCount, int OverdueCount);

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
