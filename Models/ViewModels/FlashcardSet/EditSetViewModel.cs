using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Dữ liệu form chỉnh sửa thông tin bộ thẻ
// Dữ liệu form chỉnh sửa thông tin bộ thẻ
public class EditSetViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;
}
