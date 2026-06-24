using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

public class FlashcardSet
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}
