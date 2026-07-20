using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.Study;

// Contract nghiệp vụ học: settings, tiến độ, hub, đánh dấu thuộc, hoàn thành phiên.
// Không lọc thẻ mode (strategy) và không mở huy hiệu (observer) — những việc đó nằm trong implementation.
public interface IStudyService
{
    Task<List<Flashcard>> GetCardsForModeAsync(
        StudyMode mode,
        int setId,
        UserStudySettings settings,
        string? userId);

    Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId);

    Task<UserStudySettings> GetSettingsAsync(string? userId);

    Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input);

    Task SaveFilterSettingsAsync(string userId, bool? starredOnly, bool? unlearnedOnly);

    Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned);

    Task<StudySession> StartSessionAsync(string userId, int setId, StudyMode mode);

    Task CompleteSessionAsync(string userId, int setId, int sessionId);

    Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId);
}
