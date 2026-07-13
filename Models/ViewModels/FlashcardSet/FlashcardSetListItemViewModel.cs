namespace ltwnc.Models.ViewModels.FlashcardSet;

// Một dòng thư viện /Set: entity set + thống kê học
public class FlashcardSetListItemViewModel
{
    // Bộ thẻ (title, updated...)
    public ltwnc.Models.Entities.FlashcardSet Set { get; set; } = null!;

    // Số thẻ trong set
    public int TotalCards { get; set; }

    // Số thẻ user đã thuộc
    public int LearnedCount { get; set; }

    // learned * 100 / total (0 nếu rỗng)
    public int MasteryPercent { get; set; }
}
