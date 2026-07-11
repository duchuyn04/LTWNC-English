using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Contract cho từng chế độ học (Flashcard, Dictation, Quiz...).
// Mỗi implementation chịu trách nhiệm duy nhất về:
// - cách lấy danh sách thẻ phù hợp
// - cách hiển thị option trên Study Hub
public interface IStudyModeStrategy
{
    // Mode mà strategy này đại diện (không được trùng lặp trong DI)
    StudyMode Mode { get; }

    // Lấy danh sách thẻ cho chế độ học này, áp dụng bộ lọc từ settings.
    // userId có thể null với user ẩn danh.
    Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId);

    // Xây dựng option hiển thị trên Study Hub (tên, mô tả, số thẻ, khả dụng...)
    StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings);
}
