using ltwnc.Models.Entities;
using ltwnc.Services.ContentModeration;

namespace ltwnc.Areas.Admin.Models;

// View model danh sách bộ flashcard để Admin kiểm duyệt.
public sealed class AdminContentIndexViewModel
{
    public required IReadOnlyList<AdminContentSetRowViewModel> Items { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Visibility { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    // Tổng trang tối thiểu là 1 để view không cần nhánh đặc biệt cho danh sách rỗng.
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

// Một hàng summary trong danh sách Admin, không chứa nội dung từng thẻ.
public sealed class AdminContentSetRowViewModel
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string OwnerDisplay { get; init; }
    public required string VisibilityLabel { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public string? ModerationPublicReason { get; init; }
    public required string UpdatedDisplay { get; init; }
    public required string ModeratedDisplay { get; init; }
    public required int CardCount { get; init; }
    public required int PendingReportCount { get; init; }
    public required int ModerationVersion { get; init; }
    public required bool CanQuarantine { get; init; }
    public required bool CanRestore { get; init; }
}

// View model trang chi tiết bộ flashcard trong Admin.
public sealed class AdminContentDetailsViewModel
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string OwnerDisplay { get; init; }
    public required string VisibilityLabel { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public string? ModerationPublicReason { get; init; }
    public string? ModerationInternalNote { get; init; }
    public string? ModerationEvidence { get; init; }
    public required string CreatedDisplay { get; init; }
    public required string UpdatedDisplay { get; init; }
    public required string ModeratedDisplay { get; init; }
    public required int ModerationVersion { get; init; }
    public required bool CanQuarantine { get; init; }
    public required bool CanRestore { get; init; }
    public required IReadOnlyList<AdminContentFlashcardViewModel> Cards { get; init; }
}

// View model một thẻ trong trang chi tiết Admin.
public sealed class AdminContentFlashcardViewModel
{
    public required int Id { get; init; }
    public required string FrontText { get; init; }
    public required string BackText { get; init; }
    public string? PartOfSpeech { get; init; }
    public required int OrderIndex { get; init; }
}

// Form cách ly bộ flashcard.
public sealed class AdminQuarantineContentInputModel
{
    public int Version { get; set; }
    public string PublicReason { get; set; } = string.Empty;
    public string? InternalNote { get; set; }
    public string? Evidence { get; set; }
    public bool Confirmed { get; set; }
}

// Form khôi phục bộ flashcard đã bị cách ly.
public sealed class AdminRestoreContentInputModel
{
    public int Version { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

// View model cổng lý do trước khi mở nội dung chi tiết của bộ riêng tư.
public sealed class AdminContentReasonGateViewModel
{
    public required int FlashcardSetId { get; init; }
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }
}

// Mapper chuyển dữ liệu service sang dữ liệu hiển thị đã định dạng.
public static class AdminContentModerationViewModelMapper
{
    // Dựng view model danh sách, giữ nguyên bộ lọc để view dựng lại query.
    public static AdminContentIndexViewModel ToIndexViewModel(
        AdminContentSetPage page,
        AdminContentSetQuery query)
    {
        return new AdminContentIndexViewModel
        {
            Items = page.Items.Select(ToRowViewModel).ToArray(),
            Search = query.Search,
            Status = query.Status,
            Visibility = query.Visibility,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    // Dựng view model chi tiết, bao gồm ghi chú nội bộ vì trang này chỉ dành cho Admin.
    public static AdminContentDetailsViewModel ToDetailsViewModel(AdminContentSetDetails details)
    {
        return new AdminContentDetailsViewModel
        {
            Id = details.Id,
            Title = details.Title,
            Description = details.Description,
            OwnerDisplay = details.OwnerDisplay,
            VisibilityLabel = BuildVisibilityLabel(details.IsPublic),
            StatusLabel = BuildStatusLabel(details.ModerationStatus),
            StatusTone = BuildStatusTone(details.ModerationStatus),
            ModerationPublicReason = details.ModerationPublicReason,
            ModerationInternalNote = details.ModerationInternalNote,
            ModerationEvidence = details.ModerationEvidence,
            CreatedDisplay = FormatDateTime(details.CreatedAtUtc),
            UpdatedDisplay = FormatDateTime(details.UpdatedAtUtc),
            ModeratedDisplay = FormatDateTime(details.ModeratedAtUtc),
            ModerationVersion = details.ModerationVersion,
            CanQuarantine = details.ModerationStatus == FlashcardSetModerationStatus.Active,
            CanRestore = details.ModerationStatus == FlashcardSetModerationStatus.Quarantined,
            Cards = details.Cards.Select(ToFlashcardViewModel).ToArray()
        };
    }

    // Dựng view model cho cổng lý do, không chứa nội dung thẻ.
    public static AdminContentReasonGateViewModel ToReasonGateViewModel(
        int flashcardSetId,
        string? reason,
        string? errorMessage)
    {
        return new AdminContentReasonGateViewModel
        {
            FlashcardSetId = flashcardSetId,
            Reason = reason,
            ErrorMessage = errorMessage
        };
    }

    // Chuyển một hàng summary sang thông tin hiển thị.
    private static AdminContentSetRowViewModel ToRowViewModel(AdminContentSetRow row)
    {
        return new AdminContentSetRowViewModel
        {
            Id = row.Id,
            Title = row.Title,
            OwnerDisplay = row.OwnerDisplay,
            VisibilityLabel = BuildVisibilityLabel(row.IsPublic),
            StatusLabel = BuildStatusLabel(row.ModerationStatus),
            StatusTone = BuildStatusTone(row.ModerationStatus),
            ModerationPublicReason = row.ModerationPublicReason,
            UpdatedDisplay = FormatDateTime(row.UpdatedAtUtc),
            ModeratedDisplay = FormatDateTime(row.ModeratedAtUtc),
            CardCount = row.CardCount,
            PendingReportCount = row.PendingReportCount,
            ModerationVersion = row.ModerationVersion,
            CanQuarantine = row.ModerationStatus == FlashcardSetModerationStatus.Active,
            CanRestore = row.ModerationStatus == FlashcardSetModerationStatus.Quarantined
        };
    }

    // Chuyển một thẻ sang view model chỉ đọc cho Admin.
    private static AdminContentFlashcardViewModel ToFlashcardViewModel(AdminContentFlashcardRow row)
    {
        return new AdminContentFlashcardViewModel
        {
            Id = row.Id,
            FrontText = row.FrontText,
            BackText = row.BackText,
            PartOfSpeech = row.PartOfSpeech,
            OrderIndex = row.OrderIndex
        };
    }

    // Nhãn public/private bằng tiếng Việt.
    private static string BuildVisibilityLabel(bool isPublic)
    {
        if (isPublic)
        {
            return "Công khai";
        }

        return "Riêng tư";
    }

    // Nhãn trạng thái kiểm duyệt cho badge.
    private static string BuildStatusLabel(string status)
    {
        if (status == FlashcardSetModerationStatus.Quarantined)
        {
            return "Đã cách ly";
        }

        return "Đang hoạt động";
    }

    // Tone CSS ổn định cho badge trạng thái.
    private static string BuildStatusTone(string status)
    {
        if (status == FlashcardSetModerationStatus.Quarantined)
        {
            return "danger";
        }

        return "success";
    }

    // Định dạng thời gian UTC sang giờ Việt Nam, dùng dấu gạch nếu chưa có.
    private static string FormatDateTime(DateTime? valueUtc)
    {
        if (valueUtc == null)
        {
            return "-";
        }

        return AdminTimeZone.ToVietnamTime(valueUtc.Value).ToString("HH:mm dd/MM/yyyy");
    }
}
