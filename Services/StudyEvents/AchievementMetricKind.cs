namespace ltwnc.Services.StudyEvents;

// ============================================================
// Loại chỉ số dùng để đo tiến độ / điều kiện mở khóa huy hiệu.
// Mỗi Definition trong catalog gắn với đúng một MetricKind + Target.
// ============================================================
public enum AchievementMetricKind
{
    // Số thẻ đã thuộc (UserProgress.IsLearned)
    CardsMastered,

    // Số buổi Flashcard đã hoàn thành
    FlashcardSessions,

    // Số buổi Nghe chép đã hoàn thành
    DictationSessions,

    // Số câu nghe chép trả lời đúng
    DictationCorrectAnswers,

    // Số buổi nghe chép đạt điểm 100
    DictationPerfectSessions
}
