using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class CreateSetRequest
{
    [Required(ErrorMessage = "Tên bộ từ không được để trống.")]
    [StringLength(200, ErrorMessage = "Tên bộ từ tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }
}

public class UpdateSetRequest
{
    [Required(ErrorMessage = "Tên bộ từ không được để trống.")]
    [StringLength(200, ErrorMessage = "Tên bộ từ tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }
}

// DTO trả về cho API bộ thẻ — không trả entity EF ra ngoài.
public class SetResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCardRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "SetId không hợp lệ.")]
    public int SetId { get; set; }

    [Required(ErrorMessage = "Thuật ngữ không được để trống.")]
    public string FrontText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Định nghĩa không được để trống.")]
    public string BackText { get; set; } = string.Empty;

    public string? Pronunciation { get; set; }

    [StringLength(80, ErrorMessage = "Loại từ tối đa 80 ký tự.")]
    public string? PartOfSpeech { get; set; }

    public string? ExampleSentence { get; set; }

    public string? ExampleMeaning { get; set; }

    public string? Synonyms { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsStarred { get; set; }
}

public class UpdateCardRequest : CreateCardRequest
{
    public int Id { get; set; }

    // true = xóa ảnh đã upload khỏi thẻ
    public bool RemoveUploadedImage { get; set; }
}

public class CardResponse
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public string? UploadedImagePath { get; set; }
    public bool IsStarred { get; set; }
    public int OrderIndex { get; set; }
}

// Một thẻ trong payload import hàng loạt (SetId nằm trên BatchImportRequest).
public class BatchImportCardItem
{
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsStarred { get; set; }
}

public class BatchImportRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "SetId không hợp lệ.")]
    public int SetId { get; set; }

    [Required, MaxLength(5000)]
    public List<CreateCardRequest> Cards { get; set; } = new();

    public bool ReplaceAll { get; set; }
}

public class ReorderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "SetId không hợp lệ.")]
    public int SetId { get; set; }

    [Required]
    public int[] OrderedCardIds { get; set; } = Array.Empty<int>();
}
