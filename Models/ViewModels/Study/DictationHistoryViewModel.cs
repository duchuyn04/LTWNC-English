namespace ltwnc.Models.ViewModels.Study;

public class DictationHistoryViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<DictationHistoryItemViewModel> Items { get; set; } = new();
}

public class DictationHistoryItemViewModel
{
    public int SessionId { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public string AnsweredText { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public DateTime AnsweredAt { get; set; }
}
