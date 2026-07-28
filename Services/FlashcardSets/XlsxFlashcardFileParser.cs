using ClosedXML.Excel;
using System.IO.Compression;

namespace ltwnc.Services.FlashcardSets;

public sealed class XlsxFlashcardFileParser : IFlashcardFileParser
{
    public Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ThrowIfNull` để thực hiện bước nghiệp vụ này.
        ArgumentNullException.ThrowIfNull(stream);
        // 2. Gọi `ThrowIfCancellationRequested` để thực hiện bước nghiệp vụ này.
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Gọi `ValidateArchiveSize` để thực hiện bước nghiệp vụ này.
        ValidateArchiveSize(stream);

        // 4. Khởi tạo `workbook` với dữ liệu ban đầu cần thiết.
        using var workbook = new XLWorkbook(stream);
        // 5. Gọi `First` và lưu kết quả vào `worksheet`.
        IXLWorksheet worksheet = workbook.Worksheets.First();
        // 6. Tính giá trị và lưu vào `lastColumn` để dùng ở bước tiếp theo.
        int lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        // 7. Tính giá trị và lưu vào `lastRow` để dùng ở bước tiếp theo.
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        // 8. Kiểm tra `lastColumn > FlashcardImportValidation.MaxColumns` để chọn nhánh xử lý phù hợp.
        if (lastColumn > FlashcardImportValidation.MaxColumns)
        {
            // 9. Dừng xử lý và phát sinh lỗi `new FlashcardImportException( $"Tệp nhập không được vượt quá {Flash...`.
            throw new FlashcardImportException(
                $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxColumns} cột.");
        }
        // 10. Kiểm tra `lastRow > FlashcardImportValidation.MaxRows + 1` để chọn nhánh xử lý phù hợp.
        if (lastRow > FlashcardImportValidation.MaxRows + 1)
        {
            // 11. Dừng xử lý và phát sinh lỗi `new FlashcardImportException( $"Tệp nhập không được vượt quá {Flash...`.
            throw new FlashcardImportException(
                $"Tệp nhập không được vượt quá {FlashcardImportValidation.MaxRows} dòng dữ liệu.");
        }

        // 12. Tính giá trị và lưu vào `headers` để dùng ở bước tiếp theo.
        string[] headers = lastColumn == 0
            ? []
            : Enumerable.Range(1, lastColumn)
                .Select(column => worksheet.Cell(1, column).GetString())
                .ToArray();

        // 13. Khởi tạo `rows` với dữ liệu ban đầu cần thiết.
        var rows = new List<(int RowNumber, IReadOnlyList<string> Values)>();
        // 14. Lặp qua phạm vi dữ liệu cần xử lý.
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            // 15. Gọi `ThrowIfCancellationRequested` để thực hiện bước nghiệp vụ này.
            cancellationToken.ThrowIfCancellationRequested();
            // 16. Gọi `ToArray` và lưu kết quả vào `values`.
            string[] values = Enumerable.Range(1, lastColumn)
                .Select(column => worksheet.Cell(rowNumber, column).GetString())
                .ToArray();
            // 17. Gọi `Add` để thực hiện bước nghiệp vụ này.
            rows.Add((rowNumber, values));
        }

        // 18. Trả kết quả từ `FromResult` cho nơi gọi.
        return Task.FromResult(FlashcardImportValidation.ParseRows(headers, rows));
    }

    private static void ValidateArchiveSize(Stream stream)
    {
        // 1. Tính giá trị và lưu vào `maxExpandedBytes` để dùng ở bước tiếp theo.
        const long maxExpandedBytes = 50L * 1024 * 1024;
        // 2. Tính giá trị và lưu vào `maxEntries` để dùng ở bước tiếp theo.
        const int maxEntries = 1000;
        // 3. Kiểm tra `!stream.CanSeek` để chọn nhánh xử lý phù hợp.
        if (!stream.CanSeek)
        {
            // 4. Dừng xử lý và phát sinh lỗi `new FlashcardImportException("Luồng XLSX phải hỗ trợ seek để kiểm t...`.
            throw new FlashcardImportException("Luồng XLSX phải hỗ trợ seek để kiểm tra an toàn.");
        }

        // 5. Tính giá trị và lưu vào `originalPosition` để dùng ở bước tiếp theo.
        long originalPosition = stream.Position;
        // 6. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 7. Khởi tạo `archive` với dữ liệu ban đầu cần thiết.
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            // 8. Kiểm tra `archive.Entries.Count > maxEntries || archive.Entries.Sum(entry => ...` để chọn nhánh xử lý phù hợp.
            if (archive.Entries.Count > maxEntries
                || archive.Entries.Sum(entry => entry.Length) > maxExpandedBytes)
            {
                // 9. Dừng xử lý và phát sinh lỗi `new FlashcardImportException("Tệp XLSX giải nén quá lớn hoặc có quá...`.
                throw new FlashcardImportException("Tệp XLSX giải nén quá lớn hoặc có quá nhiều thành phần.");
            }
        }
        finally
        {
            // 10. Cập nhật `stream.Position` bằng giá trị mới.
            stream.Position = originalPosition;
        }
    }
}
