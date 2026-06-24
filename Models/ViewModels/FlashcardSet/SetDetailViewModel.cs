using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class SetDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<Flashcard> Flashcards { get; set; } = new();
    public bool IsOwner { get; set; }
}
