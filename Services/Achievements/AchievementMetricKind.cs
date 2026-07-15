namespace ltwnc.Services.Achievements;

// Loại chỉ số gắn với từng huy hiệu trong catalog (metric + Target).
public enum AchievementMetricKind
{
    // Số thẻ IsLearned
    CardsMastered,

    // Số buổi Flashcard hoàn thành
    FlashcardSessions,

    // Số buổi Dictation hoàn thành
    DictationSessions,

    // Số câu nghe chép đúng
    DictationCorrectAnswers,

    // Số buổi Dictation điểm 100
    DictationPerfectSessions
}
