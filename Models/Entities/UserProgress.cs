using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Trạng thái học chi tiết của một thẻ
public enum UserProgressStatus
{
    // Chưa học
    Unlearned = 0,
    // Đang học / vừa trả lời sai
    Learning = 1,
    // Đã thuộc / vừa trả lời đúng
    Mastered = 2
}

// Tiến độ user trên một thẻ. Mỗi cặp (UserId, FlashcardId) một dòng.
public class UserProgress
{
    [Key]
    public int Id { get; set; }

    // Id user trong bảng Users (cookie auth)
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardId { get; set; }

    // true = đã thuộc (filter UnlearnedOnly dựa vào đây)
    public bool IsLearned { get; set; }

    // Enum chi tiết hơn IsLearned
    public UserProgressStatus Status { get; set; } = UserProgressStatus.Unlearned;

    // Đếm lần đúng
    public int CorrectCount { get; set; }

    // Đếm lần sai
    public int WrongCount { get; set; }

    // Lần ôn gần nhất (UTC)
    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
