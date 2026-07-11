using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyEvents;

// ============================================================
// Các "mẩu tin" (sự kiện) trong mẫu Observer.
// Khi user học xong một việc (đánh dấu thẻ, xong buổi học...),
// hệ thống tạo một mẩu tin này rồi phát cho mọi "người theo dõi".
// Subject (người phát tin) không cần biết ai sẽ nhận tin.
// ============================================================

// Lớp gốc cho mọi sự kiện học tập.
// Mọi mẩu tin đều biết: ai (UserId) và lúc nào (OccurredAtUtc).
public abstract record StudyEvent(string UserId, DateTime OccurredAtUtc);

// User vừa cập nhật tiến độ một thẻ (đã thuộc / chưa thuộc).
// Ví dụ: bấm "Đã biết" trên màn Flashcard.
public record CardProgressChangedEvent(
    string UserId,
    DateTime OccurredAtUtc,
    int SetId,
    int FlashcardId,
    bool IsLearned,
    UserProgressStatus Status
) : StudyEvent(UserId, OccurredAtUtc);

// User vừa hoàn thành một buổi học (Flashcard hoặc Dictation).
// Ví dụ: bấm "Hoàn thành" cuối phiên flashcard, hoặc xong nghe chép.
public record StudySessionCompletedEvent(
    string UserId,
    DateTime OccurredAtUtc,
    int SetId,
    int SessionId,
    StudyMode Mode,
    int? Score
) : StudyEvent(UserId, OccurredAtUtc);

// User vừa nộp một câu trả lời trong chế độ Nghe chép.
// Dùng để mở thành tích liên quan (ví dụ thẻ đầu tiên trả lời đúng).
public record DictationAnswerCheckedEvent(
    string UserId,
    DateTime OccurredAtUtc,
    int SetId,
    int SessionId,
    int FlashcardId,
    bool IsCorrect
) : StudyEvent(UserId, OccurredAtUtc);
