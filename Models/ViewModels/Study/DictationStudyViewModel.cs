using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// View học nghe chép: session + list thẻ + settings TTS
public class DictationStudyViewModel
{
    // Bộ thẻ đang học
    public int SetId { get; set; }

    // Tiêu đề set (header)
    public string SetTitle { get; set; } = string.Empty;

    // Thẻ đã lọc / shuffle cho phiên
    public List<DictationCardViewModel> Cards { get; set; } = new();

    // Cài đặt dictation (tốc độ, synonym, hint...)
    public StudySettingsViewModel Settings { get; set; } = new();

    // Vocabulary hay ExampleSentence (khớp session)
    public DictationContentMode ContentMode { get; set; }

    // StudySession.Id vừa tạo (POST Check/Complete)
    public int SessionId { get; set; }

    // Dự phòng UI streak (có thể 0 nếu chưa dùng)
    public int StreakDays { get; set; }
}
