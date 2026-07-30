using ltwnc.Services.Audit;

namespace ltwnc.Services.ContentModeration;

public sealed record QuarantineFromReportCommand(
    long ReportId,
    int ReportVersion,
    int FlashcardSetVersion,
    AdminActorContext Actor,
    string PublicReason,
    string? InternalNote,
    string? Evidence,
    bool Confirmed);

public sealed record RestoreFlashcardSetCommand(
    int FlashcardSetId,
    int Version,
    AdminActorContext Actor,
    string Reason,
    bool Confirmed);

public sealed record ContentModerationOperationResult(bool Succeeded, string Message)
{
    public static ContentModerationOperationResult Success(string message) => new(true, message);

    public static ContentModerationOperationResult Failure(string message) => new(false, message);
}
