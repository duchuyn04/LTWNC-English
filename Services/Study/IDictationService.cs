using ltwnc.Models.Entities;

namespace ltwnc.Services.Study;

// Contract nghe chép: lấy thẻ, tạo phiên, chấm đáp án, hoàn thành, xem kết quả.
// DTO (DictationCheckResult, DictationResult, …) vẫn là class concrete trong DictationService.cs.
public interface IDictationService
{
    Task<List<Flashcard>> GetCardsForDictationAsync(
        int setId,
        string userId,
        UserStudySettings settings);

    Task<bool> AnyCardHasExampleSentenceAsync(int setId);

    Task<StudySession> CreateSessionAsync(
        string userId,
        int setId,
        DictationContentMode contentMode = DictationContentMode.Vocabulary,
        int plannedItemCount = 0,
        IReadOnlyList<Flashcard>? cards = null);

    Task<DictationRetryPlan> GetRetryPlanAsync(
        int sourceSessionId,
        int setId,
        string userId);

    Task<List<DictationHistoryItem>> GetHistoryAsync(
        int setId,
        string userId,
        int limit = 100);

    Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int setId,
        int cardId,
        string answeredText,
        string userId,
        bool acceptSynonyms);

    Task<StudySession> CompleteSessionAsync(int sessionId, int setId, string userId);

    Task<DictationResult> GetSessionResultAsync(int sessionId, int setId, string userId);
}
