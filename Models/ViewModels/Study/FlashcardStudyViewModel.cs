using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

public class FlashcardStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<Flashcard> Flashcards { get; set; } = new();
    public int CurrentIndex { get; set; }
    public bool StarredOnly { get; set; }
}
