namespace ltwnc.Models.ViewModels.Achievements;

// Một huy hiệu trên trang /Achievements (progress bar + CTA)
public class AchievementListItemViewModel
{
    // Mã catalog (nội bộ, sort/key)
    public string Code { get; set; } = string.Empty;

    // Tên hiện UI
    public string Title { get; set; } = string.Empty;

    // Mô tả điều kiện / ý nghĩa
    public string Description { get; set; } = string.Empty;

    // true = đã mở khóa
    public bool IsUnlocked { get; set; }

    // Lúc mở (UTC); null nếu chưa mở
    public DateTime? UnlockedAt { get; set; }

    // Tiến độ hiện tại (đã mở thì = Target)
    public int Current { get; set; }

    // Mốc Target từ catalog
    public int Target { get; set; }

    // 0-100; đã mở = 100
    public int ProgressPercent { get; set; }

    // Nhãn nút CTA khi chưa mở
    public string CtaText { get; set; } = string.Empty;

    // URL CTA (thường /Set)
    public string CtaUrl { get; set; } = string.Empty;
}
