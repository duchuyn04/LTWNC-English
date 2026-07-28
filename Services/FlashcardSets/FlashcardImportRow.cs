using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Services.FlashcardSets;

// Một dòng thẻ hợp lệ đã được đọc và chuẩn hóa từ tệp nhập.
public sealed class FlashcardImportRow
{
    // Số dòng gốc giúp đối chiếu lỗi với tệp người dùng tải lên.
    public int RowNumber { get; init; }
    // Nội dung mặt trước và mặt sau của thẻ.
    public string FrontText { get; init; } = string.Empty;
    public string BackText { get; init; } = string.Empty;
    // Thông tin phát âm, loại từ và ví dụ bắt buộc.
    public string Pronunciation { get; init; } = string.Empty;
    public string PartOfSpeech { get; init; } = string.Empty;
    public string ExampleSentence { get; init; } = string.Empty;
    public string ExampleMeaning { get; init; } = string.Empty;
    // Từ đồng nghĩa và URL ảnh là dữ liệu không bắt buộc.
    public string? Synonyms { get; init; }
    public string? ImageUrl { get; init; }
}

// Kết quả đọc tệp gồm dòng hợp lệ, lỗi từng dòng và lỗi cấu trúc tệp.
public sealed class FlashcardFileParseResult
{
    // Các dòng hợp lệ có thể tiếp tục nhập vào cơ sở dữ liệu.
    public IReadOnlyList<FlashcardImportRow> Rows { get; init; } =
        Array.Empty<FlashcardImportRow>();

    // Các lỗi cụ thể gắn với số dòng trong tệp.
    public IReadOnlyList<FlashcardImportError> Errors { get; init; } =
        Array.Empty<FlashcardImportError>();

    // Danh sách tiêu đề cột bắt buộc còn thiếu.
    public IReadOnlyList<string> MissingRequiredHeaders { get; init; } =
        Array.Empty<string>();

    // Lỗi chung khiến toàn bộ tệp không thể xử lý.
    public string? FileError { get; init; }
}
