using System.ComponentModel.DataAnnotations;
using ltwnc.Models;

namespace ltwnc.Models.Entities;

// Bảng FlashcardSets: bộ thẻ. 1 User nhiều set; 1 set nhiều Flashcard.
public class FlashcardSet : IPrototype<FlashcardSet>
{
    // PK tự tăng
    [Key]
    public int Id { get; set; }

    // Tiêu đề, bắt buộc, max 200
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Mô tả, optional
    public string? Description { get; set; }

    // Id user trong bảng Users (cookie auth)
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

    // Thẻ trong bộ (cần Include trước Clone)
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
