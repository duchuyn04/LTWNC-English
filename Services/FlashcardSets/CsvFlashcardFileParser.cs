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
        // 1. Gọi `ThrowIfNull` để thực hiện bước nghiệp vụ này.
        ArgumentNullException.ThrowIfNull(stream);

        // 2. Khởi tạo `reader` với dữ liệu ban đầu cần thiết.
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        // 3. Khởi tạo `csv` với dữ liệu ban đầu cần thiết.
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = false,
            MissingFieldFound = null
        });

        // 4. Kiểm tra `!await csv.ReadAsync().WaitAsync(cancellationToken)` để chọn nhánh xử lý phù hợp.
        if (!await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            // 5. Trả kết quả từ `ParseRows` cho nơi gọi.
            return FlashcardImportValidation.ParseRows([], []);
        }

        // 6. Tính giá trị và lưu vào `headers` để dùng ở bước tiếp theo.
        string[] headers = csv.Parser.Record ?? [];
        // 7. Khởi tạo `rows` với dữ liệu ban đầu cần thiết.
        var rows = new List<(int RowNumber, IReadOnlyList<string> Values)>();
        // 8. Tính giá trị và lưu vào `nextRecordStartRow` để dùng ở bước tiếp theo.
        int nextRecordStartRow = csv.Parser.RawRow + 1;

        // 9. Tiếp tục lặp khi `await csv.ReadAsync().WaitAsync(cancellationToken)` còn đúng.
        while (await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            // 10. Kiểm tra `rows.Count >= FlashcardImportValidation.MaxRows` để chọn nhánh xử lý phù hợp.
            if (rows.Count >= FlashcardImportValidation.MaxRows)
            {
                // 11. Dừng xử lý và phát sinh lỗi `new FlashcardImportException( $"Tệp nhập không được vượt quá {Flash...`.
                throw new FlashcardImportException(
                    $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxRows} dòng dữ liệu.");
            }
            // 12. Gọi `Add` để thực hiện bước nghiệp vụ này.
            rows.Add((nextRecordStartRow, csv.Parser.Record ?? []));
            // 13. Cập nhật `nextRecordStartRow` bằng giá trị mới.
            nextRecordStartRow = csv.Parser.RawRow + 1;
        }

        // 14. Trả kết quả từ `ParseRows` cho nơi gọi.
        return FlashcardImportValidation.ParseRows(headers, rows);
    }
}
