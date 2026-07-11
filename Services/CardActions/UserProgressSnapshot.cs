using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

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
