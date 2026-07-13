using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

// Bản chụp UserProgress trước khi xóa thẻ, dùng khi Undo DeleteCards.
public class UserProgressSnapshot
{
    // Id gốc của dòng progress
    public int Id { get; set; }

    // User sở hữu tiến độ
    public string UserId { get; set; } = string.Empty;

    // Thẻ gắn progress
    public int FlashcardId { get; set; }

    // Đã thuộc hay chưa
    public bool IsLearned { get; set; }

    // Trạng thái chi tiết (Learning / Mastered...)
    public UserProgressStatus Status { get; set; }

    // Số lần trả lời đúng
    public int CorrectCount { get; set; }

    // Số lần trả lời sai
    public int WrongCount { get; set; }

    // Lần ôn gần nhất (UTC)
    public DateTime LastReviewed { get; set; }
}
