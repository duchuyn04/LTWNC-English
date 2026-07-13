namespace ltwnc.Models.ViewModels.Study;

// Một thẻ trong phiên nghe chép (JSON/JS đọc PromptText, TTS...)
public class DictationCardViewModel
{
    // Flashcard.Id
    public int Id { get; set; }

    // FrontText
    public string Term { get; set; } = string.Empty;

    // BackText
    public string Definition { get; set; } = string.Empty;

    // Câu ví dụ EN
    public string ExampleSentence { get; set; } = string.Empty;

    // Nghĩa câu VI
    public string ExampleMeaning { get; set; } = string.Empty;

    // Chuỗi TTS / đáp án chuẩn theo content mode (term hoặc example)
    public string PromptText { get; set; } = string.Empty;

    // IPA
    public string Pronunciation { get; set; } = string.Empty;

    // Ảnh hiển thị (upload path hoặc URL ngoài)
    public string? ImageUrl { get; set; }
}
