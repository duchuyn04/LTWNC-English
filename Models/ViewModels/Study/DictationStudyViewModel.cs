using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho view học nghe chép
public class DictationStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<DictationCardViewModel> Cards { get; set; } = new();
    public UserStudySettings Settings { get; set; } = new();
    public int SessionId { get; set; }
    public int StreakDays { get; set; }
}
