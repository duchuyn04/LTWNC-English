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

    // ID của bộ thẻ nguồn khi bộ này được sao chép.
    // Không cấu hình foreign key để bản sao vẫn tồn tại nếu bộ nguồn bị xóa.
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

    // Tạo bản sao độc lập của bộ thẻ, bao gồm deep-clone các thẻ con.
    // Tiền điều kiện: Flashcards phải là danh sách thẻ đầy đủ muốn nhân bản.
    // Nếu object lấy từ EF, caller phải Include navigation trước khi gọi Clone.
    // Giữ: Title, Description, và nội dung từng thẻ (qua Flashcard.Clone).
    // Reset: Id, UserId, SourceSetId, IsPublic (luôn private), timestamps.
    // Service gán lại UserId, SourceSetId và có thể khẳng định lại IsPublic theo nghiệp vụ.
    public FlashcardSet Clone()
    {
        var now = DateTime.UtcNow;
        return new FlashcardSet
        {
            Title = Title,
            Description = Description,
            // Không mang chính sách công khai của nguồn; bản clone mặc định private.
            IsPublic = false,
            CreatedAt = now,
            UpdatedAt = now,
            Flashcards = Flashcards
                .OrderBy(card => card.OrderIndex)
                .Select(card => card.Clone())
                .ToList()
        };
    }
}
