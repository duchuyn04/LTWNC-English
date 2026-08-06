using System.ComponentModel.DataAnnotations;
using ltwnc.Models.Entities;
using ltwnc.Services.Lessons;

namespace ltwnc.Models.ViewModels.Lessons;

public sealed class LessonIndexViewModel
{
    public IReadOnlyList<LessonCardViewModel> Lessons { get; init; } = [];
}

public sealed class LessonCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string IndexLabel { get; init; } = string.Empty;
    public int QuestionCount { get; init; }
}

public sealed class LessonDetailsViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string IndexLabel { get; init; } = string.Empty;
    public string ContentHtml { get; init; } = string.Empty;
    public bool HasPractice { get; init; }
}

public sealed class AdminLessonIndexViewModel
{
    public IReadOnlyList<AdminLessonRowViewModel> Lessons { get; init; } = [];
}

public sealed class AdminLessonRowViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Status { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public int QuestionCount { get; init; }
}

public sealed class AdminLessonEditViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề bắt buộc.")]
    [MaxLength(200)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Tóm tắt")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Nội dung bắt buộc.")]
    [Display(Name = "Nội dung (Markdown)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = LessonStatus.Draft;

    [Display(Name = "Thứ tự")]
    public int? SortOrder { get; set; }

    public string? PreviewHtml { get; set; }

    public static AdminLessonEditViewModel FromDetail(LessonDetail detail) => new()
    {
        Id = detail.Id,
        Title = detail.Title,
        Summary = detail.Summary,
        ContentMarkdown = detail.ContentMarkdown,
        Status = detail.Status,
        SortOrder = detail.SortOrder,
        PreviewHtml = detail.ContentHtml
    };
}

public sealed class AdminLessonQuestionsViewModel
{
    public int LessonId { get; init; }
    public string LessonTitle { get; init; } = string.Empty;
    public IReadOnlyList<AdminLessonQuestionRowViewModel> Questions { get; init; } = [];
    public AdminMcqQuestionForm Form { get; init; } = new();
}

public sealed class AdminLessonQuestionRowViewModel
{
    public int Id { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
}

public sealed class AdminMcqQuestionForm
{
    public int LessonId { get; set; }

    [Required(ErrorMessage = "Đề bài bắt buộc.")]
    [Display(Name = "Đề bài")]
    public string Prompt { get; set; } = string.Empty;

    [Display(Name = "Lựa chọn A")]
    public string? OptionA { get; set; }

    [Display(Name = "Lựa chọn B")]
    public string? OptionB { get; set; }

    [Display(Name = "Lựa chọn C")]
    public string? OptionC { get; set; }

    [Display(Name = "Lựa chọn D")]
    public string? OptionD { get; set; }

    [Display(Name = "Đáp án đúng")]
    public int CorrectOptionIndex { get; set; }
}

public sealed class LessonPracticeViewModel
{
    public int LessonId { get; init; }
    public string LessonTitle { get; init; } = string.Empty;
    public int Step { get; init; }
    public int Total { get; init; }
    public int Score { get; init; }
    public int QuestionId { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
    public bool ShowFeedback { get; init; }
    public bool IsCorrect { get; init; }
    public int? SelectedIndex { get; init; }
    public int? CorrectIndex { get; init; }

    public int ProgressPercent => Total <= 0 ? 0 : (int)Math.Round(100.0 * (Step - (ShowFeedback ? 0 : 1)) / Total);
}

public sealed class LessonPracticeAnswerForm
{
    public int QuestionId { get; set; }
    public int SelectedIndex { get; set; }
}

public sealed class LessonPracticeResultViewModel
{
    public int LessonId { get; init; }
    public string LessonTitle { get; init; } = string.Empty;
    public int Score { get; init; }
    public int Total { get; init; }
}
