using ltwnc.Models.ViewModels.Flashcards;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Toàn bộ dữ liệu trang sửa bộ thẻ: form metadata và danh sách thẻ strongly typed.
public class EditSetPageViewModel : EditSetViewModel
{
    public IReadOnlyList<FlashcardViewModel> Cards { get; set; } = Array.Empty<FlashcardViewModel>();
}
