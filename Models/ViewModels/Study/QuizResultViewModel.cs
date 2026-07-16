using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

public class QuizResultViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public List<QuizWrongAnswerViewModel> WrongAnswers { get; set; } = new();
}

public class QuizWrongAnswerViewModel
{
    public QuizQuestionDirection Direction { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public string SelectedAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
}
