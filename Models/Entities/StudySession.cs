using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

public enum StudyMode
{
    Flashcard,
    Quiz,
    Write,
    Match
}

public class StudySession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardSetId { get; set; }

    public StudyMode Mode { get; set; } = StudyMode.Flashcard;

    public int? Score { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
