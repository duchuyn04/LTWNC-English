using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Màn tổng kết /Study/{setId}/Dictation/Result/{sessionId}
public class DictationResultViewModel
{
    // Bộ thẻ
    public int SetId { get; set; }

    // Tiêu đề set
    public string SetTitle { get; set; } = string.Empty;

    // Phiên vừa xong
    public int SessionId { get; set; }

    // Content mode của phiên
    public DictationContentMode ContentMode { get; set; }

    // Số câu đã trả lời
    public int TotalCards { get; set; }

    // Số câu đúng
    public int CorrectCount { get; set; }

    // Điểm lưu session
    public int Score { get; set; }

    // Thẻ sai để ôn lại
    public List<DictationResultCardViewModel> WrongCards { get; set; } = new();
}

// Một thẻ sai trên màn kết quả
public class DictationResultCardViewModel
{
    // Flashcard.Id
    public int Id { get; set; }

    // Term
    public string Term { get; set; } = string.Empty;

    // Definition
    public string Definition { get; set; } = string.Empty;

    // IPA
    public string Pronunciation { get; set; } = string.Empty;

    // Câu ví dụ
    public string ExampleSentence { get; set; } = string.Empty;

    // Nghĩa câu
    public string ExampleMeaning { get; set; } = string.Empty;
}
