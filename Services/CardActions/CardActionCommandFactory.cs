namespace ltwnc.Services.CardActions;

// Chọn Concrete Creator theo action type rồi gọi Factory Method của creator đó.
public class CardActionCommandFactory : ICardActionCommandFactory
{
    private readonly IReadOnlyList<CardActionCommandCreator> _creators;

    public CardActionCommandFactory(IEnumerable<CardActionCommandCreator> creators)
    {
        _creators = creators.ToList();
    }

    public ICardActionCommand Create(
        string actionType,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
    {
        List<CardActionCommandCreator> matches = _creators
            .Where(creator => creator.ActionType == actionType)
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Unknown action type: {actionType}.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple command creators are registered for action type: {actionType}.");
        }

        return matches[0].Create(setId, userId, cardIds);
    }
}
