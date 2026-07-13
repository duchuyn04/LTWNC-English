using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

// Cách chấm nghe chép (cũ, giữ enum; app hiện chủ yếu theo content mode)
public enum DictationAnswerMode
{
    // Đáp án là thuật ngữ
    Term,
    // Đáp án là định nghĩa
    Definition
}

// Nội dung đọc khi nghe chép
public enum DictationContentMode
{
    // Đọc / gõ thuật ngữ
    Vocabulary,
    // Đọc / gõ câu ví dụ
    ExampleSentence
}

// Cài đặt học của một user (một dòng / UserId).
// Bộ lọc, mặt thẻ Flashcard, và tùy chọn Dictation.
public class UserStudySettings
{
    [Key]
    public int Id { get; set; }

    // AspNetUsers.Id
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Bộ lọc Study Hub / màn học: chỉ thẻ đã sao
    public bool StarredOnly { get; set; }

    // Bộ lọc: chỉ thẻ chưa thuộc
    public bool UnlearnedOnly { get; set; }

    // Flashcard: hiện term mặt trước
    public bool ShowFrontTerm { get; set; } = true;

    // Flashcard: hiện định nghĩa mặt trước
    public bool ShowFrontDefinition { get; set; }

    // Flashcard: hiện IPA mặt trước
    public bool ShowFrontIpa { get; set; } = true;

    // Flashcard: hiện ảnh mặt trước
    public bool ShowFrontImage { get; set; }

    // Flashcard: hiện term mặt sau
    public bool ShowBackTerm { get; set; }

    // Flashcard: hiện định nghĩa mặt sau
    public bool ShowBackDefinition { get; set; } = true;

    // Flashcard: hiện IPA mặt sau
    public bool ShowBackIpa { get; set; }

    // Flashcard: hiện câu ví dụ mặt sau
    public bool ShowBackExample { get; set; } = true;

    // Flashcard: hiện ảnh mặt sau
    public bool ShowBackImage { get; set; } = true;

    // Ẩn hết ảnh
    public bool HideImage { get; set; }

    // Làm mờ ảnh (che gợi ý)
    public bool BlurImage { get; set; }

    // Ảnh cỡ lớn
    public bool LargeImage { get; set; }

    // TTS khi hiện mặt trước
    public bool PronounceFront { get; set; } = true;

    // TTS khi lật mặt sau
    public bool PronounceBack { get; set; }

    // Dictation: Vocabulary hay ExampleSentence
    public DictationContentMode DictationContentMode { get; set; } = DictationContentMode.Vocabulary;

    // Dictation: kiểu đáp án (giữ tương thích)
    public DictationAnswerMode DictationAnswerMode { get; set; } = DictationAnswerMode.Term;

    // Tự sang câu sau khi đúng
    public bool DictationAutoAdvance { get; set; }

    // Tốc độ Web Speech (1.0 = bình thường)
    public float DictationPlaybackSpeed { get; set; } = 1.0f;

    // URI giọng nói trình duyệt (null = mặc định)
    public string? DictationVoiceUri { get; set; }

    // Hiện gợi ý khi sai
    public bool DictationShowHint { get; set; } = true;

    // Chấp nhận synonym khi chấm vocabulary
    public bool DictationAcceptSynonyms { get; set; } = true;

    // Xáo trộn thứ tự thẻ khi vào dictation
    public bool DictationShuffle { get; set; }

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }
}
