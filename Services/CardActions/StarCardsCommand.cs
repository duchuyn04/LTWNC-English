using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command gắn sao nhiều thẻ. Snapshot giữ IsStarred cũ để Undo.
public class StarCardsCommand : ICardActionCommand
{
    // Query / update Flashcards
    private readonly AppDbContext _context;

    // cardId -> IsStarred trước khi Execute (dùng Undo)
    private readonly Dictionary<int, bool> _previousStates = new();

    // Cố định "Star" cho log và factory
    public string ActionType => "Star";

    // Bộ thẻ chứa các thẻ đang thao tác
    public int SetId { get; }

    // User thực hiện (log)
    public string UserId { get; }

    // Id thẻ cần gắn sao
    public IReadOnlyList<int> CardIds { get; }

    // Tạo command với set, user và danh sách card id
    public StarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    // Chụp IsStarred cũ rồi set true cho mọi thẻ trong CardIds thuộc SetId
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
            card.IsStarred = true;
        }

        await _context.SaveChangesAsync();
    }

    // Gán lại IsStarred theo snapshot; thẻ không có trong snapshot thì bỏ qua
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

    // Nạp dictionary từ log; json null/rỗng thì để snapshot trống
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
