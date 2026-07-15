namespace ltwnc.Services.Achievements;

// Ảnh chụp metric thành tích của user tại một thời điểm.
// Progress service đếm xong đổ vào đây; unlock / UI đọc lại, không query lặp.
public sealed class AchievementProgressSnapshot
{
    // Số thẻ IsLearned
    public int CardsMastered { get; init; }

    // Số buổi mode Flashcard
    public int FlashcardSessions { get; init; }

    // Số buổi mode Dictation
    public int DictationSessions { get; init; }

    // Tổng câu nghe chép đúng
    public int DictationCorrectAnswers { get; init; }

    // Số buổi Dictation điểm 100
    public int DictationPerfectSessions { get; init; }

    // Map enum metric -> số đếm tương ứng (so với Target trong catalog)
    public int GetValue(AchievementMetricKind kind)
    {
        switch (kind)
        {
            case AchievementMetricKind.CardsMastered:
                return CardsMastered;
            case AchievementMetricKind.FlashcardSessions:
                return FlashcardSessions;
            case AchievementMetricKind.DictationSessions:
                return DictationSessions;
            case AchievementMetricKind.DictationCorrectAnswers:
                return DictationCorrectAnswers;
            case AchievementMetricKind.DictationPerfectSessions:
                return DictationPerfectSessions;
            default:
                return 0;
        }
    }
}
