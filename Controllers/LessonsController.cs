using System.Text.Json;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Lessons;
using ltwnc.Services.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[Authorize]
[Route("Lessons")]
public sealed class LessonsController : Controller
{
    private const string PracticeSessionPrefix = "lesson-practice:";

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
                QuestionCount = item.QuestionCount
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
            HasPractice = detail.QuestionCount > 0
        };
        return View(model);
    }

    [HttpGet("{id:int}/Practice")]
    public async Task<IActionResult> Practice(int id, bool restart = false, CancellationToken cancellationToken = default)
    {
        PracticeBundle? bundle = await _lessons.GetPracticeBundleAsync(id, cancellationToken);
        if (bundle is null)
        {
            return NotFound();
        }

        PracticeRunState state = restart
            ? StartRun(bundle)
            : GetOrStartRun(id, bundle);

        if (state.Index >= state.QuestionIds.Count)
        {
            return View("PracticeResult", new LessonPracticeResultViewModel
            {
                LessonId = bundle.LessonId,
                LessonTitle = bundle.LessonTitle,
                Score = state.Score,
                Total = state.QuestionIds.Count
            });
        }

        PracticeQuestionItem question = bundle.Questions.First(q => q.Id == state.QuestionIds[state.Index]);
        return View(ToPracticeView(bundle, state, question, showFeedback: false));
    }

    [HttpPost("{id:int}/Practice/Answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PracticeAnswer(
        int id,
        LessonPracticeAnswerForm form,
        CancellationToken cancellationToken)
    {
        PracticeBundle? bundle = await _lessons.GetPracticeBundleAsync(id, cancellationToken);
        if (bundle is null)
        {
            return NotFound();
        }

        PracticeRunState? state = ReadRun(id);
        if (state is null || state.Index >= state.QuestionIds.Count)
        {
            return RedirectToAction(nameof(Practice), new { id, restart = true });
        }

        int expectedQuestionId = state.QuestionIds[state.Index];
        if (form.QuestionId != expectedQuestionId)
        {
            return RedirectToAction(nameof(Practice), new { id });
        }

        PracticeQuestionItem question = bundle.Questions.First(q => q.Id == expectedQuestionId);

        if (question.Type == LessonQuestionTypes.Writing)
        {
            GradeWritingResult grade = await _lessons.GradeWritingAsync(
                id,
                form.QuestionId,
                form.WrittenAnswer ?? string.Empty,
                publishedOnly: true,
                cancellationToken);

            if (!grade.Succeeded)
            {
                TempData["PracticeError"] = grade.Error ?? "Không chấm được câu trả lời.";
                return RedirectToAction(nameof(Practice), new { id });
            }

            if (grade.IsCorrect)
            {
                state.Score += 1;
            }

            state.AwaitingNext = true;
            WriteRun(id, state);

            return View("Practice", ToPracticeView(
                bundle,
                state,
                question,
                showFeedback: true,
                isCorrect: grade.IsCorrect,
                writtenAnswer: form.WrittenAnswer,
                acceptedAnswers: grade.AcceptedAnswers ?? []));
        }

        GradeMcqResult mcq = await _lessons.GradeMcqAsync(
            id,
            form.QuestionId,
            form.SelectedIndex,
            publishedOnly: true,
            cancellationToken);

        if (!mcq.Succeeded)
        {
            TempData["PracticeError"] = mcq.Error ?? "Không chấm được câu trả lời.";
            return RedirectToAction(nameof(Practice), new { id });
        }

        if (mcq.IsCorrect)
        {
            state.Score += 1;
        }

        state.AwaitingNext = true;
        WriteRun(id, state);

        return View("Practice", ToPracticeView(
            bundle,
            state,
            question,
            showFeedback: true,
            isCorrect: mcq.IsCorrect,
            selectedIndex: form.SelectedIndex,
            correctIndex: mcq.CorrectOptionIndex));
    }

    private static LessonPracticeViewModel ToPracticeView(
        PracticeBundle bundle,
        PracticeRunState state,
        PracticeQuestionItem question,
        bool showFeedback,
        bool isCorrect = false,
        int? selectedIndex = null,
        int? correctIndex = null,
        string? writtenAnswer = null,
        IReadOnlyList<string>? acceptedAnswers = null) =>
        new()
        {
            LessonId = bundle.LessonId,
            LessonTitle = bundle.LessonTitle,
            Step = state.Index + 1,
            Total = state.QuestionIds.Count,
            Score = state.Score,
            QuestionId = question.Id,
            QuestionType = question.Type,
            Prompt = question.Prompt,
            Options = question.Options,
            ShowFeedback = showFeedback,
            IsCorrect = isCorrect,
            SelectedIndex = selectedIndex,
            CorrectIndex = correctIndex,
            WrittenAnswer = writtenAnswer,
            AcceptedAnswers = acceptedAnswers ?? []
        };

    [HttpPost("{id:int}/Practice/Next")]
    [ValidateAntiForgeryToken]
    public IActionResult PracticeNext(int id)
    {
        PracticeRunState? state = ReadRun(id);
        if (state is null)
        {
            return RedirectToAction(nameof(Practice), new { id, restart = true });
        }

        if (state.AwaitingNext)
        {
            state.Index += 1;
            state.AwaitingNext = false;
            state.LastCorrect = null;
            state.LastSelectedIndex = null;
            state.LastCorrectIndex = null;
            WriteRun(id, state);
        }

        return RedirectToAction(nameof(Practice), new { id });
    }

    private PracticeRunState GetOrStartRun(int lessonId, PracticeBundle bundle)
    {
        PracticeRunState? existing = ReadRun(lessonId);
        if (existing is not null
            && existing.QuestionIds.SequenceEqual(bundle.Questions.Select(q => q.Id)))
        {
            return existing;
        }

        return StartRun(bundle);
    }

    private PracticeRunState StartRun(PracticeBundle bundle)
    {
        PracticeRunState state = new()
        {
            LessonId = bundle.LessonId,
            QuestionIds = bundle.Questions.Select(q => q.Id).ToList(),
            Index = 0,
            Score = 0
        };
        WriteRun(bundle.LessonId, state);
        return state;
    }

    private PracticeRunState? ReadRun(int lessonId)
    {
        string? json = HttpContext.Session.GetString(PracticeSessionPrefix + lessonId);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PracticeRunState>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteRun(int lessonId, PracticeRunState state)
    {
        HttpContext.Session.SetString(
            PracticeSessionPrefix + lessonId,
            JsonSerializer.Serialize(state));
    }

    private sealed class PracticeRunState
    {
        public int LessonId { get; set; }
        public List<int> QuestionIds { get; set; } = [];
        public int Index { get; set; }
        public int Score { get; set; }
        public bool AwaitingNext { get; set; }
        public bool? LastCorrect { get; set; }
        public int? LastSelectedIndex { get; set; }
        public int? LastCorrectIndex { get; set; }
    }
}
