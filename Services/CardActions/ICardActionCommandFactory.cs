namespace ltwnc.Services.CardActions;

// Map action type từ form/API ("Delete" | "Star" | "Unstar") sang ICardActionCommand.
public interface ICardActionCommandFactory
{
    ICardActionCommand Create(
        string actionType,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds);
}
