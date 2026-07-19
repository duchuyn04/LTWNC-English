namespace ltwnc.Services.CardActions;

// Bản chụp một thẻ trước khi xóa, đủ để Undo khôi phục đúng Id và nội dung.
public class FlashcardSnapshot
{
    // Id gốc (cần IDENTITY_INSERT khi restore)
    public int Id { get; set; }

    // Bộ thẻ chứa thẻ
    public int FlashcardSetId { get; set; }

    // Mặt trước (thuật ngữ)
    public string FrontText { get; set; } = string.Empty;

    // Mặt sau (định nghĩa)
    public string BackText { get; set; } = string.Empty;

    // IPA
    public string Pronunciation { get; set; } = string.Empty;

    // Loại từ
    public string PartOfSpeech { get; set; } = string.Empty;

    // Câu ví dụ EN
    public string ExampleSentence { get; set; } = string.Empty;

    // Nghĩa câu ví dụ VI
    public string ExampleMeaning { get; set; } = string.Empty;

    // Đồng nghĩa (chuỗi, có thể null)
    public string? Synonyms { get; set; }

    // URL ảnh ngoài
    public string? ImageUrl { get; set; }

    // Đường dẫn ảnh upload nội bộ
    public string? UploadedImagePath { get; set; }

    // Trạng thái sao lúc xóa
    public bool IsStarred { get; set; }

    // Thứ tự trong bộ
    public int OrderIndex { get; set; }

    // Progress gắn thẻ (xóa cascade, restore kèm theo)
    public List<UserProgressSnapshot> UserProgresses { get; set; } = new();

    // Chi tiết câu nghe chép gắn thẻ (cũng restore kèm)
    public List<DictationSessionDetailSnapshot> DictationSessionDetails { get; set; } = new();

    public List<EnglishMissionTargetWordSnapshot> EnglishMissionTargetWords { get; set; } = new();
}

public class EnglishMissionTargetWordSnapshot
{
    public int Id { get; set; }
    public int EnglishMissionId { get; set; }
    public int FlashcardId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public bool IsUsed { get; set; }
    public int? FirstUsedTurn { get; set; }
}

// Bản chụp một dòng DictationSessionDetail trước khi xóa thẻ
public class DictationSessionDetailSnapshot
{
    // Id gốc
    public int Id { get; set; }

    // Phiên nghe chép chứa câu này
    public int StudySessionId { get; set; }

    // Thẻ được hỏi
    public int FlashcardId { get; set; }

    // User trả lời đúng hay sai
    public bool IsCorrect { get; set; }

    // Text user đã nhập
    public string AnsweredText { get; set; } = string.Empty;

    // Thời điểm ghi detail
    public DateTime CreatedAt { get; set; }
}
