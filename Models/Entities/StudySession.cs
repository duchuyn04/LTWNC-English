using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

// Enum định nghĩa các chế độ học
public enum StudyMode
{
    Flashcard, // Lật thẻ
    Quiz,      // Trắc nghiệm
    Write,     // Viết chính tả
    Match,     // Ghép đôi
    Dictation  // Nghe chép chính tả
}

// Entity đại diện cho bảng StudySessions — phiên học
// Ghi nhận mỗi lần người dùng hoàn thành một buổi học
public class StudySession
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến người học (bảng AspNetUsers)
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Khóa ngoại đến bộ thẻ đã học
    [Required]
    public int FlashcardSetId { get; set; }

    // Chế độ học (Flashcard, Quiz, Write, Match)
    public StudyMode Mode { get; set; } = StudyMode.Flashcard;

    // Điểm số — chỉ có giá trị với Quiz, Write, Match (null với Flashcard)
    public int? Score { get; set; }

    // Thời gian hoàn thành phiên học
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — liên kết đến người học
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    // Navigation property — liên kết đến bộ thẻ đã học
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
