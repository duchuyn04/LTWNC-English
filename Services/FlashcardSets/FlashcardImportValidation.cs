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

    internal static string NormalizeHeader(string? header) =>
        (header ?? string.Empty).Trim().ToUpperInvariant();

    internal static FlashcardFileParseResult ParseRows(
        IReadOnlyList<string> headers,
        IEnumerable<(int RowNumber, IReadOnlyList<string> Values)> sourceRows)
    {
        Dictionary<string, int> columns = headers
            .Select((header, index) => (Header: NormalizeHeader(header), Index: index))
            .Where(item => item.Header.Length > 0)
            .GroupBy(item => item.Header, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);

        string[] missingHeaders = RequiredHeaders
            .Where(header => !columns.ContainsKey(header))
            .ToArray();

        if (missingHeaders.Length > 0)
        {
            return new FlashcardFileParseResult
            {
                MissingRequiredHeaders = missingHeaders,
                FileError = $"Tệp thiếu cột bắt buộc: {string.Join(", ", missingHeaders)}."
            };
        }

        var rows = new List<FlashcardImportRow>();
        var errors = new List<FlashcardImportError>();

        foreach ((int rowNumber, IReadOnlyList<string> values) in sourceRows)
        {
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string frontText = GetRequired(values, columns, FrontTextHeader);
            string backText = GetRequired(values, columns, BackTextHeader);
            string pronunciation = GetRequired(values, columns, PronunciationHeader);
            string partOfSpeech = GetRequired(values, columns, PartOfSpeechHeader);
            string exampleSentence = GetRequired(values, columns, ExampleSentenceHeader);
            string exampleMeaning = GetRequired(values, columns, ExampleMeaningHeader);

            string? reason = RequiredError(frontText, "Thuật ngữ")
                ?? RequiredError(backText, "Định nghĩa")
                ?? RequiredError(pronunciation, "IPA")
                ?? RequiredError(partOfSpeech, "Loại từ")
                ?? RequiredError(exampleSentence, "Ví dụ tiếng Anh")
                ?? RequiredError(exampleMeaning, "Nghĩa ví dụ tiếng Việt");

            if (reason is null && partOfSpeech.Length > 80)
            {
                reason = "Loại từ không được vượt quá 80 ký tự.";
            }

            if (reason is not null)
            {
                errors.Add(new FlashcardImportError { RowNumber = rowNumber, Reason = reason });
                continue;
            }

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

        return new FlashcardFileParseResult { Rows = rows, Errors = errors };
    }

    private static string GetRequired(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        string header)
    {
        int index = columns[header];
        return index < values.Count ? values[index].Trim() : string.Empty;
    }

    private static string? GetOptional(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        string header)
    {
        if (!columns.TryGetValue(header, out int index) || index >= values.Count)
        {
            return null;
        }

        string value = values[index].Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? RequiredError(string value, string fieldName) =>
        value.Length == 0 ? $"{fieldName} không được để trống." : null;
}
