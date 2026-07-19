using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public class EnglishMission
{
    [Key] public int Id { get; set; }
    [Required] public int StudySessionId { get; set; }
    [Required, MaxLength(80)] public string Topic { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Required] public string Situation { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string NpcName { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string NpcRole { get; set; } = string.Empty;
    [Required] public string OpeningLine { get; set; } = string.Empty;
    [Required] public string GoalsJson { get; set; } = "[]";
    [Required, MaxLength(40)] public string Status { get; set; } = "Active";
    public int TurnCount { get; set; }
    public int? Score { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(StudySessionId))] public StudySession? StudySession { get; set; }
    public ICollection<EnglishMissionTargetWord> TargetWords { get; set; } = new List<EnglishMissionTargetWord>();
    public ICollection<EnglishMissionTurn> Turns { get; set; } = new List<EnglishMissionTurn>();
}

public class EnglishMissionTargetWord
{
    [Key] public int Id { get; set; }
    [Required] public int EnglishMissionId { get; set; }
    [Required] public int FlashcardId { get; set; }
    [Required, MaxLength(160)] public string Term { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Definition { get; set; } = string.Empty;
    [MaxLength(80)] public string? PartOfSpeech { get; set; }
    [MaxLength(1000)] public string? ExampleSentence { get; set; }
    public bool IsUsed { get; set; }
    public int? FirstUsedTurn { get; set; }
    public EnglishMission? Mission { get; set; }
    public Flashcard? Flashcard { get; set; }
}

public class EnglishMissionTurn
{
    [Key] public int Id { get; set; }
    [Required] public int EnglishMissionId { get; set; }
    public int TurnNumber { get; set; }
    [Required, MaxLength(64)] public string ClientTurnId { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string UserText { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string NpcText { get; set; } = string.Empty;
    [MaxLength(1000)] public string? FeedbackVi { get; set; }
    [MaxLength(1000)] public string? CorrectionEn { get; set; }
    [MaxLength(1000)] public string? CorrectionExplanationVi { get; set; }
    [Required] public string UsedWordsJson { get; set; } = "[]";
    [Required] public string AchievedGoalsJson { get; set; } = "[]";
    [MaxLength(120)] public string? ProviderName { get; set; }
    [MaxLength(200)] public string? ModelId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public EnglishMission? Mission { get; set; }
}
