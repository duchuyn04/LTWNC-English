namespace ltwnc.Services.FlashcardSets;

public sealed class FlashcardFileParserResolver
{
    private readonly IFlashcardFileParser _csvParser;
    private readonly IFlashcardFileParser _xlsxParser;

    public FlashcardFileParserResolver(
        CsvFlashcardFileParser csvParser,
        XlsxFlashcardFileParser xlsxParser)
    {
        _csvParser = csvParser;
        _xlsxParser = xlsxParser;
    }

    public IFlashcardFileParser Resolve(string extension) =>
        extension?.Trim().ToLowerInvariant() switch
        {
            ".csv" => _csvParser,
            ".xlsx" => _xlsxParser,
            _ => throw new FlashcardImportException("Chỉ hỗ trợ tệp .csv và .xlsx.")
        };
}

public sealed class FlashcardImportException : Exception
{
    public FlashcardImportException(string message) : base(message)
    {
    }

    public FlashcardImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
