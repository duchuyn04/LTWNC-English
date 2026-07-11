using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using ltwnc.Models;

namespace ltwnc.Models.Entities;

// Entity đại diện cho bảng FlashcardSets — bộ thẻ flashcard
// Quan hệ: 1 User có nhiều FlashcardSet, 1 FlashcardSet có nhiều Flashcard
public class FlashcardSet : IPrototype<FlashcardSet>
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Tiêu đề bộ thẻ — bắt buộc, tối đa 200 ký tự
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Mô tả bộ thẻ — không bắt buộc
    public string? Description { get; set; }

    // Khóa ngoại đến người tạo bộ thẻ (bảng AspNetUsers)
    [Required]
    public string UserId { get; set; } = string.Empty;

    // true = mọi người xem được, false = chỉ chủ sở hữu xem
    public bool IsPublic { get; set; } = true;

    // Khóa ngoại đến bộ thẻ nguồn khi bộ này là bản sao của một bộ công khai
    public int? SourceSetId { get; set; }

    // Thời gian tạo bộ thẻ
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thời gian cập nhật gần nhất
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — liên kết đến người tạo
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    // Navigation property — danh sách thẻ trong bộ
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();

    // Tạo bản sao độc lập của bộ thẻ, bao gồm cả các thẻ con.
    // Không gán UserId/SourceSetId/IsPublic vì đó là trách nhiệm của service.
    public FlashcardSet Clone()
    {
        var now = DateTime.UtcNow;
        return new FlashcardSet
        {
            Id = 0,
            Title = Title,
            Description = Description,
            UserId = string.Empty,
            SourceSetId = null,
            CreatedAt = now,
            UpdatedAt = now,
            Flashcards = Flashcards
                .OrderBy(card => card.OrderIndex)
                .Select(card => card.Clone())
                .ToList()
        };
    }
}
