namespace ltwnc.Services.StudyEvents;

// ============================================================
// "Danh mục huy hiệu" — định nghĩa sẵn các thành tích trong app.
// Code (mã) giữ cố định để lưu database và không trùng.
// Title / Description hiện ra cho user đọc trên trang Thành tích.
// ============================================================
public static class AchievementCatalog
{
    // Mã thành tích — dùng trong database, không đổi lung tung
    public const string FirstCardMastered = "first_card_mastered";
    public const string CardsMastered10 = "cards_mastered_10";
    public const string FirstFlashcardSession = "first_flashcard_session";
    public const string FirstDictationSession = "first_dictation_session";
    public const string DictationPerfectSession = "dictation_perfect_session";

    // Một mục trong danh mục: mã + tên đẹp + mô tả dễ hiểu
    public sealed record Definition(string Code, string Title, string Description);

    // Danh sách đầy đủ các huy hiệu app đang hỗ trợ
    public static IReadOnlyList<Definition> All { get; } =
    [
        new Definition(
            FirstCardMastered,
            "Thẻ đầu tiên đã thuộc",
            "Bạn đã đánh dấu thuộc hoặc trả lời đúng ít nhất một thẻ."),
        new Definition(
            CardsMastered10,
            "Thuộc 10 thẻ",
            "Bạn đã có ít nhất 10 thẻ ở trạng thái đã thuộc."),
        new Definition(
            FirstFlashcardSession,
            "Buổi Flashcard đầu tiên",
            "Bạn đã hoàn thành một buổi học lật thẻ."),
        new Definition(
            FirstDictationSession,
            "Buổi Nghe chép đầu tiên",
            "Bạn đã hoàn thành một buổi nghe và viết lại."),
        new Definition(
            DictationPerfectSession,
            "Nghe chép điểm tuyệt đối",
            "Bạn đã hoàn thành một buổi nghe chép với điểm 100.")
    ];

    // Tìm định nghĩa theo mã; trả null nếu không có trong danh mục
    public static Definition? Find(string code)
        => All.FirstOrDefault(item => item.Code == code);
}
