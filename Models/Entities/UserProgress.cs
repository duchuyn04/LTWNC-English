using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

public enum UserProgressStatus
{
    Unlearned = 0,
    Learning = 1,
    Mastered = 2
}

// Entity đại diện cho bảng UserProgresses — tiến trình học của người dùng
// Ghi nhận trạng thái đã biết/chưa biết của mỗi thẻ
// Unique constraint: mỗi người dùng chỉ có 1 tiến trình cho mỗi thẻ
public class UserProgress
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến người học (bảng AspNetUsers)
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Khóa ngoại đến thẻ được học
    [Required]
    public int FlashcardId { get; set; }

    // true = đã biết, false = chưa biết
    public bool IsLearned { get; set; }

    public UserProgressStatus Status { get; set; } = UserProgressStatus.Unlearned;

    public int CorrectCount { get; set; }

    public int WrongCount { get; set; }

    // Thời gian học gần nhất
    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;

    // Navigation property — liên kết đến người học
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    // Navigation property — liên kết đến thẻ
    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
