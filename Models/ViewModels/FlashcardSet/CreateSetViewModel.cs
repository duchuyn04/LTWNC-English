using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Form tạo bộ thẻ (GET/POST /Set/Create)
public class CreateSetViewModel
{
    // Tiêu đề bộ
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    // Mô tả optional
    public string? Description { get; set; }

    // true = public trên trang chủ / chi tiết ẩn danh
    public bool IsPublic { get; set; } = true;
}
