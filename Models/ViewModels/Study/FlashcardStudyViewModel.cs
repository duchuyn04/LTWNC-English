using ltwnc.Models.ViewModels.Flashcards;

namespace ltwnc.Models.ViewModels.Study;

// View lật thẻ /Study/{setId}/Flashcard
public class FlashcardStudyViewModel
{
    // Bộ thẻ
    public int SetId { get; set; }

    // StudySession.Id đang chạy; 0 với khách chưa đăng nhập.
    public int SessionId { get; set; }

    // Tiêu đề header
    public string SetTitle { get; set; } = string.Empty;

    // Thẻ sau bộ lọc (học tuần tự / shuffle UI)
    public IReadOnlyList<FlashcardViewModel> Flashcards { get; set; } = Array.Empty<FlashcardViewModel>();

    // Toàn bộ thẻ không filter (UI phụ, vocabulary list)
    public IReadOnlyList<FlashcardViewModel> VocabularyCards { get; set; } = Array.Empty<FlashcardViewModel>();

    // cardId -> progress (đã thuộc / đúng sai...)
    public IReadOnlyDictionary<int, FlashcardProgressViewModel> ProgressByCardId { get; set; } =
        new Dictionary<int, FlashcardProgressViewModel>();

    // Vị trí thẻ hiện tại (0-based, đã clamp)
    public int CurrentIndex { get; set; }

    // Filter hiệu lực: chỉ đã sao
    public bool StarredOnly { get; set; }

    // Filter hiệu lực: chỉ chưa thuộc
    public bool UnlearnedOnly { get; set; }

    // Cài đặt mặt thẻ / TTS / ảnh (JS đọc)
    public StudySettingsViewModel Settings { get; set; } = new();

    // false = khách (ẩn mark learned / complete)
    public bool IsAuthenticated { get; set; }
}
