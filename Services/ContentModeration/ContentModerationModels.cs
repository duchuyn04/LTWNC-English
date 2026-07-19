using ltwnc.Models.Entities;

namespace ltwnc.Services.ContentModeration;

public sealed record AdminContentSetQuery(
    string? Search = null,
    string? Status = null,
    string? Visibility = null,
    int Page = ContentModerationService.DefaultPage,
    int PageSize = ContentModerationService.DefaultPageSize);

public sealed record AdminContentSetRow(
    int Id,
    string Title,
    string OwnerDisplay,
    bool IsPublic,
    string ModerationStatus,
    string? ModerationPublicReason,
    DateTime UpdatedAtUtc,
    DateTime? ModeratedAtUtc,
    int CardCount,
    int PendingReportCount,
    int ModerationVersion);

public sealed record AdminContentSetPage(
    IReadOnlyList<AdminContentSetRow> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    // Tổng số trang tối thiểu là 1 để view phân trang không rơi vào trạng thái rỗng.
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
}

public sealed record AdminContentSetDetailsResult(
    bool Found,
    bool RequiresReason,
    string? Message,
    AdminContentSetDetails? Details)
{
    // Kết quả không tìm thấy dùng chung cho controller.
    public static AdminContentSetDetailsResult NotFound()
    {
        return new AdminContentSetDetailsResult(false, false, null, null);
    }

    // Kết quả yêu cầu lý do trước khi mở chi tiết nội dung riêng tư.
    public static AdminContentSetDetailsResult ReasonRequired(string? message)
    {
        return new AdminContentSetDetailsResult(true, true, message, null);
    }

    // Kết quả đã có dữ liệu chi tiết sau khi qua kiểm tra quyền riêng tư.
    public static AdminContentSetDetailsResult Success(AdminContentSetDetails details)
    {
        return new AdminContentSetDetailsResult(true, false, null, details);
    }
}

public sealed record AdminContentSetDetails(
    int Id,
    string Title,
    string? Description,
    string OwnerDisplay,
    bool IsPublic,
    string ModerationStatus,
    string? ModerationPublicReason,
    string? ModerationInternalNote,
    string? ModerationEvidence,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ModeratedAtUtc,
    int ModerationVersion,
    IReadOnlyList<AdminContentFlashcardRow> Cards);

public sealed record AdminContentFlashcardRow(
    int Id,
    string FrontText,
    string BackText,
    string? PartOfSpeech,
    int OrderIndex);

public sealed record AdminContentSetAccessCommand(
    string ActorUserId,
    string ActorDisplay,
    string? Reason,
    string? CorrelationId = null);

public sealed record QuarantineFlashcardSetCommand(
    int FlashcardSetId,
    int Version,
    string ActorUserId,
    string ActorDisplay,
    string PublicReason,
    string? InternalNote,
    string? Evidence,
    bool Confirmed,
    string? CorrelationId = null);

public sealed record QuarantineFromReportCommand(
    long ReportId,
    int ReportVersion,
    int FlashcardSetVersion,
    string ActorUserId,
    string ActorDisplay,
    string PublicReason,
    string? InternalNote,
    string? Evidence,
    bool Confirmed,
    string? CorrelationId = null);

public sealed record RestoreFlashcardSetCommand(
    int FlashcardSetId,
    int Version,
    string ActorUserId,
    string ActorDisplay,
    string Reason,
    bool Confirmed,
    string? CorrelationId = null);

public sealed record ContentModerationOperationResult(bool Succeeded, string Message)
{
    // Tạo kết quả thành công để controller chỉ cần hiện thông báo và redirect.
    public static ContentModerationOperationResult Success(string message)
    {
        return new ContentModerationOperationResult(true, message);
    }

    // Tạo kết quả thất bại nghiệp vụ, không ném exception cho lỗi do dữ liệu form.
    public static ContentModerationOperationResult Failure(string message)
    {
        return new ContentModerationOperationResult(false, message);
    }
}

