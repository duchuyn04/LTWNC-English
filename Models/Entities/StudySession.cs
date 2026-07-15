using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

// Bảng StudySessions: một buổi học đã hoàn thành.
public class StudySession
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Id user trong bảng Users (cookie auth)
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Khóa ngoại đến bộ thẻ đã học
    [Required]
    public int FlashcardSetId { get; set; }

    // Mode buổi học
    public StudyMode Mode { get; set; } = StudyMode.Flashcard;

    // Chỉ meaningful khi Mode = Dictation
    public DictationContentMode DictationContentMode { get; set; } = DictationContentMode.Vocabulary;

    // Điểm: Dictation/Quiz...; Flashcard thường null
    public int? Score { get; set; }

    // Thời gian hoàn thành phiên học
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Bộ thẻ của buổi học
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
