using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Services.FlashcardSets;

public static class FlashcardImportValidation
{
    public const int MaxRows = 5000;
    public const int MaxColumns = 20;
    internal const string FrontTextHeader = "THUẬT NGỮ";
    internal const string BackTextHeader = "ĐỊNH NGHĨA";
    internal const string PronunciationHeader = "IPA";
    internal const string PartOfSpeechHeader = "LOẠI TỪ";
    internal const string ExampleSentenceHeader = "VÍ DỤ TIẾNG ANH";
    internal const string ExampleMeaningHeader = "NGHĨA VÍ DỤ TIẾNG VIỆT";
    internal const string SynonymsHeader = "TỪ ĐỒNG NGHĨA";
    internal const string ImageUrlHeader = "URL ẢNH";

    internal static readonly string[] RequiredHeaders =
    [
        FrontTextHeader,
        BackTextHeader,
        PronunciationHeader,
        PartOfSpeechHeader,
        ExampleSentenceHeader,
        ExampleMeaningHeader
    ];

    internal static string NormalizeHeader(string? header)
    {
        // 1. Trả kết quả từ `ToUpperInvariant` cho nơi gọi.
        return (header ?? string.Empty).Trim().ToUpperInvariant();
    }

    internal static FlashcardFileParseResult ParseRows(
        IReadOnlyList<string> headers,
        IEnumerable<(int RowNumber, IReadOnlyList<string> Values)> sourceRows)
    {
        // 1. Gọi `ToDictionary` và lưu kết quả vào `columns`.
        Dictionary<string, int> columns = headers
            .Select((header, index) => (Header: NormalizeHeader(header), Index: index))
            .Where(item => item.Header.Length > 0)
            .GroupBy(item => item.Header, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);

        // 2. Gọi `ToArray` và lưu kết quả vào `missingHeaders`.
        string[] missingHeaders = RequiredHeaders
            .Where(header => !columns.ContainsKey(header))
            .ToArray();

        // 3. Kiểm tra `missingHeaders.Length > 0` để chọn nhánh xử lý phù hợp.
        if (missingHeaders.Length > 0)
        {
            // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new FlashcardFileParseResult
            {
                MissingRequiredHeaders = missingHeaders,
                FileError = $"Tệp thiếu cột bắt buộc: {string.Join(", ", missingHeaders)}."
            };
        }

        // 5. Khởi tạo `rows` với dữ liệu ban đầu cần thiết.
        var rows = new List<FlashcardImportRow>();
        // 6. Khởi tạo `errors` với dữ liệu ban đầu cần thiết.
        var errors = new List<FlashcardImportError>();

        // 7. Duyệt từng phần tử trong `sourceRows` để xử lý lần lượt.
        foreach ((int rowNumber, IReadOnlyList<string> values) in sourceRows)
        {
            // 8. Kiểm tra `values.All(string.IsNullOrWhiteSpace)` để chọn nhánh xử lý phù hợp.
            if (values.All(string.IsNullOrWhiteSpace))
            {
                // 9. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 10. Gọi `GetRequired` và lưu kết quả vào `frontText`.
            string frontText = GetRequired(values, columns, FrontTextHeader);
            // 11. Gọi `GetRequired` và lưu kết quả vào `backText`.
            string backText = GetRequired(values, columns, BackTextHeader);
            // 12. Gọi `GetRequired` và lưu kết quả vào `pronunciation`.
            string pronunciation = GetRequired(values, columns, PronunciationHeader);
            // 13. Gọi `GetRequired` và lưu kết quả vào `partOfSpeech`.
            string partOfSpeech = GetRequired(values, columns, PartOfSpeechHeader);
            // 14. Gọi `GetRequired` và lưu kết quả vào `exampleSentence`.
            string exampleSentence = GetRequired(values, columns, ExampleSentenceHeader);
            // 15. Gọi `GetRequired` và lưu kết quả vào `exampleMeaning`.
            string exampleMeaning = GetRequired(values, columns, ExampleMeaningHeader);

            // 16. Tính giá trị và lưu vào `reason` để dùng ở bước tiếp theo.
            string? reason = RequiredError(frontText, "Thuật ngữ")
                ?? RequiredError(backText, "Định nghĩa")
                ?? RequiredError(pronunciation, "IPA")
                ?? RequiredError(partOfSpeech, "Loại từ")
                ?? RequiredError(exampleSentence, "Ví dụ tiếng Anh")
                ?? RequiredError(exampleMeaning, "Nghĩa ví dụ tiếng Việt");

            // 17. Kiểm tra `reason is null && partOfSpeech.Length > 80` để chọn nhánh xử lý phù hợp.
            if (reason is null && partOfSpeech.Length > 80)
            {
                // 18. Cập nhật `reason` bằng giá trị mới.
                reason = "Loại từ không được vượt quá 80 ký tự.";
            }

            // 19. Kiểm tra `reason is not null` để chọn nhánh xử lý phù hợp.
            if (reason is not null)
            {
                // 20. Gọi `Add` để thực hiện bước nghiệp vụ này.
                errors.Add(new FlashcardImportError { RowNumber = rowNumber, Reason = reason });
                // 21. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 22. Gọi `Add` để thực hiện bước nghiệp vụ này.
            rows.Add(new FlashcardImportRow
            {
                RowNumber = rowNumber,
                FrontText = frontText,
                BackText = backText,
                Pronunciation = pronunciation,
                PartOfSpeech = partOfSpeech,
                ExampleSentence = exampleSentence,
                ExampleMeaning = exampleMeaning,
                Synonyms = GetOptional(values, columns, SynonymsHeader),
                ImageUrl = GetOptional(values, columns, ImageUrlHeader)
            });
        }

        // 23. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new FlashcardFileParseResult { Rows = rows, Errors = errors };
    }

    private static string GetRequired(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        string header)
    {
        // 1. Tính giá trị và lưu vào `index` để dùng ở bước tiếp theo.
        int index = columns[header];
        // 2. Trả `index < values.Count ? values[index].Trim() : string.Empty` cho nơi gọi.
        return index < values.Count ? values[index].Trim() : string.Empty;
    }

    private static string? GetOptional(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        string header)
    {
        // 1. Kiểm tra `!columns.TryGetValue(header, out int index) || index >= values.Count` để chọn nhánh xử lý phù hợp.
        if (!columns.TryGetValue(header, out int index) || index >= values.Count)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `value`.
        string value = values[index].Trim();
        // 4. Trả `value.Length == 0 ? null : value` cho nơi gọi.
        return value.Length == 0 ? null : value;
    }

    private static string? RequiredError(string value, string fieldName)
    {
        // 1. Trả `value.Length == 0 ? $"{fieldName} không được để trống." : null` cho nơi gọi.
        return value.Length == 0 ? $"{fieldName} không được để trống." : null;
    }
}
