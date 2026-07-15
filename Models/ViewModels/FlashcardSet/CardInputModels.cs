using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Models.ViewModels.FlashcardSet;

// Các field chung của form thêm/sửa thẻ.
public abstract class CardInputModel
{
    [Required(ErrorMessage = "Thuật ngữ không được để trống.")]
    public string FrontText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Định nghĩa không được để trống.")]
    public string BackText { get; set; } = string.Empty;

    [Required(ErrorMessage = "IPA không được để trống.")]
    public string Pronunciation { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loại từ không được để trống.")]
    [StringLength(80, ErrorMessage = "Loại từ tối đa 80 ký tự.")]
    public string PartOfSpeech { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ví dụ tiếng Anh không được để trống.")]
    public string ExampleSentence { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nghĩa câu ví dụ tiếng Việt không được để trống.")]
    public string ExampleMeaning { get; set; } = string.Empty;

    public string? Synonyms { get; set; }

    public string? ImageUrl { get; set; }

    public IFormFile? ImageFile { get; set; }

    public bool IsStarred { get; set; }
}

public class AddCardInputModel : CardInputModel
{
}

public class EditCardInputModel : CardInputModel
{
    public int SetId { get; set; }

    public bool RemoveUploadedImage { get; set; }
}
