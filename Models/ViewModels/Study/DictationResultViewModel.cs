using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho màn hình tổng kết nghe chép
public class DictationResultViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public DictationContentMode ContentMode { get; set; }
    public int TotalCards { get; set; }
    public int CorrectCount { get; set; }
    public int Score { get; set; }
    public List<DictationResultCardViewModel> WrongCards { get; set; } = new();
}

// Thông tin một thẻ cần ôn trong màn hình tổng kết
public class DictationResultCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
}
