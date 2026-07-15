using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Phần tiến độ mà View học cần; loại bỏ Id/UserId/navigation của entity UserProgress.
public class FlashcardProgressViewModel
{
    public UserProgressStatus Status { get; set; }

    public int CorrectCount { get; set; }

    public int WrongCount { get; set; }

    public static FlashcardProgressViewModel FromEntity(UserProgress progress)
    {
        return new FlashcardProgressViewModel
        {
            Status = progress.Status,
            CorrectCount = progress.CorrectCount,
            WrongCount = progress.WrongCount
        };
    }
}
