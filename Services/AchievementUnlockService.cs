using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// So metric hiện tại với catalog, chèn UserAchievement còn thiếu.
// Observer gọi khi có sự kiện học; trang Thành tích cũng gọi để rescan.
public class AchievementUnlockService
{
    // Ghi / đọc bảng UserAchievements
    private readonly AppDbContext _context;

    // Lấy snapshot metric để so với Target từng huy hiệu
    private readonly AchievementProgressService _progress;

    // Inject DbContext và service đếm metric
    public AchievementUnlockService(AppDbContext context, AchievementProgressService progress)
    {
        _context = context;
        _progress = progress;
    }

    // Duyệt catalog: đủ Target và chưa có code thì ghi bản ghi mới; trả về list vừa mở lần này
    public async Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Metric hiện tại (đếm một lần)
        AchievementProgressSnapshot snapshot =
            await _progress.GetSnapshotAsync(userId, cancellationToken);

        // Code đã mở, tránh chèn trùng
        List<string> existingCodes = await _context.UserAchievements
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => achievement.Code)
            .ToListAsync(cancellationToken);

        HashSet<string> unlockedCodes = existingCodes.ToHashSet();

        // Định nghĩa vừa mở trong lần gọi này (để UI hiện banner)
        List<AchievementCatalog.Definition> newlyUnlocked = new();

        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            if (unlockedCodes.Contains(definition.Code))
            {
                continue;
            }

            int metricValue = snapshot.GetValue(definition.Metric);
            if (metricValue < definition.Target)
            {
                continue;
            }

            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                Code = definition.Code,
                Title = definition.Title,
                Description = definition.Description,
                UnlockedAt = DateTime.UtcNow
            });

            newlyUnlocked.Add(definition);
            unlockedCodes.Add(definition.Code);
        }

        // Có huy hiệu mới mới Save; unique (UserId, Code) có thể va chạm khi 2 request song song
        if (newlyUnlocked.Count > 0)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Request khác đã mở cùng code: coi như đã xong
            }
        }

        return newlyUnlocked;
    }
}
