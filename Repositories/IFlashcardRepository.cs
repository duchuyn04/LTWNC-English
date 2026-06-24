using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IFlashcardRepository
{
    Task<List<Flashcard>> GetBySetIdAsync(int setId);
    Task<Flashcard?> GetByIdAsync(int id);
    Task AddAsync(Flashcard card);
    void Update(Flashcard card);
    void Delete(Flashcard card);
}
