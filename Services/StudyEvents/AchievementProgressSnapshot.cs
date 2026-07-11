namespace ltwnc.Services.StudyEvents;

// ============================================================
// Ảnh chụp tiến độ các chỉ số thành tích của một người dùng tại một thời điểm.
// Service đọc các bảng học (progress, session, dictation detail) rồi đổ vào đây
// để trang Thành tích / unlock service dùng lại, không query lặp từng huy hiệu.
// ============================================================
public sealed class AchievementProgressSnapshot
{
    // Số thẻ đã đánh dấu thuộc (IsLearned)
    public int CardsMastered { get; init; }

    // Số buổi học Flashcard đã hoàn thành
    public int FlashcardSessions { get; init; }

    // Số buổi nghe chép đã hoàn thành
    public int DictationSessions { get; init; }

    // Tổng số câu nghe chép trả lời đúng
    public int DictationCorrectAnswers { get; init; }

    // Số buổi nghe chép đạt điểm 100
    public int DictationPerfectSessions { get; init; }

    // Lấy giá trị metric theo loại — dùng khi so với Target trong catalog
    public int GetValue(AchievementMetricKind kind) => kind switch
    {
        AchievementMetricKind.CardsMastered => CardsMastered,
        AchievementMetricKind.FlashcardSessions => FlashcardSessions,
        AchievementMetricKind.DictationSessions => DictationSessions,
        AchievementMetricKind.DictationCorrectAnswers => DictationCorrectAnswers,
        AchievementMetricKind.DictationPerfectSessions => DictationPerfectSessions,
        _ => 0
    };
}
