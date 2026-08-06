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
