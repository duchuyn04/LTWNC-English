namespace ltwnc.Services.FlashcardSets;

public interface IFlashcardFileParser
{
    Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
