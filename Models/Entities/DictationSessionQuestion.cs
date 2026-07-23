using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Snapshot một câu hỏi Dictation tại thời điểm bắt đầu phiên học.
public class DictationSessionQuestion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StudySessionId { get; set; }

    [Required]
    public int FlashcardId { get; set; }

    public int OrderIndex { get; set; }

    [Required]
    public string PromptText { get; set; } = string.Empty;

    [Required]
    public string CorrectAnswer { get; set; } = string.Empty;

    [Required]
    public string Term { get; set; } = string.Empty;

    [Required]
    public string Definition { get; set; } = string.Empty;

    [Required]
    public string Pronunciation { get; set; } = string.Empty;

    [Required]
    public string ExampleSentence { get; set; } = string.Empty;

    [Required]
    public string ExampleMeaning { get; set; } = string.Empty;

    public string? Synonyms { get; set; }

    public string? AnsweredText { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime? AnsweredAt { get; set; }

    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }
}
