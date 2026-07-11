namespace ltwnc.Services.StudyEvents;

// ============================================================
// Vai trò SUBJECT (người phát tin / "trạm phát") trong mẫu Observer.
//
// Service học tập chỉ cần gọi PublishAsync sau khi lưu dữ liệu.
// Trạm phát sẽ lần lượt báo cho tất cả observer đã đăng ký.
// Nhờ vậy, thêm "người theo dõi" mới không phải sửa StudyService.
// ============================================================
public interface IStudyEventPublisher
{
    // Phát một sự kiện học tới mọi observer đang lắng nghe.
    Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
