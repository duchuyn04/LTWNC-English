using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public class Flashcard
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int FlashcardSetId { get; set; }

    [Required]
    public string FrontText { get; set; } = string.Empty;

    [Required]
    public string BackText { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    // Navigation
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
