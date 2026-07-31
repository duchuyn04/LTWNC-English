using ltwnc.Data;

namespace ltwnc.Services.CardActions;

// GoF Creator: Create giữ quy trình chung, còn Factory Method được class con quyết định.
public abstract class CardActionCommandCreator
{
    protected CardActionCommandCreator(AppDbContext context)
    {
        Context = context;
    }

    protected AppDbContext Context { get; }

    public abstract string ActionType { get; }

    public ICardActionCommand Create(
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
        => CreateCommand(setId, userId, cardIds);

    protected abstract ICardActionCommand CreateCommand(
        int setId,
        string userId,
        IReadOnlyList<int> cardIds);
}

public sealed class DeleteCardsCommandCreator : CardActionCommandCreator
{
    public DeleteCardsCommandCreator(AppDbContext context) : base(context)
    {
    }

    public override string ActionType => "Delete";

    protected override ICardActionCommand CreateCommand(
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
        => new DeleteCardsCommand(Context, setId, userId, cardIds);
}

public sealed class StarCardsCommandCreator : CardActionCommandCreator
{
    public StarCardsCommandCreator(AppDbContext context) : base(context)
    {
    }

    public override string ActionType => "Star";

    protected override ICardActionCommand CreateCommand(
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
        => new StarCardsCommand(Context, setId, userId, cardIds);
}

public sealed class UnstarCardsCommandCreator : CardActionCommandCreator
{
    public UnstarCardsCommandCreator(AppDbContext context) : base(context)
    {
    }

    public override string ActionType => "Unstar";

    protected override ICardActionCommand CreateCommand(
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
        => new UnstarCardsCommand(Context, setId, userId, cardIds);
}
