namespace ltwnc.Services.StudyEvents;

// ============================================================
// Vai trò OBSERVER (người theo dõi) trong mẫu thiết kế Observer.
//
// Giống như người đăng ký nhận thông báo: khi có chuyện xảy ra
// (user học xong một thẻ, xong một buổi...), hệ thống gọi method này.
// Mỗi observer chỉ lo việc của mình (mở thành tích, ghi log...).
// Họ không cần biết ai đã phát tin, cũng không cần biết các observer khác.
// ============================================================
public interface IStudyEventObserver
{
    // Nhận một mẩu tin sự kiện học và xử lý (nếu quan tâm).
    // cancellationToken: cho phép hủy giữa chừng nếu request bị dừng.
    Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
