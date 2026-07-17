namespace ltwnc.Models.ViewModels.FlashcardSet;

public class FlashcardImportError
{
    public int RowNumber { get; init; }

    public string Reason { get; init; } = string.Empty;
}
