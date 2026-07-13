using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// View lật thẻ /Study/{setId}/Flashcard
public class FlashcardStudyViewModel
{
    // Bộ thẻ
    public int SetId { get; set; }

    // Tiêu đề header
    public string SetTitle { get; set; } = string.Empty;

    // Thẻ sau bộ lọc (học tuần tự / shuffle UI)
    public List<Flashcard> Flashcards { get; set; } = new();

    // Toàn bộ thẻ không filter (UI phụ, vocabulary list)
    public List<Flashcard> VocabularyCards { get; set; } = new();

    // cardId -> progress (đã thuộc / đúng sai...)
    public Dictionary<int, UserProgress> ProgressByCardId { get; set; } = new();

    // Vị trí thẻ hiện tại (0-based, đã clamp)
    public int CurrentIndex { get; set; }

    // Filter hiệu lực: chỉ đã sao
    public bool StarredOnly { get; set; }

    // Filter hiệu lực: chỉ chưa thuộc
    public bool UnlearnedOnly { get; set; }

    // Cài đặt mặt thẻ / TTS / ảnh (JS đọc)
    public UserStudySettings Settings { get; set; } = new();

    // false = khách (ẩn mark learned / complete)
    public bool IsAuthenticated { get; set; }
}
