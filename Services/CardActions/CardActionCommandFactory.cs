using ltwnc.Data;

namespace ltwnc.Services.CardActions;

// Map chuỗi action type từ form/API sang command concrete.
// Controller không new trực tiếp Delete/Star/Unstar.
public class CardActionCommandFactory : ICardActionCommandFactory
{
    // Truyền vào constructor từng command (cần DbContext)
    private readonly AppDbContext _context;

    // Inject DbContext dùng chung cho mọi command tạo ra
    public CardActionCommandFactory(AppDbContext context)
    {
        _context = context;
    }

    // actionType: "Delete" | "Star" | "Unstar". Sai type thì throw.
    public ICardActionCommand Create(
        string actionType,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
    {
        if (actionType == "Delete")
        {
            return new DeleteCardsCommand(_context, setId, userId, cardIds);
        }

        if (actionType == "Star")
        {
            return new StarCardsCommand(_context, setId, userId, cardIds);
        }

        if (actionType == "Unstar")
        {
            return new UnstarCardsCommand(_context, setId, userId, cardIds);
        }

        throw new InvalidOperationException($"Unknown action type: {actionType}.");
    }
}
