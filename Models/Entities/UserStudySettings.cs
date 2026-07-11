using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

// Chế độ trả lời trong bài nghe chép
// Term: đọc thuật ngữ, nhập thuật ngữ
// Definition: đọc thuật ngữ, nhập nghĩa
public enum DictationAnswerMode
{
    Term,
    Definition
}

public enum DictationContentMode
{
    Vocabulary,
    ExampleSentence
}

// Entity lưu cài đặt học tập của từng ngườidùng
// Mỗi user chỉ có một bản ghi cài đặt (unique theo UserId)
// Entity lưu cài đặt học tập cá nhân của từng ngườidùng
public class UserStudySettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    // Bộ lọc thẻ: chỉ học thẻ đã đánh sao hoặc thẻ chưa thuộc
    public bool StarredOnly { get; set; }
    public bool UnlearnedOnly { get; set; }

    // Cấu hình hiển thị mặt trước thẻ
    public bool ShowFrontTerm { get; set; } = true;
    public bool ShowFrontDefinition { get; set; }
    public bool ShowFrontIpa { get; set; } = true;
    public bool ShowFrontImage { get; set; }

    // Cấu hình hiển thị mặt sau thẻ
    public bool ShowBackTerm { get; set; }
    public bool ShowBackDefinition { get; set; } = true;
    public bool ShowBackIpa { get; set; }
    public bool ShowBackExample { get; set; } = true;
    public bool ShowBackImage { get; set; } = true;

    // Tùy chọn hiển thị hình ảnh
    public bool HideImage { get; set; }
    public bool BlurImage { get; set; }
    public bool LargeImage { get; set; }

    // Tùy chọn phát âm khi lật thẻ
    public bool PronounceFront { get; set; } = true;
    public bool PronounceBack { get; set; }

    // Cài đặt riêng cho chế độ nghe chép
    public DictationContentMode DictationContentMode { get; set; } = DictationContentMode.Vocabulary;
    public DictationAnswerMode DictationAnswerMode { get; set; } = DictationAnswerMode.Term;
    public bool DictationAutoAdvance { get; set; }
    public float DictationPlaybackSpeed { get; set; } = 1.0f;
    public string? DictationVoiceUri { get; set; }
    public bool DictationShowHint { get; set; } = true;
    public bool DictationAcceptSynonyms { get; set; } = true;
    public bool DictationShuffle { get; set; }

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }
}
