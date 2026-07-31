using System.ComponentModel.DataAnnotations;

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

    // Id user trong bảng Users (cookie auth)
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Ôn tập đến hạn: số thẻ tối đa trong một lượt.
    public int ReviewSessionSize { get; set; } = ReviewSettingsPolicy.DefaultSessionSize;

    // Ôn tập đến hạn: giới hạn khoảng ôn dài hạn theo ngày.
    public int ReviewMaxIntervalDays { get; set; } = ReviewSettingsPolicy.DefaultMaxIntervalDays;

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
}

public static class ReviewSettingsPolicy
{
    public const int DefaultSessionSize = 20;
    public const int MinimumSessionSize = 5;
    public const int MaximumSessionSize = 100;
    public const int DefaultMaxIntervalDays = 30;
    public const int MinimumMaxIntervalDays = 30;
    public const int MaximumMaxIntervalDays = 365;
    public const int DefaultNewCardQuota = 5;
    public const int MinimumNewCardQuota = 0;
    public const int MaximumNewCardQuota = 20;

    public static int ValidateSessionSize(int value)
    {
        if (value < MinimumSessionSize || value > MaximumSessionSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Kích thước lượt ôn phải từ {MinimumSessionSize} đến {MaximumSessionSize}.");
        }

        return value;
    }

    public static int ValidateMaxIntervalDays(int value)
    {
        if (value < MinimumMaxIntervalDays || value > MaximumMaxIntervalDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Khoảng ôn tối đa phải từ {MinimumMaxIntervalDays} đến {MaximumMaxIntervalDays} ngày.");
        }

        return value;
    }

    public static int ValidateNewCardQuota(int value)
    {
        if (value < MinimumNewCardQuota || value > MaximumNewCardQuota)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Hạn mức thẻ mới phải từ {MinimumNewCardQuota} đến {MaximumNewCardQuota} thẻ mỗi ngày.");
        }

        return value;
    }
}
