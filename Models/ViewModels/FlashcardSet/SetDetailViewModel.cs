using ltwnc.Models.ViewModels.Flashcards;
using ltwnc.Services.ContentReports;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Trang chi tiết /Set/{id}: xem, copy, vào học
public class SetDetailViewModel
{
    // Id set
    public int Id { get; set; }

    // Tiêu đề
    public string Title { get; set; } = string.Empty;

    // Mô tả
    public string? Description { get; set; }

    // Public hay private
    public bool IsPublic { get; set; }

    // Bộ đang bị Admin cách ly khỏi mọi luồng công khai hay không.
    public bool IsQuarantined { get; set; }

    // Lý do công khai do Admin nhập, chỉ hiển thị cho tác giả khi bộ bị cách ly.
    public string? ModerationPublicReason { get; set; }

    // Thời điểm Admin xử lý gần nhất, chỉ dùng để tác giả biết mốc thay đổi.
    public DateTime? ModeratedAtUtc { get; set; }

    // Danh sách thẻ (preview)
    public IReadOnlyList<FlashcardViewModel> Flashcards { get; set; } = Array.Empty<FlashcardViewModel>();

    // Viewer có phải owner không (hiện Edit/Delete)
    public bool IsOwner { get; set; }

    // Nếu viewer đã copy set public này: id bản sao; null = chưa copy
    public int? ExistingCopyId { get; set; }

    // Danh mục lý do báo cáo cố định cho người học.
    public IReadOnlyList<ContentReportReasonOption> ReportReasonOptions { get; set; } =
        Array.Empty<ContentReportReasonOption>();

    // Bật form báo cáo khi viewer đã đăng nhập, không phải chủ sở hữu và chưa có báo cáo đang mở.
    public bool CanReport { get; set; }

    // Nhắc người học rằng báo cáo đang chờ nên không thể gửi trùng.
    public bool HasOpenReport { get; set; }
}
