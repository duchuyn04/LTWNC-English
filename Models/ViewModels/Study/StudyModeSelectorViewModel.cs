using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Study Hub /Study/{setId}: stats + list mode + filter
public class StudyModeSelectorViewModel
{
    // Bộ thẻ
    public int SetId { get; set; }

    // Tiêu đề
    public string SetTitle { get; set; } = string.Empty;

    // Mô tả set (có thể null)
    public string? SetDescription { get; set; }

    // Tổng thẻ trong set
    public int TotalCards { get; set; }

    // Thẻ user đã thuộc
    public int LearnedCount { get; set; }

    // Thẻ đang gắn sao
    public int StarredCount { get; set; }

    // % thuộc (0-100)
    public int MasteryPercent { get; set; }

    // Số buổi học 7 ngày gần đây
    public int RecentSessionCount { get; set; }

    // Filter hiện tại: chỉ sao
    public bool StarredOnly { get; set; }

    // Filter hiện tại: chỉ chưa thuộc
    public bool UnlearnedOnly { get; set; }

    // Mode gợi ý (Flashcard hoặc Dictation tùy mastery)
    public StudyMode RecommendedMode { get; set; }

    // Mode đã có strategy thật (Flashcard, Dictation...)
    public List<StudyModeOptionViewModel> Modes { get; set; } = new();

    // Mode "sắp ra mắt" (Quiz, Write, Match...) chưa implement
    public List<StudyModeOptionViewModel> RoadmapModes { get; set; } = new();

    // Cảnh báo UI (filter rỗng, fallback mode...)
    public List<string> Warnings { get; set; } = new();
}

// Một thẻ mode trên hub (strategy.BuildOption hoặc roadmap)
public class StudyModeOptionViewModel
{
    // Enum mode
    public StudyMode Mode { get; set; }

    // Tên hiện UI
    public string Name { get; set; } = string.Empty;

    // Mô tả ngắn
    public string Description { get; set; } = string.Empty;

    // Class Phosphor icon (vd. ph-cards)
    public string IconClass { get; set; } = string.Empty;

    // URL vào mode (roadmap có thể rỗng)
    public string ActionUrl { get; set; } = string.Empty;

    // false = disable click (không thẻ / sắp ra mắt)
    public bool IsAvailable { get; set; }

    // true = mode được đề xuất
    public bool IsRecommended { get; set; }

    // Số thẻ sau lọc cho mode này
    public int CardCount { get; set; }

    // Ước lượng thời gian (giây)
    public int EstimatedSeconds { get; set; }

    // Lý do không học được (null nếu available)
    public string? UnavailableReason { get; set; }
}
