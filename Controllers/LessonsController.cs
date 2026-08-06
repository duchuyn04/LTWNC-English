using ltwnc.Models.ViewModels.Lessons;
using ltwnc.Services.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[Authorize]
[Route("Lessons")]
public sealed class LessonsController : Controller
{
    private readonly ILessonService _lessons;

    public LessonsController(ILessonService lessons)
    {
        _lessons = lessons;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<LessonListItem> items = await _lessons.ListPublishedAsync(cancellationToken);
        LessonIndexViewModel model = new()
        {
            Lessons = items.Select((item, index) => new LessonCardViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Summary = item.Summary,
                IndexLabel = $"Bài {(index + 1):00}",
                QuestionCount = 0
            }).ToArray()
        };
        return View(model);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        IReadOnlyList<LessonListItem> published = await _lessons.ListPublishedAsync(cancellationToken);
        int index = published.ToList().FindIndex(item => item.Id == id);
        LessonDetail? detail = await _lessons.GetPublishedAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        LessonDetailsViewModel model = new()
        {
            Id = detail.Id,
            Title = detail.Title,
            Summary = detail.Summary,
            IndexLabel = index >= 0 ? $"Bài {(index + 1):00}" : "Bài học",
            ContentHtml = detail.ContentHtml,
            HasPractice = false
        };
        return View(model);
    }
}
