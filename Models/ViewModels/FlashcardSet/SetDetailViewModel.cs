using ltwnc.Models.ViewModels.Flashcards;

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

    // Danh sách thẻ (preview)
    public IReadOnlyList<FlashcardViewModel> Flashcards { get; set; } = Array.Empty<FlashcardViewModel>();

    // Viewer có phải owner không (hiện Edit/Delete)
    public bool IsOwner { get; set; }

    // Nếu viewer đã copy set public này: id bản sao; null = chưa copy
    public int? ExistingCopyId { get; set; }
}
