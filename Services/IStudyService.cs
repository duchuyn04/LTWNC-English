using ltwnc.Models.Entities;

namespace ltwnc.Services;

public interface IStudyService
{
    Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId);
    Task MarkLearnedAsync(string userId, int flashcardId, bool learned);
    Task CompleteSessionAsync(string userId, int setId, StudyMode mode);
}
