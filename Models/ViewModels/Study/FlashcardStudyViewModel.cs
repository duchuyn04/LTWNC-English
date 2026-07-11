using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho view học flashcard (lật thẻ)
public class FlashcardStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<Flashcard> Flashcards { get; set; } = new();
    public List<Flashcard> VocabularyCards { get; set; } = new();
    public Dictionary<int, UserProgress> ProgressByCardId { get; set; } = new();
    public int CurrentIndex { get; set; }
    public bool StarredOnly { get; set; }
    public UserStudySettings Settings { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public bool UnlearnedOnly { get; set; }
}
