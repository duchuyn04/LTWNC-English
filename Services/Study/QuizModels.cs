namespace ltwnc.Services.Study;

public sealed class QuizUnavailableException : InvalidOperationException
{
    public QuizUnavailableException(string message) : base(message) { }
}

public sealed record QuizPoolAvailability(
    bool IsAvailable,
    int DistinctTermCount,
    int DistinctDefinitionCount,
    string? UnavailableReason);
