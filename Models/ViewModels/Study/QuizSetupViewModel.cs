namespace ltwnc.Models.ViewModels.Study;

public enum QuizTimingMode
{
    Preset,
    Custom,
    Untimed
}

public class QuizSetupViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public QuizTimingMode? TimingMode { get; set; }
    public int? SelectedPresetMinutes { get; set; }
    public int? CustomMinutes { get; set; }
    public int? ActiveSessionId { get; set; }
}
