using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public enum QuizQuestionDirection
{
    TermToDefinition,
    DefinitionToTerm
}

public class QuizSessionQuestion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StudySessionId { get; set; }

    [Required]
    public int FlashcardId { get; set; }

    public int OrderIndex { get; set; }
    public QuizQuestionDirection Direction { get; set; }

    [Required]
    public string PromptText { get; set; } = string.Empty;

    [Required]
    public string Choice1Text { get; set; } = string.Empty;

    [Required]
    public string Choice2Text { get; set; } = string.Empty;

    [Required]
    public string Choice3Text { get; set; } = string.Empty;

    [Required]
    public string Choice4Text { get; set; } = string.Empty;

    public int CorrectChoiceIndex { get; set; }
    public int? SelectedChoiceIndex { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime? AnsweredAt { get; set; }

    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }

    [NotMapped]
    public IReadOnlyList<string> Choices =>
        new[] { Choice1Text, Choice2Text, Choice3Text, Choice4Text };
}
