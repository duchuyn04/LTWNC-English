using ltwnc.Data;

namespace ltwnc.Services.CardActions;

// Factory tạo các command xử lý thẻ theo action type
// Là nơi duy nhất chứa switch trên loại hành động cụ thể
public class CardActionCommandFactory
{
    private readonly AppDbContext _context;

    public CardActionCommandFactory(AppDbContext context)
    {
        _context = context;
    }

    public ICardActionCommand Create(string actionType, int setId, string userId, IReadOnlyList<int> cardIds)
    {
        return actionType switch
        {
            "Delete" => new DeleteCardsCommand(_context, setId, userId, cardIds),
            "Star" => new StarCardsCommand(_context, setId, userId, cardIds),
            "Unstar" => new UnstarCardsCommand(_context, setId, userId, cardIds),
            _ => throw new InvalidOperationException($"Unknown action type: {actionType}.")
        };
    }
}
