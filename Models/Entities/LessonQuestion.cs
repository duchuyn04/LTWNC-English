using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class LessonQuestion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LessonId { get; set; }

    [Required, MaxLength(40)]
    public string Type { get; set; } = LessonQuestionTypes.MultipleChoice;

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>JSON array of option strings (MCQ).</summary>
    public string? OptionsJson { get; set; }

    public int? CorrectOptionIndex { get; set; }

    /// <summary>JSON array of accepted answers (Writing — ticket 03).</summary>
    public string? AcceptedAnswersJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Lesson? Lesson { get; set; }
}

public static class LessonQuestionTypes
{
    public const string MultipleChoice = "MultipleChoice";
    public const string Writing = "Writing";
}
