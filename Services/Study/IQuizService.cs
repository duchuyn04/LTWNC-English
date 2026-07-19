using ltwnc.Models.Entities;

namespace ltwnc.Services.Study;

public interface IQuizService
{
    Task<QuizSetupState> GetSetupAsync(int setId, string userId);

    Task<StudySession> StartNewAsync(
        int setId,
        string userId,
        UserStudySettings settings,
        int timeLimitMinutes);

    Task<StudySession> StartOrResumeAsync(
        int setId,
        string userId,
        UserStudySettings settings);

    Task<QuizQuestionState> GetCurrentQuestionAsync(
        int setId,
        int sessionId,
        string userId);

    Task<QuizAnswerResult> AnswerAsync(
        int setId,
        int sessionId,
        int questionId,
        int selectedChoiceIndex,
        string userId);

    Task CompleteExpiredAsync(int setId, int sessionId, string userId);

    Task<QuizSessionResult> GetResultAsync(int setId, int sessionId, string userId);
    Task<StudySession> RetryWrongAsync(int setId, int sessionId, string userId);
    Task<StudySession> RetryAllAsync(int setId, int sessionId, string userId);
}
