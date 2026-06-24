using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IStudySessionRepository
{
    Task AddAsync(StudySession session);
    Task<UserProgress?> GetProgressAsync(string userId, int flashcardId);
    Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId);
    void UpdateProgress(UserProgress progress);
    Task AddProgressAsync(UserProgress progress);
}
