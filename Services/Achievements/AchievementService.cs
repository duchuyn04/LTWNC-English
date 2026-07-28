using ltwnc.Data;
using ltwnc.Models.ViewModels.Achievements;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Achievements;

// Dữ liệu trang Thành tích: list huy hiệu + tiêu đề vừa mở khi rescan
public sealed class AchievementPageModel
{
    // Từng huy hiệu kèm tiến độ / trạng thái mở
    public IReadOnlyList<AchievementListItemViewModel> Items { get; init; } =
        Array.Empty<AchievementListItemViewModel>();

    // Title các huy hiệu vừa mở trong lần rescan này (hiện banner TempData)
    public IReadOnlyList<string> NewlyUnlockedTitles { get; init; } =
        Array.Empty<string>();
}

// Đọc trang Thành tích: rescan unlock, ghép catalog + metric + DB đã mở.
public class AchievementService : IAchievementService
{
    // Đọc UserAchievements đã lưu
    private readonly AppDbContext _context;

    // Rescan mở huy hiệu đủ điều kiện còn thiếu
    private readonly IAchievementUnlockService _unlock;

    // Lấy metric cho progress bar
    private readonly IAchievementProgressService _progress;

    // Inject unlock, progress và DbContext
    public AchievementService(
        AppDbContext context,
        IAchievementUnlockService unlock,
        IAchievementProgressService progress)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_unlock` để các phương thức khác sử dụng.
        _unlock = unlock;
        // 3. Lưu dependency `_progress` để các phương thức khác sử dụng.
        _progress = progress;
    }

    // Rescan + snapshot + map catalog thành view model đã sắp xếp
    public async Task<AchievementPageModel> GetPageAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `SyncEligibleAsync` và lưu kết quả vào `newlyUnlockedDefinitions`.
        IReadOnlyList<AchievementCatalog.Definition> newlyUnlockedDefinitions =
            await _unlock.SyncEligibleAsync(userId, cancellationToken);

        // 2. Gọi `GetSnapshotAsync` và lưu kết quả vào `snapshot`.
        AchievementProgressSnapshot snapshot =
            await _progress.GetSnapshotAsync(userId, cancellationToken);

        // 3. Gọi `ToDictionaryAsync` và lưu kết quả vào `unlockedByCode`.
        Dictionary<string, DateTime> unlockedByCode = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .ToDictionaryAsync(
                achievement => achievement.Code,
                achievement => achievement.UnlockedAt,
                cancellationToken);

        // 4. Khởi tạo `items` với dữ liệu ban đầu cần thiết.
        List<AchievementListItemViewModel> items = new();

        // 5. Duyệt từng `definition` trong `AchievementCatalog.All` để xử lý lần lượt.
        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            // 6. Gọi `GetValue` và lưu kết quả vào `metricValue`.
            int metricValue = snapshot.GetValue(definition.Metric);
            // 7. Gọi `Min` và lưu kết quả vào `cappedCurrent`.
            int cappedCurrent = Math.Min(metricValue, definition.Target);
            // 8. Gọi `ContainsKey` và lưu kết quả vào `isUnlocked`.
            bool isUnlocked = unlockedByCode.ContainsKey(definition.Code);

            // 9. Tính giá trị và lưu vào `unlockedAt` để dùng ở bước tiếp theo.
            DateTime? unlockedAt = null;
            // 10. Kiểm tra `isUnlocked` để chọn nhánh xử lý phù hợp.
            if (isUnlocked)
            {
                // 11. Cập nhật `unlockedAt` bằng giá trị mới.
                unlockedAt = unlockedByCode[definition.Code];
            }

            // Đã mở: current = target, 100%. Chưa mở: current capped, % theo target
            // 12. Khai báo `displayCurrent` để lưu dữ liệu dùng ở các bước sau.
            int displayCurrent;
            // 13. Khai báo `progressPercent` để lưu dữ liệu dùng ở các bước sau.
            int progressPercent;
            // 14. Kiểm tra `isUnlocked` để chọn nhánh xử lý phù hợp.
            if (isUnlocked)
            {
                // 15. Cập nhật `displayCurrent` bằng giá trị mới.
                displayCurrent = definition.Target;
                // 16. Cập nhật `progressPercent` bằng giá trị mới.
                progressPercent = 100;
            }
            else if (definition.Target <= 0)
            {
                // 17. Cập nhật `displayCurrent` bằng giá trị mới.
                displayCurrent = cappedCurrent;
                // 18. Cập nhật `progressPercent` bằng giá trị mới.
                progressPercent = 0;
            }
            else
            {
                // 19. Cập nhật `displayCurrent` bằng giá trị mới.
                displayCurrent = cappedCurrent;
                // 20. Cập nhật `progressPercent` bằng giá trị mới.
                progressPercent = cappedCurrent * 100 / definition.Target;
            }

            // 21. Gọi `Add` để thực hiện bước nghiệp vụ này.
            items.Add(new AchievementListItemViewModel
            {
                Code = definition.Code,
                Title = definition.Title,
                Description = definition.Description,
                IsUnlocked = isUnlocked,
                UnlockedAt = unlockedAt,
                Current = displayCurrent,
                Target = definition.Target,
                ProgressPercent = progressPercent,
                CtaText = definition.CtaText,
                CtaUrl = definition.CtaPath
            });
        }

        // Ưu tiên đã mở, rồi % cao, rồi title
        // 22. Cập nhật `items` bằng giá trị mới.
        items = items
            .OrderByDescending(item => item.IsUnlocked)
            .ThenByDescending(item => item.ProgressPercent)
            .ThenBy(item => item.Title)
            .ToList();

        // 23. Gọi `ToList` và lưu kết quả vào `newlyUnlockedTitles`.
        List<string> newlyUnlockedTitles = newlyUnlockedDefinitions
            .Select(definition => definition.Title)
            .ToList();

        // 24. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AchievementPageModel
        {
            Items = items,
            NewlyUnlockedTitles = newlyUnlockedTitles
        };
    }
}
