using ltwnc.Services.StudyEvents;

namespace ltwnc.Services.Achievements;

// Observer thành tích: mỗi sự kiện học thì quét lại metric và mở huy hiệu đủ điều kiện.
// StudyService / DictationService không gọi unlock trực tiếp.
public class AchievementStudyObserver : IStudyEventObserver
{
    // Service chèn UserAchievement còn thiếu
    private readonly IAchievementUnlockService _unlockService;

    // Inject unlock service
    public AchievementStudyObserver(IAchievementUnlockService unlockService)
    {
        // 1. Lưu dependency `_unlockService` để các phương thức khác sử dụng.
        _unlockService = unlockService;
    }

    // User rỗng thì bỏ; còn lại SyncEligibleAsync theo UserId trên sự kiện
    public async Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(studyEvent.UserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(studyEvent.UserId))
        {
            // 2. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 3. Gọi `SyncEligibleAsync` để thực hiện bước nghiệp vụ này.
        await _unlockService.SyncEligibleAsync(studyEvent.UserId, cancellationToken);
    }
}
