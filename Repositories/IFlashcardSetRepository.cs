using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IFlashcardSetRepository
{
    Task<List<FlashcardSet>> GetByUserIdAsync(string userId);
    Task<List<FlashcardSet>> GetPublicSetsAsync();
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);
    Task<FlashcardSet?> GetByIdAsync(int id);
    Task<FlashcardSet?> GetByIdWithCardsAsync(int id);
    Task AddAsync(FlashcardSet set);
    void Update(FlashcardSet set);
    void Delete(FlashcardSet set);
    Task SaveChangesAsync();
}
