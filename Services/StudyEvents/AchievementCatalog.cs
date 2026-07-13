namespace ltwnc.Services.StudyEvents;

// Danh mục huy hiệu tĩnh trong code.
// Code cố định (lưu DB). Title/Description hiện UI. Metric+Target tính progress và unlock.
public static class AchievementCatalog
{
    // Mã cũ: giữ nguyên chuỗi để không vỡ dữ liệu đã unlock
    public const string FirstCardMastered = "first_card_mastered";
    public const string CardsMastered10 = "cards_mastered_10";
    public const string FirstFlashcardSession = "first_flashcard_session";
    public const string FirstDictationSession = "first_dictation_session";
    public const string DictationPerfectSession = "dictation_perfect_session";

    // Mã tier thêm sau (25/50/100 thẻ, nhiều buổi...)
    public const string CardsMastered25 = "cards_mastered_25";
    public const string CardsMastered50 = "cards_mastered_50";
    public const string CardsMastered100 = "cards_mastered_100";
    public const string FlashcardSessions5 = "flashcard_sessions_5";
    public const string FlashcardSessions10 = "flashcard_sessions_10";
    public const string FlashcardSessions20 = "flashcard_sessions_20";
    public const string DictationSessions5 = "dictation_sessions_5";
    public const string DictationCorrect10 = "dictation_correct_10";
    public const string DictationCorrect50 = "dictation_correct_50";

    // Text nút CTA nhóm "thẻ thuộc"
    private const string CardCtaText = "Học tiếp trong thư viện bộ thẻ";

    // Text nút CTA nhóm "buổi học"
    private const string SessionCtaText = "Chọn bộ thẻ để học tiếp";

    // Đường dẫn CTA chung
    private const string DefaultCtaPath = "/Set";

    // Một huy hiệu: mã, UI, metric đo, mốc Target, CTA
    public sealed record Definition(
        // Khóa lưu UserAchievement.Code
        string Code,
        // Tên hiện UI
        string Title,
        // Mô tả điều kiện
        string Description,
        // Loại metric so với snapshot
        AchievementMetricKind Metric,
        // Cần đạt bao nhiêu
        int Target,
        // Nhãn nút CTA
        string CtaText,
        // URL CTA
        string CtaPath);

    // Toàn bộ huy hiệu app hỗ trợ (thứ tự catalog, UI có thể sort lại)
    public static IReadOnlyList<Definition> All { get; } =
    [
        // --- Thẻ đã thuộc ---
        new Definition(
            FirstCardMastered,
            "Thẻ đầu tiên đã thuộc",
            "Bạn đã đánh dấu thuộc hoặc trả lời đúng ít nhất một thẻ.",
            AchievementMetricKind.CardsMastered,
            1,
            CardCtaText,
            DefaultCtaPath),
        new Definition(
            CardsMastered10,
            "Thuộc 10 thẻ",
            "Bạn đã có ít nhất 10 thẻ ở trạng thái đã thuộc.",
            AchievementMetricKind.CardsMastered,
            10,
            CardCtaText,
            DefaultCtaPath),
        new Definition(
            CardsMastered25,
            "Thuộc 25 thẻ",
            "Bạn đã có ít nhất 25 thẻ ở trạng thái đã thuộc.",
            AchievementMetricKind.CardsMastered,
            25,
            CardCtaText,
            DefaultCtaPath),
        new Definition(
            CardsMastered50,
            "Thuộc 50 thẻ",
            "Bạn đã có ít nhất 50 thẻ ở trạng thái đã thuộc.",
            AchievementMetricKind.CardsMastered,
            50,
            CardCtaText,
            DefaultCtaPath),
        new Definition(
            CardsMastered100,
            "Thuộc 100 thẻ",
            "Bạn đã có ít nhất 100 thẻ ở trạng thái đã thuộc.",
            AchievementMetricKind.CardsMastered,
            100,
            CardCtaText,
            DefaultCtaPath),

        // --- Buổi Flashcard ---
        new Definition(
            FirstFlashcardSession,
            "Buổi Flashcard đầu tiên",
            "Bạn đã hoàn thành một buổi học lật thẻ.",
            AchievementMetricKind.FlashcardSessions,
            1,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            FlashcardSessions5,
            "5 buổi Flashcard",
            "Bạn đã hoàn thành ít nhất 5 buổi học lật thẻ.",
            AchievementMetricKind.FlashcardSessions,
            5,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            FlashcardSessions10,
            "10 buổi Flashcard",
            "Bạn đã hoàn thành ít nhất 10 buổi học lật thẻ.",
            AchievementMetricKind.FlashcardSessions,
            10,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            FlashcardSessions20,
            "20 buổi Flashcard",
            "Bạn đã hoàn thành ít nhất 20 buổi học lật thẻ.",
            AchievementMetricKind.FlashcardSessions,
            20,
            SessionCtaText,
            DefaultCtaPath),

        // --- Buổi / câu Nghe chép ---
        new Definition(
            FirstDictationSession,
            "Buổi Nghe chép đầu tiên",
            "Bạn đã hoàn thành một buổi nghe và viết lại.",
            AchievementMetricKind.DictationSessions,
            1,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            DictationSessions5,
            "5 buổi Nghe chép",
            "Bạn đã hoàn thành ít nhất 5 buổi nghe chép.",
            AchievementMetricKind.DictationSessions,
            5,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            DictationCorrect10,
            "10 câu nghe chép đúng",
            "Bạn đã trả lời đúng ít nhất 10 câu trong chế độ nghe chép.",
            AchievementMetricKind.DictationCorrectAnswers,
            10,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            DictationCorrect50,
            "50 câu nghe chép đúng",
            "Bạn đã trả lời đúng ít nhất 50 câu trong chế độ nghe chép.",
            AchievementMetricKind.DictationCorrectAnswers,
            50,
            SessionCtaText,
            DefaultCtaPath),
        new Definition(
            DictationPerfectSession,
            "Nghe chép điểm tuyệt đối",
            "Bạn đã hoàn thành một buổi nghe chép với điểm 100.",
            AchievementMetricKind.DictationPerfectSessions,
            1,
            SessionCtaText,
            DefaultCtaPath)
    ];

    // Tìm định nghĩa theo mã; trả null nếu không có trong danh mục
    public static Definition? Find(string code)
        => All.FirstOrDefault(item => item.Code == code);
}
