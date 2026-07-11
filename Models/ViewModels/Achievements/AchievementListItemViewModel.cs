namespace ltwnc.Models.ViewModels.Achievements;

// Dữ liệu một huy hiệu để hiển thị trên trang Thành tích
public class AchievementListItemViewModel
{
    // Mã kỹ thuật (không hiện cho user, dùng nội bộ)
    public string Code { get; set; } = string.Empty;

    // Tên huy hiệu đẹp
    public string Title { get; set; } = string.Empty;

    // Giải thích vì sao được / làm sao để được
    public string Description { get; set; } = string.Empty;

    // true = user đã mở khóa; false = vẫn đang khóa
    public bool IsUnlocked { get; set; }

    // Lúc mở khóa (null nếu chưa mở)
    public DateTime? UnlockedAt { get; set; }
}
