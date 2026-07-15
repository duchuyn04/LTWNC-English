using ltwnc.Services;

namespace ltwnc.Services.StudyEvents;

// Observer thành tích: mỗi sự kiện học thì quét lại metric và mở huy hiệu đủ điều kiện.
// StudyService / DictationService không gọi unlock trực tiếp.
public class AchievementStudyObserver : IStudyEventObserver
{
    // Service chèn UserAchievement còn thiếu
    private readonly IAchievementUnlockService _unlockService;

    // Inject unlock service
    public AchievementStudyObserver(IAchievementUnlockService unlockService)
    {
        _unlockService = unlockService;
    }

    // User rỗng thì bỏ; còn lại SyncEligibleAsync theo UserId trên sự kiện
    public async Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyEvent.UserId))
        {
            return;
        }

        await _unlockService.SyncEligibleAsync(studyEvent.UserId, cancellationToken);
    }
}
