using ltwnc.Services.ContentReports;

namespace ltwnc.Areas.Admin.Models;

public sealed class AdminContentReportIndexViewModel
{
    public required IReadOnlyList<AdminContentReportRowViewModel> Items { get; init; }
    public required IReadOnlyList<ContentReportReasonOption> ReasonOptions { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int OverduePendingCount { get; init; }

    public int TotalPages
    {
        get
        {
            if (TotalCount == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }

    public bool HasPreviousPage
    {
        get
        {
            return Page > 1;
        }
    }

    public bool HasNextPage
    {
        get
        {
            return Page < TotalPages;
        }
    }
}

public sealed class AdminContentReportRowViewModel
{
    public long Id { get; init; }
    public int FlashcardSetId { get; init; }
    public required string FlashcardSetTitle { get; init; }
    public required string ReporterDisplay { get; init; }
    public required string OwnerDisplay { get; init; }
    public required string ReasonLabel { get; init; }
    public string? Description { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public required string CreatedAtDisplay { get; init; }
    public required string AgeDisplay { get; init; }
    public string? ResolutionReason { get; init; }
    public int Version { get; init; }
    public bool CanDismiss { get; init; }
}

public sealed class AdminContentReportDismissInputModel
{
    public int Version { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public static class AdminContentReportViewModelMapper
{
    // Chuyển page service sang view model và giữ nguyên bộ lọc hiện tại.
    public static AdminContentReportIndexViewModel ToIndexViewModel(
        AdminContentReportPage page,
        IReadOnlyList<ContentReportReasonOption> reasonOptions,
        string? search,
        string? status,
        string? reason,
        string? sort,
        int overduePendingCount)
    {
        return new AdminContentReportIndexViewModel
        {
            Items = page.Items.Select(ToRowViewModel).ToArray(),
            ReasonOptions = reasonOptions,
            Search = search,
            Status = status,
            Reason = reason,
            Sort = sort,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            OverduePendingCount = overduePendingCount
        };
    }

    // Chuyển một báo cáo sang dữ liệu hiển thị, không để view tự tính nghiệp vụ.
    private static AdminContentReportRowViewModel ToRowViewModel(AdminContentReportRow report)
    {
        DateTime nowUtc = DateTime.UtcNow;
        TimeSpan age = nowUtc - report.CreatedAtUtc;

        return new AdminContentReportRowViewModel
        {
            Id = report.Id,
            FlashcardSetId = report.FlashcardSetId,
            FlashcardSetTitle = report.FlashcardSetTitle,
            ReporterDisplay = report.ReporterDisplay,
            OwnerDisplay = report.OwnerDisplay,
            ReasonLabel = report.ReasonLabel,
            Description = report.Description,
            StatusLabel = BuildStatusLabel(report.Status),
            StatusTone = BuildStatusTone(report.Status),
            CreatedAtDisplay = AdminTimeZone.ToVietnamTime(report.CreatedAtUtc).ToString("HH:mm dd/MM/yyyy"),
            AgeDisplay = BuildAgeDisplay(age),
            ResolutionReason = report.ResolutionReason,
            Version = report.Version,
            CanDismiss = report.Status == ltwnc.Models.Entities.ContentReportStatus.Pending
        };
    }

    // Tạo nhãn trạng thái tiếng Việt cho bảng Admin.
    private static string BuildStatusLabel(string status)
    {
        if (status == ltwnc.Models.Entities.ContentReportStatus.Dismissed)
        {
            return "Đã bác bỏ";
        }

        if (status == ltwnc.Models.Entities.ContentReportStatus.Quarantined)
        {
            return "Đã cách ly";
        }

        return "Đang chờ";
    }

    // Tạo tone CSS ổn định cho badge trạng thái.
    private static string BuildStatusTone(string status)
    {
        if (status == ltwnc.Models.Entities.ContentReportStatus.Dismissed)
        {
            return "neutral";
        }

        if (status == ltwnc.Models.Entities.ContentReportStatus.Quarantined)
        {
            return "danger";
        }

        return "warning";
    }

    // Hiển thị tuổi báo cáo đủ rõ cho hàng đợi xử lý nhanh.
    private static string BuildAgeDisplay(TimeSpan age)
    {
        if (age.TotalHours >= 24)
        {
            int days = Math.Max(1, (int)Math.Floor(age.TotalDays));
            return $"{days} ngày";
        }

        if (age.TotalHours >= 1)
        {
            int hours = Math.Max(1, (int)Math.Floor(age.TotalHours));
            return $"{hours} giờ";
        }

        int minutes = Math.Max(1, (int)Math.Floor(age.TotalMinutes));
        return $"{minutes} phút";
    }
}
