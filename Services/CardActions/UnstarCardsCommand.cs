using System.Text.Json;
using ltwnc.Data;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command bỏ sao nhiều thẻ cùng lúc
// Snapshot lưu trạng thái IsStarred cũ để hoàn tác về đúng trạng thái ban đầu
// Command bỏ sao nhiều thẻ, lưu trạng thái cũ để hoàn tác
public class UnstarCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly Dictionary<int, bool> _previousStates = new();

    public string ActionType => "Unstar";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public UnstarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
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

        // Ghi nhớ trạng thái cũ trước khi bỏ sao
        _previousStates.Clear();
        foreach (var card in cards)
        {
            _previousStates[card.Id] = card.IsStarred;
            card.IsStarred = false;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        // Khôi phục trạng thái sao cũ
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
