using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Lưu chi tiết từng câu trả lời trong một phiên nghe chép
// Một StudySession có nhiều DictationSessionDetail
public class DictationSessionDetail
{
    // Khóa chính tự tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến phiên học
    [Required]
    public int StudySessionId { get; set; }

    // Khóa ngoại đến thẻ được hỏi
    [Required]
    public int FlashcardId { get; set; }

    // true nếu người dùng trả lời đúng
    public bool IsCorrect { get; set; }

    // Nội dung người dùng đã nhập
    public string AnsweredText { get; set; } = string.Empty;

    // Thời điểm trả lời
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property đến phiên học
    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }

    // Navigation property đến thẻ
    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
