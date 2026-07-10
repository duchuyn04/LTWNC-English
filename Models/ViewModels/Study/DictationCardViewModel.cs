namespace ltwnc.Models.ViewModels.Study;

// Thông tin một thẻ hiển thị trong bài nghe chép
public class DictationCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
