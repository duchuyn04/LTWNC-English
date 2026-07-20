using ltwnc.Models.Entities;

namespace ltwnc.Services.AdminStudyRecords;

// Tham số lọc/sắp xếp/phân trang cho danh sách phiên học phía máy chủ.
// From/To là ngày theo giờ Việt Nam; service tự quy đổi sang UTC trước khi truy vấn.
public sealed record AdminStudySessionQuery(
    string? Search = null,
    string? UserId = null,
    string? Mode = null,
    string? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Sort = null,
    int Page = AdminStudyRecordService.DefaultPage,
    int PageSize = AdminStudyRecordService.DefaultPageSize);

// Một hàng trong bảng danh sách phiên học, chỉ gồm dữ liệu cần hiển thị.
public sealed record AdminStudySessionRow(
    int SessionId,
    string UserId,
    string UserName,
    string Email,
    StudyMode Mode,
    string FlashcardSetTitle,
    int? Score,
    int PlannedItemCount,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int? DurationSeconds,
    // Trạng thái đã suy ra phía máy chủ: completed | inprogress | abandoned.
    string Status);

// Một trang kết quả phiên học kèm thông tin phân trang.
public sealed record AdminStudySessionPage(
    IReadOnlyList<AdminStudySessionRow> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    // Tổng trang tối thiểu là 1 để giao diện không rơi vào trạng thái rỗng.
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

// Một câu trả lờ trong phiên nghe chép chính tả (Dictation).
public sealed record AdminDictationAnswerRow(
    string CardFrontText,
    string AnsweredText,
    bool IsCorrect,
    DateTime AnsweredAtUtc);

// Tóm tắt Nhiệm vụ tiếng Anh gắn với phiên; KHÔNG chứa nội dung hội thoại.
// Mở hội thoại chi tiết là phạm vi của hạng mục Nhiệm vụ tiếng Anh (issue 09).
public sealed record AdminMissionSummary(
    string Topic,
    string Title,
    string Status,
    int? Score,
    int TurnCount,
    int TargetWordTotal,
    int TargetWordUsed);

// Ảnh chụp tiến độ hiện tại của ngườ học trên bộ thẻ của phiên.
public sealed record AdminSetProgressSummary(
    int TotalCards,
    int MasteredCount,
    int LearningCount,
    int UnlearnedCount);

// Dữ liệu chi tiết một phiên học ở chế độ chỉ đọc, đã kèm phần riêng theo chế độ học.
public sealed record AdminStudySessionDetails(
    int SessionId,
    string UserId,
    string UserName,
    string Email,
    StudyMode Mode,
    int FlashcardSetId,
    string FlashcardSetTitle,
    int? Score,
    int PlannedItemCount,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int? DurationSeconds,
    string Status,
    IReadOnlyList<AdminDictationAnswerRow> DictationAnswers,
    AdminMissionSummary? Mission,
    AdminSetProgressSummary SetProgress);

// Ngữ cảnh của lần truy cập nhạy cảm: ai xem, vì lý do gì.
// Lý do bắt buộc vì mọi lần mở hồ sơ cấp ngườ học đều phải được kiểm toán.
public sealed record AdminStudyRecordAccessCommand(
    string ActorUserId,
    string ActorDisplay,
    string Reason,
    string? CorrelationId = null);
