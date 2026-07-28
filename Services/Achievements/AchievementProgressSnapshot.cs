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
        // 1. Phân nhánh xử lý theo giá trị `kind`.
        switch (kind)
        {
            case AchievementMetricKind.CardsMastered:
                // 2. Trả `CardsMastered` cho nơi gọi.
                return CardsMastered;
            case AchievementMetricKind.FlashcardSessions:
                // 3. Trả `FlashcardSessions` cho nơi gọi.
                return FlashcardSessions;
            case AchievementMetricKind.DictationSessions:
                // 4. Trả `DictationSessions` cho nơi gọi.
                return DictationSessions;
            case AchievementMetricKind.DictationCorrectAnswers:
                // 5. Trả `DictationCorrectAnswers` cho nơi gọi.
                return DictationCorrectAnswers;
            case AchievementMetricKind.DictationPerfectSessions:
                // 6. Trả `DictationPerfectSessions` cho nơi gọi.
                return DictationPerfectSessions;
            default:
                // 7. Trả `0` cho nơi gọi.
                return 0;
        }
    }
}
