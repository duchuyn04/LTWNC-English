namespace ltwnc.Models.ViewModels.FlashcardSet;

// Một dòng thư viện /Set: dữ liệu hiển thị bộ thẻ + thống kê học.
public class FlashcardSetListItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    // Số thẻ trong set
    public int TotalCards { get; set; }

    // Số thẻ user đã thuộc
    public int LearnedCount { get; set; }

    // learned * 100 / total (0 nếu rỗng)
    public int MasteryPercent { get; set; }
}
