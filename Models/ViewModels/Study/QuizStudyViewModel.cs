using ltwnc.Models.Entities;
using System.Text.Json.Serialization;

namespace ltwnc.Models.ViewModels.Study;

public class QuizStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int QuestionId { get; set; }
    public int CurrentNumber { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public int? RemainingSeconds { get; set; }
    public QuizQuestionDirection Direction { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public bool IsReviewOnly { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SelectedChoiceIndex { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CorrectChoiceIndex { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsCorrect { get; set; }
    public int? PreviousQuestionId { get; set; }
    public int? NextQuestionId { get; set; }
    public int? CurrentPendingQuestionId { get; set; }
}
