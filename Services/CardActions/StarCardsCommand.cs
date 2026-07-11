using System.Text.Json;
using ltwnc.Data;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command đánh sao nhiều thẻ cùng lúc
// Snapshot lưu trạng thái IsStarred cũ của từng thẻ để hoàn tác chính xác
// Command đánh sao nhiều thẻ, lưu trạng thái cũ để hoàn tác
public class StarCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly Dictionary<int, bool> _previousStates = new();

    public string ActionType => "Star";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public StarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    public async Task ExecuteAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        // Ghi nhớ trạng thái cũ trước khi đánh sao
        _previousStates.Clear();
        foreach (var card in cards)
        {
            _previousStates[card.Id] = card.IsStarred;
            card.IsStarred = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        // Khôi phục trạng thái sao cũ; bỏ qua thẻ không có trong snapshot
        foreach (var card in cards)
            if (_previousStates.TryGetValue(card.Id, out var oldState)) card.IsStarred = oldState;

        await _context.SaveChangesAsync();
    }

    public string GetSnapshotJson() => JsonSerializer.Serialize(_previousStates);

    public void LoadSnapshot(string json)
    {
        _previousStates.Clear();
        foreach (var (id, state) in JsonSerializer.Deserialize<Dictionary<int, bool>>(json) ?? [])
            _previousStates[id] = state;
    }
}
