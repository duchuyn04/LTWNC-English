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

    // Optional fields for quick entry
    public string? Pronunciation { get; set; }

    [StringLength(80, ErrorMessage = "Loại từ tối đa 80 ký tự.")]
    public string? PartOfSpeech { get; set; }

    public string? ExampleSentence { get; set; }

    public string? ExampleMeaning { get; set; }

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
