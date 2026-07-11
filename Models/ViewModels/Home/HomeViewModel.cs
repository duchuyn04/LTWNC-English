namespace ltwnc.Models.ViewModels.Home;

// Dữ liệu truyền cho trang chủ: danh sách bộ thẻ public và từ khóa tìm kiếm
public class HomeViewModel
{
    public List<ltwnc.Models.Entities.FlashcardSet> PublicSets { get; set; } = new();
    public string? SearchQuery { get; set; }
}
