using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Achievements;

// So metric hiện tại với catalog, chèn UserAchievement còn thiếu.
// Observer gọi khi có sự kiện học; trang Thành tích cũng gọi để rescan.
public class AchievementUnlockService : IAchievementUnlockService
{
    // Ghi / đọc bảng UserAchievements
    private readonly AppDbContext _context;

    // Lấy snapshot metric để so với Target từng huy hiệu
    private readonly IAchievementProgressService _progress;

    // Inject DbContext và service đếm metric
    public AchievementUnlockService(AppDbContext context, IAchievementProgressService progress)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_progress` để các phương thức khác sử dụng.
        _progress = progress;
    }

    // Duyệt catalog: đủ Target và chưa có code thì ghi bản ghi mới; trả về list vừa mở lần này
    public async Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Metric hiện tại (đếm một lần)
        // 1. Gọi `GetSnapshotAsync` và lưu kết quả vào `snapshot`.
        AchievementProgressSnapshot snapshot =
            await _progress.GetSnapshotAsync(userId, cancellationToken);

        // Code đã mở, tránh chèn trùng
        // 2. Gọi `ToListAsync` và lưu kết quả vào `existingCodes`.
        List<string> existingCodes = await _context.UserAchievements
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => achievement.Code)
            .ToListAsync(cancellationToken);

        // 3. Gọi `ToHashSet` và lưu kết quả vào `unlockedCodes`.
        HashSet<string> unlockedCodes = existingCodes.ToHashSet();

        // Định nghĩa vừa mở trong lần gọi này (để UI hiện banner)
        // 4. Khởi tạo `newlyUnlocked` với dữ liệu ban đầu cần thiết.
        List<AchievementCatalog.Definition> newlyUnlocked = new();

        // 5. Duyệt từng `definition` trong `AchievementCatalog.All` để xử lý lần lượt.
        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            // 6. Kiểm tra `unlockedCodes.Contains(definition.Code)` để chọn nhánh xử lý phù hợp.
            if (unlockedCodes.Contains(definition.Code))
            {
                // 7. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 8. Gọi `GetValue` và lưu kết quả vào `metricValue`.
            int metricValue = snapshot.GetValue(definition.Metric);
            // 9. Kiểm tra `metricValue < definition.Target` để chọn nhánh xử lý phù hợp.
            if (metricValue < definition.Target)
            {
                // 10. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 11. Gọi `Add` để thực hiện bước nghiệp vụ này.
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                Code = definition.Code,
                Title = definition.Title,
                Description = definition.Description,
                UnlockedAt = DateTime.UtcNow
            });

            // 12. Gọi `Add` để thực hiện bước nghiệp vụ này.
            newlyUnlocked.Add(definition);
            // 13. Gọi `Add` để thực hiện bước nghiệp vụ này.
            unlockedCodes.Add(definition.Code);
        }

        // Có huy hiệu mới mới Save; unique (UserId, Code) có thể va chạm khi 2 request song song
        // 14. Kiểm tra `newlyUnlocked.Count > 0` để chọn nhánh xử lý phù hợp.
        if (newlyUnlocked.Count > 0)
        {
            // 15. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
            try
            {
                // 16. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // 17. Gọi `ToArray` và lưu kết quả vào `attemptedCodes`.
                string[] attemptedCodes = newlyUnlocked.Select(definition => definition.Code).ToArray();
                // 18. Gọi `Clear` để thực hiện bước nghiệp vụ này.
                _context.ChangeTracker.Clear();
                // 19. Gọi `ToListAsync` và lưu kết quả vào `persistedCodes`.
                List<string> persistedCodes = await _context.UserAchievements
                    .Where(achievement => achievement.UserId == userId
                        && attemptedCodes.Contains(achievement.Code))
                    .Select(achievement => achievement.Code)
                    .ToListAsync(cancellationToken);
                // 20. Kiểm tra `attemptedCodes.All(code => persistedCodes.Contains(code, StringComp...` để chọn nhánh xử lý phù hợp.
                if (attemptedCodes.All(code => persistedCodes.Contains(code, StringComparer.Ordinal)))
                {
                    // 21. Trả `[]` cho nơi gọi.
                    return [];
                }

                // 22. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
                throw;
            }
        }

        // 23. Trả `newlyUnlocked` cho nơi gọi.
        return newlyUnlocked;
    }
}
