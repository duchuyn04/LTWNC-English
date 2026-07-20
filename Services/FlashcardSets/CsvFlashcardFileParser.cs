using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace ltwnc.Services.FlashcardSets;

public sealed class CsvFlashcardFileParser : IFlashcardFileParser
{
    public async Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = false,
            MissingFieldFound = null
        });

        if (!await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            return FlashcardImportValidation.ParseRows([], []);
        }

        string[] headers = csv.Parser.Record ?? [];
        var rows = new List<(int RowNumber, IReadOnlyList<string> Values)>();
        int nextRecordStartRow = csv.Parser.RawRow + 1;

        while (await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            if (rows.Count >= FlashcardImportValidation.MaxRows)
            {
                throw new FlashcardImportException(
                    $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxRows} dòng dữ liệu.");
            }
            rows.Add((nextRecordStartRow, csv.Parser.Record ?? []));
            nextRecordStartRow = csv.Parser.RawRow + 1;
        }

        return FlashcardImportValidation.ParseRows(headers, rows);
    }
}
