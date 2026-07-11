using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// ============================================================
// Service đồng bộ mở khóa huy hiệu theo metric hiện tại của user.
// Dùng chung cho Observer (khi có sự kiện học) và rescan trang Thành tích.
// Chỉ chèn UserAchievement còn thiếu; trả về danh sách vừa mới mở trong lần gọi này.
// ============================================================
public class AchievementUnlockService
{
    private readonly AppDbContext _context;
    private readonly AchievementProgressService _progress;

    public AchievementUnlockService(AppDbContext context, AchievementProgressService progress)
    {
        _context = context;
        _progress = progress;
    }

    // Quét catalog: metric đủ Target và chưa có code → ghi UserAchievement
    public async Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Ảnh chụp metric hiện tại (đếm một lần)
        var snapshot = await _progress.GetSnapshotAsync(userId, cancellationToken);

        // 2. Các mã huy hiệu user đã có — tránh mở trùng
        var already = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);
        var have = already.ToHashSet();

        // 3. Duyệt toàn bộ danh mục, chèn những cái đủ điều kiện
        var newly = new List<AchievementCatalog.Definition>();

        foreach (var def in AchievementCatalog.All)
        {
            // Đã có mã này rồi → bỏ qua
            if (have.Contains(def.Code))
                continue;

            // Metric chưa đạt Target → bỏ qua
            var value = snapshot.GetValue(def.Metric);
            if (value < def.Target)
                continue;

            // Đủ điều kiện → ghi bản ghi thành tích mới
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                Code = def.Code,
                Title = def.Title,
                Description = def.Description,
                UnlockedAt = DateTime.UtcNow
            });
            newly.Add(def);
            have.Add(def.Code);
        }

        // 4. Lưu nếu có huy hiệu mới; race unique index thì coi như đã mở rồi
        if (newly.Count > 0)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Hai request đồng thời cùng mở → unique (UserId, Code) có thể va chạm
            }
        }

        // 5. Trả về định nghĩa vừa mở trong lần gọi này
        return newly;
    }
}
