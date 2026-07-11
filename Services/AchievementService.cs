using ltwnc.Data;
using ltwnc.Models.ViewModels.Achievements;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// Kết quả trang Thành tích: danh sách huy hiệu + tiêu đề vừa mở khi rescan
public sealed class AchievementPageModel
{
    public IReadOnlyList<AchievementListItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<string> NewlyUnlockedTitles { get; init; } = [];
}

// ============================================================
// Service đọc thành tích để hiện trên trang web.
// Khi mở trang: rescan mở khóa các huy hiệu đủ điều kiện (nếu thiếu),
// rồi ghép catalog + tiến độ metric + trạng thái đã mở cho UI.
// ============================================================
public class AchievementService
{
    private readonly AppDbContext _context;
    private readonly AchievementUnlockService _unlock;
    private readonly AchievementProgressService _progress;

    public AchievementService(
        AppDbContext context,
        AchievementUnlockService unlock,
        AchievementProgressService progress)
    {
        _context = context;
        _unlock = unlock;
        _progress = progress;
    }

    // Rescan + snapshot + map toàn bộ danh mục thành view model trang Thành tích
    public async Task<AchievementPageModel> GetPageAsync(
        string userId,
        CancellationToken ct = default)
    {
        // 1. Đồng bộ mở khóa các huy hiệu đủ mốc nhưng chưa có dòng trong DB
        var newly = await _unlock.SyncEligibleAsync(userId, ct);

        // 2. Ảnh chụp metric hiện tại để vẽ progress bar
        var snapshot = await _progress.GetSnapshotAsync(userId, ct);

        // 3. Các huy hiệu user đã có (sau rescan)
        var unlocked = await _context.UserAchievements.AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToDictionaryAsync(a => a.Code, a => a.UnlockedAt, ct);

        // 4. Ghép catalog → view model (tiến độ, CTA, trạng thái mở)
        var items = AchievementCatalog.All.Select(def =>
            {
                var value = snapshot.GetValue(def.Metric);
                var current = Math.Min(value, def.Target);
                var isUnlocked = unlocked.ContainsKey(def.Code);
                return new AchievementListItemViewModel
                {
                    Code = def.Code,
                    Title = def.Title,
                    Description = def.Description,
                    IsUnlocked = isUnlocked,
                    UnlockedAt = isUnlocked ? unlocked[def.Code] : null,
                    // Đã mở → hiện full target; chưa mở → current capped
                    Current = isUnlocked ? def.Target : current,
                    Target = def.Target,
                    ProgressPercent = def.Target <= 0
                        ? 0
                        : (isUnlocked ? 100 : current * 100 / def.Target),
                    CtaText = def.CtaText,
                    CtaUrl = def.CtaPath
                };
            })
            .OrderByDescending(i => i.IsUnlocked)
            .ThenByDescending(i => i.ProgressPercent)
            .ThenBy(i => i.Title)
            .ToList();

        return new AchievementPageModel
        {
            Items = items,
            NewlyUnlockedTitles = newly.Select(d => d.Title).ToList()
        };
    }
}
