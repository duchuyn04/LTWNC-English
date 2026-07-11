namespace ltwnc.Services.CardActions;

public interface ICardActionCommand
{
    string ActionType { get; }
    int SetId { get; }
    string UserId { get; }
    IReadOnlyList<int> CardIds { get; }

    Task ExecuteAsync();
    Task UndoAsync();

    string GetSnapshotJson();
    void LoadSnapshot(string json);
}
