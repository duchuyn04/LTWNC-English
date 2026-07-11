using ltwnc.Services;

namespace ltwnc.Services.StudyEvents;

// ============================================================
// OBSERVER cụ thể #1: "người theo dõi thành tích".
//
// Vai trò trong mẫu Observer:
// - Không phát tin, chỉ LẮNG NGHE tin từ StudyEventPublisher.
// - Mỗi khi có sự kiện học, nhờ AchievementUnlockService quét lại
//   metric và mở các huy hiệu đủ điều kiện (nếu chưa có).
//
// StudyService không biết class này tồn tại — chỉ cần Publish sự kiện.
// Logic mốc / tier nằm ở catalog + unlock service, không nằm ở đây.
// ============================================================
public class AchievementStudyObserver : IStudyEventObserver
{
    private readonly AchievementUnlockService _unlockService;

    public AchievementStudyObserver(AchievementUnlockService unlockService)
    {
        _unlockService = unlockService;
    }

    // Cửa vào duy nhất: nhận mẩu tin → đồng bộ huy hiệu cho user đó
    public async Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        // Không có user → không mở huy hiệu
        if (string.IsNullOrWhiteSpace(studyEvent.UserId))
            return;

        // Mọi sự kiện học đều quét lại huy hiệu đủ điều kiện cho user đó
        await _unlockService.SyncEligibleAsync(studyEvent.UserId, cancellationToken);
    }
}
