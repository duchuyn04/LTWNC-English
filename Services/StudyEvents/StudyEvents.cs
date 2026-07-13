using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyEvents;

// Các mẩu tin (sự kiện) mẫu Observer.
// Service học tạo tin sau khi Save; publisher gửi cho mọi observer.

// Gốc: mọi sự kiện đều có user và thời điểm UTC
public abstract record StudyEvent(string UserId, DateTime OccurredAtUtc);

// User cập nhật tiến độ một thẻ (ví dụ bấm "Đã biết" trên Flashcard)
public record CardProgressChangedEvent(
    // User thao tác
    string UserId,
    // Thời điểm sự kiện
    DateTime OccurredAtUtc,
    // Bộ thẻ chứa thẻ
    int SetId,
    // Thẻ vừa đổi
    int FlashcardId,
    // true = đã thuộc
    bool IsLearned,
    // Status chi tiết sau khi lưu
    UserProgressStatus Status
) : StudyEvent(UserId, OccurredAtUtc);

// User hoàn thành một buổi học (Flashcard hoặc Dictation)
public record StudySessionCompletedEvent(
    string UserId,
    DateTime OccurredAtUtc,
    // Bộ thẻ của buổi học
    int SetId,
    // Id StudySession vừa lưu
    int SessionId,
    // Mode buổi học
    StudyMode Mode,
    // Điểm (Dictation có; Flashcard có thể null)
    int? Score
) : StudyEvent(UserId, OccurredAtUtc);

// User nộp một câu trả lời nghe chép
public record DictationAnswerCheckedEvent(
    string UserId,
    DateTime OccurredAtUtc,
    int SetId,
    // Phiên dictation
    int SessionId,
    // Thẻ vừa chấm
    int FlashcardId,
    // Đáp án đúng hay sai
    bool IsCorrect
) : StudyEvent(UserId, OccurredAtUtc);
