using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Dữ liệu form tạo bộ thẻ mới
// Dữ liệu form tạo bộ thẻ mới
public class CreateSetViewModel
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;
}
