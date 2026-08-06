using System.Security.Claims;
using ltwnc.Models.Entities;
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
                QuestionCount = item.QuestionCount
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

    [HttpGet("{id:int}/Questions")]
    public async Task<IActionResult> Questions(int id, CancellationToken cancellationToken)
    {
        LessonDetail? lesson = await _lessons.GetForAdminAsync(id, cancellationToken);
        if (lesson is null)
        {
            return NotFound();
        }

        IReadOnlyList<LessonQuestionAdminItem> questions =
            await _lessons.ListQuestionsForAdminAsync(id, cancellationToken);

        return View(BuildQuestionsPage(lesson, questions));
    }

    [HttpPost("{id:int}/Questions/AddMcq")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMcq(
        int id,
        AdminMcqQuestionForm mcqForm,
        CancellationToken cancellationToken)
    {
        LessonDetail? lesson = await _lessons.GetForAdminAsync(id, cancellationToken);
        if (lesson is null)
        {
            return NotFound();
        }

        mcqForm.LessonId = id;
        List<string> options = new[]
            {
                mcqForm.OptionA, mcqForm.OptionB, mcqForm.OptionC, mcqForm.OptionD
            }
            .Select(option => (option ?? string.Empty).Trim())
            .Where(option => option.Length > 0)
            .ToList();

        LessonQuestionMutationResult result = await _lessons.AddMcqQuestionAsync(
            new AddMcqQuestionCommand(id, mcqForm.Prompt ?? string.Empty, options, mcqForm.CorrectOptionIndex),
            cancellationToken);

        if (!result.Succeeded)
        {
            IReadOnlyList<LessonQuestionAdminItem> questions =
                await _lessons.ListQuestionsForAdminAsync(id, cancellationToken);
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được câu hỏi.");
            return View("Questions", BuildQuestionsPage(lesson, questions, mcqForm: mcqForm));
        }

        TempData["AdminLessonsSuccess"] = "Đã thêm câu trắc nghiệm.";
        return RedirectToAction(nameof(Questions), new { id });
    }

    [HttpPost("{id:int}/Questions/AddWriting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddWriting(
        int id,
        AdminWritingQuestionForm writingForm,
        CancellationToken cancellationToken)
    {
        LessonDetail? lesson = await _lessons.GetForAdminAsync(id, cancellationToken);
        if (lesson is null)
        {
            return NotFound();
        }

        writingForm.LessonId = id;
        List<string> answers = (writingForm.AcceptedAnswersText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(answer => answer.Length > 0)
            .ToList();

        LessonQuestionMutationResult result = await _lessons.AddWritingQuestionAsync(
            new AddWritingQuestionCommand(id, writingForm.Prompt ?? string.Empty, answers),
            cancellationToken);

        if (!result.Succeeded)
        {
            IReadOnlyList<LessonQuestionAdminItem> questions =
                await _lessons.ListQuestionsForAdminAsync(id, cancellationToken);
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thêm được câu viết.");
            return View("Questions", BuildQuestionsPage(lesson, questions, writingForm: writingForm));
        }

        TempData["AdminLessonsSuccess"] = "Đã thêm câu viết.";
        return RedirectToAction(nameof(Questions), new { id });
    }

    [HttpPost("{id:int}/Questions/{questionId:int}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(
        int id,
        int questionId,
        CancellationToken cancellationToken)
    {
        LessonQuestionMutationResult result =
            await _lessons.DeleteQuestionAsync(id, questionId, cancellationToken);
        TempData[result.Succeeded ? "AdminLessonsSuccess" : "AdminLessonsError"] =
            result.Succeeded ? "Đã xóa câu hỏi." : (result.Error ?? "Không xóa được.");
        return RedirectToAction(nameof(Questions), new { id });
    }

    private static AdminLessonQuestionsViewModel BuildQuestionsPage(
        LessonDetail lesson,
        IReadOnlyList<LessonQuestionAdminItem> questions,
        AdminMcqQuestionForm? mcqForm = null,
        AdminWritingQuestionForm? writingForm = null) =>
        new()
        {
            LessonId = lesson.Id,
            LessonTitle = lesson.Title,
            Questions = questions.Select(ToQuestionRow).ToArray(),
            McqForm = mcqForm ?? new AdminMcqQuestionForm { LessonId = lesson.Id },
            WritingForm = writingForm ?? new AdminWritingQuestionForm { LessonId = lesson.Id }
        };

    private static AdminLessonQuestionRowViewModel ToQuestionRow(LessonQuestionAdminItem item)
    {
        if (item.Type == LessonQuestionTypes.Writing)
        {
            return new AdminLessonQuestionRowViewModel
            {
                Id = item.Id,
                TypeLabel = "Câu viết",
                Prompt = item.Prompt,
                Meta = item.AcceptedAnswers.Count == 0
                    ? "Chưa có đáp án"
                    : $"Đáp án: {string.Join(" / ", item.AcceptedAnswers)}"
            };
        }

        int correct = item.CorrectOptionIndex ?? 0;
        string letter = correct >= 0 && correct < 26
            ? ((char)('A' + correct)).ToString()
            : (correct + 1).ToString();
        return new AdminLessonQuestionRowViewModel
        {
            Id = item.Id,
            TypeLabel = "Trắc nghiệm",
            Prompt = item.Prompt,
            Meta = $"{item.Options.Count} lựa chọn · đúng: {letter}"
        };
    }

    private string? ActorUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
