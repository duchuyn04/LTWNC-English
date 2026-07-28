using ltwnc.Models.Entities;

namespace ltwnc.Services.AdminEnglishMissions;

// Điều kiện tìm kiếm, lọc, sắp xếp và phân trang nhiệm vụ tiếng Anh.
public sealed record AdminEnglishMissionQuery(
    string? Search = null,
    string? Topic = null,
    string? Status = null,
    string? Retention = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = AdminEnglishMissionService.DefaultPageSize);

// Một trang kết quả nhiệm vụ tiếng Anh trả về khu vực quản trị.
public sealed record AdminEnglishMissionPage(
    IReadOnlyList<AdminEnglishMissionRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

// Dữ liệu tóm tắt một nhiệm vụ dùng trên màn hình danh sách.
public sealed record AdminEnglishMissionRow(
    int MissionId,
    int StudySessionId,
    string UserName,
    string Email,
    int FlashcardSetId,
    string FlashcardSetTitle,
    string Topic,
    string Title,
    string Status,
    int TurnCount,
    int? Score,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime RetentionDeadlineUtc,
    bool ConversationAvailable,
    bool HasRetentionHold);

// Thông tin Admin và lý do cần thiết để mở hội thoại nhạy cảm.
public sealed record AdminEnglishMissionAccessCommand(
    int MissionId,
    string ActorUserId,
    string ActorDisplay,
    string IncidentType,
    string? CaseReference,
    string Reason,
    string? CorrelationId = null);

// Kết quả kiểm tra quyền truy cập trước khi trả nội dung hội thoại.
public sealed class AdminEnglishMissionConversationResult
{
    public bool Found { get; init; }
    public bool RequiresGate { get; init; }
    public string Message { get; init; } = string.Empty;
    public AdminEnglishMissionConversation? Conversation { get; init; }
}

// Toàn bộ dữ liệu hội thoại đã được phép hiển thị cho Admin.
public sealed record AdminEnglishMissionConversation(
    int MissionId,
    int StudySessionId,
    string UserName,
    string Email,
    string FlashcardSetTitle,
    string Topic,
    string Title,
    string Situation,
    string NpcName,
    string NpcRole,
    string OpeningLine,
    string Status,
    int TurnCount,
    int? Score,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime RetentionDeadlineUtc,
    string IncidentType,
    string? CaseReference,
    string Reason,
    IReadOnlyList<AdminEnglishMissionTargetWordRow> TargetWords,
    IReadOnlyList<AdminEnglishMissionTurnRow> Turns);

// Trạng thái sử dụng của một từ mục tiêu trong nhiệm vụ.
public sealed record AdminEnglishMissionTargetWordRow(
    string Term,
    string Definition,
    string? PartOfSpeech,
    bool IsUsed,
    int? FirstUsedTurn);

// Nội dung và phản hồi chấm điểm của một lượt hội thoại.
public sealed record AdminEnglishMissionTurnRow(
    int TurnNumber,
    string UserText,
    string NpcText,
    string? FeedbackVi,
    string? CorrectionEn,
    string? CorrectionExplanationVi,
    string UsedWordsDisplay,
    string AchievedGoalsDisplay,
    DateTime CreatedAtUtc);

// Kết quả dọn nội dung hội thoại đã hết thời hạn lưu giữ.
public sealed record AdminEnglishMissionCleanupResult(
    int ScannedCount,
    int ClearedCount);
