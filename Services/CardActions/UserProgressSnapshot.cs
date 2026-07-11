using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

// Snapshot lưu tiến trình học của một thẻ trước khi bị xóa
public class UserProgressSnapshot
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int FlashcardId { get; set; }
    public bool IsLearned { get; set; }
    public UserProgressStatus Status { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public DateTime LastReviewed { get; set; }
}
