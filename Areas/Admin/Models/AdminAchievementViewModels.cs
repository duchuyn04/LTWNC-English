using ltwnc.Areas.Admin;
using ltwnc.Services.AdminAchievements;

namespace ltwnc.Areas.Admin.Models;

// View model trang Admin thành tích, gồm danh mục và kết quả theo người dùng.
public sealed class AdminAchievementIndexViewModel
{
    public required IReadOnlyList<AdminAchievementDefinitionViewModel> Catalog { get; init; }
    public required IReadOnlyList<AdminAchievementUserResultViewModel> UserResults { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalUsers { get; init; }
    public int DefaultBatchSize { get; init; } = AdminAchievementService.DefaultBatchSize;

    // Tổng số trang, tối thiểu là 1 để UI không rơi vào trang 0.
    public int TotalPages
    {
        get
        {
            if (TotalUsers == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalUsers / (double)PageSize);
        }
    }

    // Cho view biết có cần hiện nút trang trước không.
    public bool HasPreviousPage
    {
        get
        {
            return Page > 1;
        }
    }

    // Cho view biết có cần hiện nút trang sau không.
    public bool HasNextPage
    {
        get
        {
            return Page < TotalPages;
        }
    }
}

// Một dòng danh mục thành tích trong bảng chỉ đọc.
public sealed class AdminAchievementDefinitionViewModel
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string MetricLabel { get; init; }
    public required int Target { get; init; }
    public required int RecipientCount { get; init; }
}

// Một dòng kết quả thành tích theo người dùng.
public sealed class AdminAchievementUserResultViewModel
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required int UnlockedCount { get; init; }
    public required int EligibleCount { get; init; }
    public required int MissingCount { get; init; }
    public required string LastUnlockedDisplay { get; init; }
    public required string MissingCodesDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
}

// Dữ liệu form đồng bộ lại cho một người dùng.
public sealed class AdminAchievementResyncUserInputModel
{
    public string TargetUserId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool Confirmed { get; set; }
}

// Dữ liệu form đồng bộ lại toàn hệ thống.
public sealed class AdminAchievementResyncAllInputModel
{
    public string? Reason { get; set; }
    public bool Confirmed { get; set; }
    public int BatchSize { get; set; } = AdminAchievementService.DefaultBatchSize;
}

// Chuyển dữ liệu service sang view model đã định dạng sẵn cho Razor.
public static class AdminAchievementViewModelMapper
{
    // Dùng overview từ service để tạo view model trang danh sách.
    public static AdminAchievementIndexViewModel ToIndexViewModel(
        AdminAchievementOverview overview,
        AdminAchievementQuery query)
    {
        return new AdminAchievementIndexViewModel
        {
            Catalog = overview.Catalog.Select(ToDefinitionViewModel).ToArray(),
            UserResults = overview.UserResults.Select(ToUserResultViewModel).ToArray(),
            Search = query.Search,
            Page = overview.Page,
            PageSize = overview.PageSize,
            TotalUsers = overview.TotalUsers
        };
    }

    // Định dạng một định nghĩa thành tích từ danh mục trong mã nguồn.
    private static AdminAchievementDefinitionViewModel ToDefinitionViewModel(
        AdminAchievementDefinitionSummary definition)
    {
        return new AdminAchievementDefinitionViewModel
        {
            Code = definition.Code,
            Title = definition.Title,
            Description = definition.Description,
            MetricLabel = BuildMetricLabel(definition.Metric),
            Target = definition.Target,
            RecipientCount = definition.RecipientCount
        };
    }

    // Định dạng kết quả thành tích theo người dùng.
    private static AdminAchievementUserResultViewModel ToUserResultViewModel(
        AdminAchievementUserResult result)
    {
        string statusLabel = "Đầy đủ";
        string statusTone = "success";
        if (result.MissingCount > 0)
        {
            statusLabel = "Cần đồng bộ";
            statusTone = "warning";
        }

        return new AdminAchievementUserResultViewModel
        {
            UserId = result.UserId,
            UserName = result.UserName,
            Email = result.Email,
            UnlockedCount = result.UnlockedCount,
            EligibleCount = result.EligibleCount,
            MissingCount = result.MissingCount,
            LastUnlockedDisplay = FormatDateTime(result.LastUnlockedAtUtc),
            MissingCodesDisplay = FormatMissingCodes(result.MissingCodes),
            StatusLabel = statusLabel,
            StatusTone = statusTone
        };
    }

    // Đổi tên metric kỹ thuật sang nhãn tiếng Việt cho Admin.
    private static string BuildMetricLabel(string metric)
    {
        if (metric == "CardsMastered")
        {
            return "Thẻ đã thuộc";
        }

        if (metric == "FlashcardSessions")
        {
            return "Buổi Flashcard";
        }

        if (metric == "DictationSessions")
        {
            return "Buổi nghe chép";
        }

        if (metric == "DictationCorrectAnswers")
        {
            return "Câu nghe chép đúng";
        }

        if (metric == "DictationPerfectSessions")
        {
            return "Buổi nghe chép 100 điểm";
        }

        return metric;
    }

    // Định dạng danh sách mã còn thiếu, giới hạn để bảng không quá dài.
    private static string FormatMissingCodes(IReadOnlyList<string> missingCodes)
    {
        if (missingCodes.Count == 0)
        {
            return "Không có";
        }

        return string.Join(", ", missingCodes.Take(4));
    }

    // Định dạng thời gian UTC sang giờ Việt Nam, trả dấu gạch khi chưa có thành tích nào.
    private static string FormatDateTime(DateTime? valueUtc)
    {
        if (valueUtc == null)
        {
            return "-";
        }

        return AdminTimeZone.ToVietnamTime(valueUtc.Value).ToString("HH:mm dd/MM/yyyy");
    }
}
