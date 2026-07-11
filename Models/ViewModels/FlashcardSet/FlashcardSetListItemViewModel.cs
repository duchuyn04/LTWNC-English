namespace ltwnc.Models.ViewModels.FlashcardSet;

// Dữ liệu một dòng trong danh sách bộ thẻ của ngườidùng, kèm thống kê tiến trình học
// Dữ liệu tổng hợp hiển thị một bộ thẻ trong danh sách thư viện
public class FlashcardSetListItemViewModel
{
    public ltwnc.Models.Entities.FlashcardSet Set { get; set; } = null!;

    public int TotalCards { get; set; }

    public int LearnedCount { get; set; }

    public int MasteryPercent { get; set; }
}
