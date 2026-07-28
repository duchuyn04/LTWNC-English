namespace ltwnc.Services.FlashcardSets;

// Hợp đồng đọc một định dạng tệp flashcard cụ thể.
public interface IFlashcardFileParser
{
    // Đọc stream, chuẩn hóa từng dòng và trả dữ liệu cùng danh sách lỗi.
    Task<FlashcardFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
