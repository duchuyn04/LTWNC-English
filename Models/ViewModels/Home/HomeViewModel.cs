namespace ltwnc.Models.ViewModels.Home;

// Trang chủ khách: list public sets + ô tìm kiếm
public class HomeViewModel
{
    // Bộ thẻ public (mới nhất hoặc kết quả search)
    public List<ltwnc.Models.Entities.FlashcardSet> PublicSets { get; set; } = new();

    // Từ khóa q trên URL; null nếu chưa search
    public string? SearchQuery { get; set; }
}
