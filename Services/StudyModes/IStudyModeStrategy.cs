using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Mỗi chế độ học (Flashcard, Dictation...) sẽ có một class riêng thực hiện interface này
public interface IStudyModeStrategy
{
    // Chế độ học mà strategy này phụ trách, ví dụ StudyMode.Flashcard
    StudyMode Mode { get; }

    // Lấy danh sách thẻ phù hợp với chế độ học và bộ lọc (sao, chưa thuộc...)
    Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId);

    // Tạo thông tin hiển thị trên trang chọn chế độ học (số thẻ, thờ gian dự kiến, có khả dụng không...)
    StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings);
}
