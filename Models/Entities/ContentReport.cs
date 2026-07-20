using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Báo cáo nội dung do người học tạo cho một bộ flashcard công khai.
public class ContentReport
{
    [Key]
    public long Id { get; set; }

    public int FlashcardSetId { get; set; }

    [Required, MaxLength(450)]
    public string ReporterUserId { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(40)]
    public string Status { get; set; } = ContentReportStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ResolvedByUserId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    [MaxLength(40)]
    public string? ResolutionOutcome { get; set; }

    [MaxLength(500)]
    public string? ResolutionReason { get; set; }

    // Số phiên bản tăng sau mỗi lần xử lý để phát hiện form cũ hoặc thao tác đồng thời.
    public int Version { get; set; } = 1;

    public FlashcardSet? FlashcardSet { get; set; }
}

public static class ContentReportStatus
{
    public const string Pending = "Pending";
    public const string Dismissed = "Dismissed";
    public const string Quarantined = "Quarantined";
}

public static class ContentReportResolutionOutcome
{
    public const string Dismissed = "Dismissed";
    public const string Quarantined = "Quarantined";
}
