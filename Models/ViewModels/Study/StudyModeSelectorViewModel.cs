using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho view chọn chế độ học (/Study/{setId})
public class StudyModeSelectorViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public string? SetDescription { get; set; }

    public int TotalCards { get; set; }
    public int LearnedCount { get; set; }
    public int StarredCount { get; set; }
    public int MasteryPercent { get; set; }
    public int RecentSessionCount { get; set; }

    public bool StarredOnly { get; set; }
    public bool UnlearnedOnly { get; set; }

    public StudyMode RecommendedMode { get; set; }
    public List<StudyModeOptionViewModel> Modes { get; set; } = new();
    public List<StudyModeOptionViewModel> RoadmapModes { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Thông tin một chế độ học trên Study Hub
public class StudyModeOptionViewModel
{
    public StudyMode Mode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsRecommended { get; set; }
    public int CardCount { get; set; }
    public int EstimatedSeconds { get; set; }
    public string? UnavailableReason { get; set; }
}
