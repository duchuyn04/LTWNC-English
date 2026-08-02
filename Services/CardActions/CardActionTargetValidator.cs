using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

internal static class CardActionTargetValidator
{
    public static async Task<List<Flashcard>> ValidateAsync(
        AppDbContext context,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
    {
        if (setId <= 0)
        {
            throw new ArgumentException("Bộ thẻ không hợp lệ.", nameof(setId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Không có quyền thao tác trên bộ thẻ này.");
        }

        if (cardIds.Count == 0)
        {
            throw new ArgumentException("Chưa chọn thẻ nào.", nameof(cardIds));
        }

        if (cardIds.Any(cardId => cardId <= 0))
        {
            throw new ArgumentException("Danh sách id thẻ không hợp lệ.", nameof(cardIds));
        }

        if (cardIds.Count != cardIds.Distinct().Count())
        {
            throw new ArgumentException("Danh sách id thẻ không được trùng lặp.", nameof(cardIds));
        }

        bool isOwner = await context.FlashcardSets
            .AnyAsync(set => set.Id == setId && set.UserId == userId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("Không có quyền thao tác trên bộ thẻ này.");
        }

        List<Flashcard> cards = await context.Flashcards
            .Where(card => card.FlashcardSetId == setId && cardIds.Contains(card.Id))
            .ToListAsync();

        if (cards.Count != cardIds.Count
            || !cards.Select(card => card.Id).ToHashSet().SetEquals(cardIds))
        {
            throw new ArgumentException("Một hoặc nhiều id thẻ không thuộc bộ thẻ này.", nameof(cardIds));
        }

        return cards;
    }
}
