using ClosedXML.Excel;
using System.IO.Compression;

namespace ltwnc.Services.FlashcardSets;

public sealed class XlsxFlashcardFileParser : IFlashcardFileParser
{
    public Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateArchiveSize(stream);

        using var workbook = new XLWorkbook(stream);
        IXLWorksheet worksheet = workbook.Worksheets.First();
        int lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastColumn > FlashcardImportValidation.MaxColumns)
        {
            throw new FlashcardImportException(
                $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxColumns} cột.");
        }
        if (lastRow > FlashcardImportValidation.MaxRows + 1)
        {
            throw new FlashcardImportException(
                $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxRows} dòng dữ liệu.");
        }

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

    private static void ValidateArchiveSize(Stream stream)
    {
        const long maxExpandedBytes = 50L * 1024 * 1024;
        const int maxEntries = 1000;
        if (!stream.CanSeek)
        {
            throw new FlashcardImportException("Luồng XLSX phải hỗ trợ seek để kiểm tra an toàn.");
        }

        long originalPosition = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > maxEntries
                || archive.Entries.Sum(entry => entry.Length) > maxExpandedBytes)
            {
                throw new FlashcardImportException("Tệp XLSX giải nén quá lớn hoặc có quá nhiều thành phần.");
            }
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
