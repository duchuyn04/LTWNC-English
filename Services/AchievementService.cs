using ltwnc.Data;
using ltwnc.Models.ViewModels.Achievements;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// ============================================================
// Service đọc thành tích để hiện trên trang web.
// Đây KHÔNG phải Observer — chỉ là "thư viện đọc dữ liệu" cho UI.
// Việc MỞ khóa huy hiệu do AchievementStudyObserver lo khi có sự kiện học.
// ============================================================
public class AchievementService
{
    private readonly AppDbContext _context;

    public AchievementService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy toàn bộ huy hiệu trong danh mục, đánh dấu cái nào user đã mở
    public async Task<IReadOnlyList<AchievementListItemViewModel>> GetCatalogWithStatusAsync(string userId)
    {
        // Các mã huy hiệu user này đã nhận
        var unlocked = await _context.UserAchievements
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToDictionaryAsync(a => a.Code, a => a.UnlockedAt);

        // Ghép danh mục cố định với trạng thái đã mở / chưa mở
        return AchievementCatalog.All
            .Select(def => new AchievementListItemViewModel
            {
                Code = def.Code,
                Title = def.Title,
                Description = def.Description,
                IsUnlocked = unlocked.ContainsKey(def.Code),
                UnlockedAt = unlocked.TryGetValue(def.Code, out var at) ? at : null
            })
            .OrderByDescending(item => item.IsUnlocked)
            .ThenBy(item => item.Title)
            .ToList();
    }
}
