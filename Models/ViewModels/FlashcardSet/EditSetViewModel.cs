using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Form meta bộ thẻ khi Edit (thẻ nằm ViewBag.Cards, không trong VM này)
public class EditSetViewModel
{
    // Id set đang sửa
    public int Id { get; set; }

    // Tiêu đề
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    // Mô tả optional
    public string? Description { get; set; }

    // Public / private
    public bool IsPublic { get; set; } = true;
}
