using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public sealed class ContentReportInputModel
{
    [Required(ErrorMessage = "Vui lòng chọn lý do báo cáo.")]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    public string? Description { get; set; }
}
