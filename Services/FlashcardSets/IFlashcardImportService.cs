using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.FlashcardSets;

// Hợp đồng đọc tệp flashcard và nhập các dòng hợp lệ vào bộ thẻ.
public interface IFlashcardImportService
{
    // Chọn parser phù hợp và đọc tệp mà chưa ghi dữ liệu.
    Task<FlashcardFileParseResult> ParseAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    // Đọc tệp rồi lưu các thẻ hợp lệ vào bộ thẻ thuộc người dùng.
    Task<FlashcardImportResult> ImportAsync(
        int setId,
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
