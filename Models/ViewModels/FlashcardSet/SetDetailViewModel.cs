using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Dữ liệu truyền cho trang chi tiết bộ thẻ (xem bộ thẻ công khai hoặc của mình)
// Dữ liệu truyền cho trang chi tiết bộ thẻ (xem / sao chép)
public class SetDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<Flashcard> Flashcards { get; set; } = new();
    public bool IsOwner { get; set; }
    public int? ExistingCopyId { get; set; }
}
