using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class Lesson
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    [Required]
    public string ContentMarkdown { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Status { get; set; } = LessonStatus.Draft;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }
}

public static class LessonStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";

    public static bool IsValid(string? status) =>
        status is Draft or Published;
}
