using System.Security.Claims;
using ltwnc.Models.ViewModels.Lessons;
using ltwnc.Services.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminAreaPolicy.Name)]
[Route("Admin/Lessons")]
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
        IReadOnlyList<LessonListItem> items = await _lessons.ListForAdminAsync(cancellationToken);
        return View(new AdminLessonIndexViewModel
        {
            Lessons = items.Select(item => new AdminLessonRowViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Summary = item.Summary,
                Status = item.Status,
                SortOrder = item.SortOrder,
                QuestionCount = 0
            }).ToArray()
        });
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("Edit", new AdminLessonEditViewModel());
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        LessonDetail? detail = await _lessons.GetForAdminAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return View(AdminLessonEditViewModel.FromDetail(detail));
    }

    [HttpGet("{id:int}/Preview")]
    public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
    {
        LessonDetail? detail = await _lessons.GetForAdminAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return View(AdminLessonEditViewModel.FromDetail(detail));
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminLessonEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.PreviewHtml = _lessons.RenderMarkdown(model.ContentMarkdown ?? string.Empty);
            return View("Edit", model);
        }

        LessonSaveResult result = await _lessons.SaveAsync(
            new LessonSaveCommand(
                model.Id,
                model.Title,
                model.Summary,
                model.ContentMarkdown,
                model.Status,
                model.Id.HasValue ? model.SortOrder : null,
                ActorUserId()),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được bài học.");
            model.PreviewHtml = _lessons.RenderMarkdown(model.ContentMarkdown ?? string.Empty);
            return View("Edit", model);
        }

        TempData["AdminLessonsSuccess"] = model.Id.HasValue ? "Đã cập nhật bài học." : "Đã tạo bài học.";
        return RedirectToAction(nameof(Edit), new { id = result.LessonId });
    }

    [HttpPost("PreviewMarkdown")]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown([FromForm] string? contentMarkdown)
    {
        return PartialView("_MarkdownPreview", _lessons.RenderMarkdown(contentMarkdown ?? string.Empty));
    }

    private string? ActorUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
