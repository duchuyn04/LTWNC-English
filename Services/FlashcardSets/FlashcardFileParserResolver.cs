namespace ltwnc.Services.FlashcardSets;

public sealed class FlashcardFileParserResolver
{
    private readonly IFlashcardFileParser _csvParser;
    private readonly IFlashcardFileParser _xlsxParser;

    public FlashcardFileParserResolver(
        CsvFlashcardFileParser csvParser,
        XlsxFlashcardFileParser xlsxParser)
    {
        // 1. Lưu dependency `_csvParser` để các phương thức khác sử dụng.
        _csvParser = csvParser;
        // 2. Lưu dependency `_xlsxParser` để các phương thức khác sử dụng.
        _xlsxParser = xlsxParser;
    }

    public IFlashcardFileParser Resolve(string extension)
    {
        // 1. Trả `extension?.Trim().ToLowerInvariant() switch { ".csv" => _csvParser,...` cho nơi gọi.
        return extension?.Trim().ToLowerInvariant() switch
        {
            ".csv" => _csvParser,
            ".xlsx" => _xlsxParser,
            _ => throw new FlashcardImportException("Chỉ hỗ trợ tệp .csv và .xlsx.")
        };
    }
}

public sealed class FlashcardImportException : Exception
{
    public FlashcardImportException(string message) : base(message)
    {
        // 1. Chuyển thông báo lỗi nhập tệp cho lớp Exception cơ sở.
    }

    public FlashcardImportException(string message, Exception innerException)
        : base(message, innerException)
    {
        // 1. Giữ cả thông báo và exception gốc để hỗ trợ truy vết nguyên nhân.
    }
}
