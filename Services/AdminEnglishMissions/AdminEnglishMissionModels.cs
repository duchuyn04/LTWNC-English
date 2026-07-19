using ltwnc.Models.Entities;

namespace ltwnc.Services.AdminEnglishMissions;

public sealed record AdminEnglishMissionQuery(
    string? Search = null,
    string? Topic = null,
    string? Status = null,
    string? Retention = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = AdminEnglishMissionService.DefaultPageSize);

public sealed record AdminEnglishMissionPage(
    IReadOnlyList<AdminEnglishMissionRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminEnglishMissionRow(
    int MissionId,
    int StudySessionId,
    string UserName,
    string Email,
    int FlashcardSetId,
    string FlashcardSetTitle,
    string Topic,
    string Title,
    string Status,
    int TurnCount,
    int? Score,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime RetentionDeadlineUtc,
    bool ConversationAvailable,
    bool HasRetentionHold);

public sealed record AdminEnglishMissionAccessCommand(
    int MissionId,
    string ActorUserId,
    string ActorDisplay,
    string IncidentType,
    string? CaseReference,
    string Reason,
    string? CorrelationId = null);

public sealed class AdminEnglishMissionConversationResult
{
    public bool Found { get; init; }
    public bool RequiresGate { get; init; }
    public string Message { get; init; } = string.Empty;
    public AdminEnglishMissionConversation? Conversation { get; init; }
}

public sealed record AdminEnglishMissionConversation(
    int MissionId,
    int StudySessionId,
    string UserName,
    string Email,
    string FlashcardSetTitle,
    string Topic,
    string Title,
    string Situation,
    string NpcName,
    string NpcRole,
    string OpeningLine,
    string Status,
    int TurnCount,
    int? Score,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime RetentionDeadlineUtc,
    string IncidentType,
    string? CaseReference,
    string Reason,
    IReadOnlyList<AdminEnglishMissionTargetWordRow> TargetWords,
    IReadOnlyList<AdminEnglishMissionTurnRow> Turns);

public sealed record AdminEnglishMissionTargetWordRow(
    string Term,
    string Definition,
    string? PartOfSpeech,
    bool IsUsed,
    int? FirstUsedTurn);

public sealed record AdminEnglishMissionTurnRow(
    int TurnNumber,
    string UserText,
    string NpcText,
    string? FeedbackVi,
    string? CorrectionEn,
    string? CorrectionExplanationVi,
    string UsedWordsDisplay,
    string AchievedGoalsDisplay,
    DateTime CreatedAtUtc);

public sealed record AdminEnglishMissionCleanupResult(
    int ScannedCount,
    int ClearedCount);
