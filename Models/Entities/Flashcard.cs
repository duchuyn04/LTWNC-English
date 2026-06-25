using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Entity đại diện cho bảng Flashcards — một thẻ flashcard
// Mỗi thẻ có 2 mặt: FrontText (tiếng Anh) và BackText (nghĩa tiếng Việt)
public class Flashcard
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến bộ thẻ chứa thẻ này
    [Required]
    public int FlashcardSetId { get; set; }

    // Nội dung mặt trước (tiếng Anh) — bắt buộc
    [Required]
    public string FrontText { get; set; } = string.Empty;

    // Nội dung mặt sau (nghĩa tiếng Việt) — bắt buộc
    [Required]
    public string BackText { get; set; } = string.Empty;

    // Thứ tự hiển thị trong bộ thẻ (0, 1, 2, ...)
    public int OrderIndex { get; set; }

    // Navigation property — liên kết đến bộ thẻ chứa thẻ này
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
