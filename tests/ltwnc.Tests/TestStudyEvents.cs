using ltwnc.Services.StudyEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace ltwnc.Tests;

// ============================================================
// Hỗ trợ test: tạo "trạm phát" không có người theo dõi nào.
// Dùng khi test StudyService / DictationService mà không cần
// kiểm tra thành tích — chỉ cần constructor đủ tham số.
// ============================================================
public static class TestStudyEvents
{
    // Publisher rỗng: PublishAsync chạy nhưng không gọi observer nào
    public static IStudyEventPublisher NoOpPublisher()
        => new StudyEventPublisher(
            Array.Empty<IStudyEventObserver>(),
            NullLogger<StudyEventPublisher>.Instance);
}
