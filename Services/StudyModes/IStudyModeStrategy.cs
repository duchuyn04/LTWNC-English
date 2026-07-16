using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy một chế độ học: lấy thẻ phù hợp + build option trên Study Hub.
public interface IStudyModeStrategy
{
    // Mode mà strategy này phụ trách (mỗi mode chỉ một strategy trong DI)
    StudyMode Mode { get; }

    // Lấy thẻ cho mode, áp dụng bộ lọc trong settings (userId null = khách)
    Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId);

    // Option hiển thị: tên, mô tả, số thẻ, có học được không...
    StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings);

    Task<StudyModeOptionViewModel> BuildOptionAsync(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings,
        string? userId)
    {
        return Task.FromResult(BuildOption(setId, cards, settings));
    }
}
