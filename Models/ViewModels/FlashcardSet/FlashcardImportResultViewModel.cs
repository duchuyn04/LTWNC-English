namespace ltwnc.Models.ViewModels.FlashcardSet;

public class FlashcardImportResult
{
    public int ImportedCount { get; init; }

    public int SkippedCount { get; init; }

    public IReadOnlyList<FlashcardImportError> Errors { get; init; } =
        Array.Empty<FlashcardImportError>();
}
