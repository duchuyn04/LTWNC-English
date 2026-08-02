namespace ltwnc.Models.ViewModels.Home;

// Trang chủ khách: list public sets + ô tìm kiếm
public class HomeViewModel
{
    // Bộ thẻ public (mới nhất hoặc kết quả search)
    public IReadOnlyList<PublicSetViewModel> PublicSets { get; set; } = Array.Empty<PublicSetViewModel>();

    // Từ khóa q trên URL; null nếu chưa search
    public string? SearchQuery { get; set; }

    public IReadOnlyList<HomeCreditPackageViewModel> CreditPackages { get; set; } = [];
}

public sealed class HomeCreditPackageViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long PriceVnd { get; set; }
    public int Credits { get; set; }
}

public class PublicSetViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
}
