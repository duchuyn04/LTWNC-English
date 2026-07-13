using ltwnc.Models.Entities;

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

    // Owner AspNetUsers.Id
    public string UserId { get; set; } = string.Empty;

    // Danh sách thẻ (preview)
    public List<Flashcard> Flashcards { get; set; } = new();

    // Viewer có phải owner không (hiện Edit/Delete)
    public bool IsOwner { get; set; }

    // Nếu viewer đã copy set public này: id bản sao; null = chưa copy
    public int? ExistingCopyId { get; set; }
}
