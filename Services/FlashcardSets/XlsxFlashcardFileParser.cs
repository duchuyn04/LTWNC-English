using ClosedXML.Excel;

namespace ltwnc.Services.FlashcardSets;

public sealed class XlsxFlashcardFileParser : IFlashcardFileParser
{
    public Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(stream);
        IXLWorksheet worksheet = workbook.Worksheets.First();
        int lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        string[] headers = lastColumn == 0
            ? []
            : Enumerable.Range(1, lastColumn)
                .Select(column => worksheet.Cell(1, column).GetString())
                .ToArray();

        var rows = new List<(int RowNumber, IReadOnlyList<string> Values)>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] values = Enumerable.Range(1, lastColumn)
                .Select(column => worksheet.Cell(rowNumber, column).GetString())
                .ToArray();
            rows.Add((rowNumber, values));
        }

        return Task.FromResult(FlashcardImportValidation.ParseRows(headers, rows));
    }
}
