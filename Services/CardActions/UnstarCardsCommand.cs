using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command bỏ sao nhiều thẻ. Snapshot giữ IsStarred cũ để Undo.
public class UnstarCardsCommand : ICardActionCommand
{
    // Query / update Flashcards
    private readonly AppDbContext _context;

    // cardId -> IsStarred trước khi Execute
    private readonly Dictionary<int, bool> _previousStates = new();

    // Cố định "Unstar"
    public string ActionType => "Unstar";

    // Bộ thẻ đang thao tác
    public int SetId { get; }

    // User thực hiện
    public string UserId { get; }

    // Id thẻ cần bỏ sao
    public IReadOnlyList<int> CardIds { get; }

    // Tạo command với set, user và danh sách card id
    public UnstarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    // Chụp IsStarred cũ rồi set false
    public async Task ExecuteAsync()
    {
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        _previousStates.Clear();

        foreach (Flashcard card in cards)
        {
            _previousStates[card.Id] = card.IsStarred;
            card.IsStarred = false;
        }

        await _context.SaveChangesAsync();
    }

    // Khôi phục IsStarred theo snapshot
    public async Task UndoAsync()
    {
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        foreach (Flashcard card in cards)
        {
            if (_previousStates.TryGetValue(card.Id, out bool oldState))
            {
                card.IsStarred = oldState;
            }
        }

        await _context.SaveChangesAsync();
    }

    // JSON dictionary cardId -> IsStarred cũ
    public string GetSnapshotJson()
    {
        return JsonSerializer.Serialize(_previousStates);
    }

    // Nạp snapshot từ log
    public void LoadSnapshot(string json)
    {
        _previousStates.Clear();

        Dictionary<int, bool>? loaded =
            JsonSerializer.Deserialize<Dictionary<int, bool>>(json);

        if (loaded == null)
        {
            return;
        }

        foreach (KeyValuePair<int, bool> pair in loaded)
        {
            _previousStates[pair.Key] = pair.Value;
        }
    }
}
