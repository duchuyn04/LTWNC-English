using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

public class UserProgress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardId { get; set; }

    public bool IsLearned { get; set; }

    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
