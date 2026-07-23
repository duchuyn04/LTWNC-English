using ltwnc.Services.FlashcardSets;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class FlashcardImportResult
{
    public int ImportedCount { get; init; }

    public int SkippedCount { get; init; }

    public IReadOnlyList<FlashcardImportError> Errors { get; init; } =
        Array.Empty<FlashcardImportError>();
}

public sealed class FlashcardImportPreview
{
    public int ValidCount => Rows.Count;

    public int SkippedCount => Errors.Count;

    public IReadOnlyList<FlashcardImportRow> Rows { get; init; } =
        Array.Empty<FlashcardImportRow>();

    public IReadOnlyList<FlashcardImportError> Errors { get; init; } =
        Array.Empty<FlashcardImportError>();
}
