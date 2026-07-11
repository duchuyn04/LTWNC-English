namespace ltwnc.Models.ViewModels.FlashcardSet;

public class FlashcardSetListItemViewModel
{
    public ltwnc.Models.Entities.FlashcardSet Set { get; set; } = null!;

    public int TotalCards { get; set; }

    public int LearnedCount { get; set; }

    public int MasteryPercent { get; set; }
}
