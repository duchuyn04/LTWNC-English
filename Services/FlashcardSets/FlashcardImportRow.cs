using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Services.FlashcardSets;

public sealed class FlashcardImportRow
{
    public int RowNumber { get; init; }
    public string FrontText { get; init; } = string.Empty;
    public string BackText { get; init; } = string.Empty;
    public string Pronunciation { get; init; } = string.Empty;
    public string PartOfSpeech { get; init; } = string.Empty;
    public string ExampleSentence { get; init; } = string.Empty;
    public string ExampleMeaning { get; init; } = string.Empty;
    public string? Synonyms { get; init; }
    public string? ImageUrl { get; init; }
}

public sealed class FlashcardFileParseResult
{
    public IReadOnlyList<FlashcardImportRow> Rows { get; init; } =
        Array.Empty<FlashcardImportRow>();

    public IReadOnlyList<FlashcardImportError> Errors { get; init; } =
        Array.Empty<FlashcardImportError>();

    public IReadOnlyList<string> MissingRequiredHeaders { get; init; } =
        Array.Empty<string>();

    public string? FileError { get; init; }
}
