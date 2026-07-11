using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ltwnc.Models;

namespace ltwnc.Models.Entities;

// Entity đại diện cho bảng Flashcards — một thẻ flashcard
// Mỗi thẻ có 2 mặt: FrontText (tiếng Anh) và BackText (nghĩa tiếng Việt)
public class Flashcard : IPrototype<Flashcard>
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến bộ thẻ chứa thẻ này
    [Required]
    public int FlashcardSetId { get; set; }

    // Nội dung mặt trước (tiếng Anh) — bắt buộc
    [Required]
    public string FrontText { get; set; } = string.Empty;

    // Nội dung mặt sau (nghĩa tiếng Việt) — bắt buộc
    [Required]
    public string BackText { get; set; } = string.Empty;

    // IPA hoặc phát âm của thuật ngữ — bắt buộc với thẻ mới
    [Required]
    public string Pronunciation { get; set; } = string.Empty;

    // Loại từ: noun, verb, adjective, adverb hoặc giá trị custom
    [Required]
    [MaxLength(80)]
    public string PartOfSpeech { get; set; } = string.Empty;

    // Câu ví dụ tiếng Anh — bắt buộc với thẻ mới
    [Required]
    public string ExampleSentence { get; set; } = string.Empty;

    // Nghĩa tiếng Việt của câu ví dụ — bắt buộc với thẻ mới
    [Required]
    public string ExampleMeaning { get; set; } = string.Empty;

    // Từ đồng nghĩa, phân tách bằng dấu phẩy hoặc chấm phẩy
    public string? Synonyms { get; set; }

    // Ảnh ngoài, không bắt buộc
    public string? ImageUrl { get; set; }

    // Ảnh upload nội bộ, không bắt buộc
    public string? UploadedImagePath { get; set; }

    // Đánh dấu sao để học riêng
    public bool IsStarred { get; set; }

    // Thứ tự hiển thị trong bộ thẻ (0, 1, 2, ...)
    public int OrderIndex { get; set; }

    // Navigation property — liên kết đến bộ thẻ chứa thẻ này
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }

    // Tạo bản sao độc lập của thẻ (dùng khi FlashcardSet.Clone deep-copy thẻ con).
    // Giữ nội dung học: FrontText, BackText, phát âm, loại từ, ví dụ, synonyms, ImageUrl, OrderIndex.
    // Reset identity (Id, FlashcardSetId để EF gán mới), IsStarred, UploadedImagePath
    // (file upload nội bộ không được nhân bản trên đĩa).
    public Flashcard Clone()
    {
        return new Flashcard
        {
            FrontText = FrontText,
            BackText = BackText,
            Pronunciation = Pronunciation,
            PartOfSpeech = PartOfSpeech,
            ExampleSentence = ExampleSentence,
            ExampleMeaning = ExampleMeaning,
            Synonyms = Synonyms,
            ImageUrl = ImageUrl,
            UploadedImagePath = null,
            IsStarred = false,
            OrderIndex = OrderIndex
        };
    }
}
