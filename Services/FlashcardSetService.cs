using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class FlashcardSetService : IFlashcardSetService
{
    private readonly IFlashcardSetRepository _setRepo;
    private readonly IFlashcardRepository _cardRepo;

    public FlashcardSetService(IFlashcardSetRepository setRepo, IFlashcardRepository cardRepo)
    {
        _setRepo = setRepo;
        _cardRepo = cardRepo;
    }

    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        return await _setRepo.GetByUserIdAsync(userId);
    }

    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _setRepo.GetPublicSetsAsync();
    }

    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _setRepo.SearchPublicSetsAsync(query);
    }

    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        return await _setRepo.GetByIdAsync(id);
    }

    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(id);
        if (set == null || set.UserId != userId) return null;
        return set;
    }

    public async Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId)
    {
        var set = new FlashcardSet
        {
            Title = title,
            Description = description,
            IsPublic = isPublic,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _setRepo.AddAsync(set);
        await _setRepo.SaveChangesAsync();
        return set;
    }

    public async Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        set.Title = title;
        set.Description = description;
        set.IsPublic = isPublic;
        set.UpdatedAt = DateTime.UtcNow;
        _setRepo.Update(set);
        await _setRepo.SaveChangesAsync();
    }

    public async Task DeleteSetAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");
        _setRepo.Delete(set);
        await _setRepo.SaveChangesAsync();
    }

    public async Task<Flashcard> AddCardAsync(int setId, string frontText, string backText, string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        var maxOrder = set.Flashcards.Any() ? set.Flashcards.Max(f => f.OrderIndex) : 0;
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            OrderIndex = maxOrder + 1
        };
        await _cardRepo.AddAsync(card);
        await _setRepo.SaveChangesAsync();
        return card;
    }

    public async Task<int> UpdateCardAsync(int cardId, string frontText, string backText, string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");
        card.FrontText = frontText;
        card.BackText = backText;
        _cardRepo.Update(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }

    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");
        _cardRepo.Delete(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }
}
