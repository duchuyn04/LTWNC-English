using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Lessons;

public sealed class LessonService : ILessonService
{
    private const int TitleMaxLength = 200;
    private const int SummaryMaxLength = 500;
    private const int PromptMaxLength = 2000;
    private const int OptionMaxLength = 500;
    private const int MinMcqOptions = 2;
    private const int MaxMcqOptions = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public LessonService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LessonListItem>> ListForAdminAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Lessons.AsNoTracking()
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .Select(lesson => new LessonListItem(
                lesson.Id,
                lesson.Title,
                lesson.Summary,
                lesson.Status,
                lesson.SortOrder,
                lesson.UpdatedAtUtc,
                lesson.Questions.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LessonListItem>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Status == LessonStatus.Published)
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .Select(lesson => new LessonListItem(
                lesson.Id,
                lesson.Title,
                lesson.Summary,
                lesson.Status,
                lesson.SortOrder,
                lesson.UpdatedAtUtc,
                lesson.Questions.Count))
            .ToListAsync(cancellationToken);
    }

    public Task<LessonDetail?> GetForAdminAsync(int id, CancellationToken cancellationToken = default)
    {
        return GetDetailAsync(id, publishedOnly: false, cancellationToken);
    }

    public Task<LessonDetail?> GetPublishedAsync(int id, CancellationToken cancellationToken = default)
    {
        return GetDetailAsync(id, publishedOnly: true, cancellationToken);
    }

    public async Task<LessonSaveResult> SaveAsync(
        LessonSaveCommand command,
        CancellationToken cancellationToken = default)
    {
        string title = (command.Title ?? string.Empty).Trim();
        string? summary = string.IsNullOrWhiteSpace(command.Summary)
            ? null
            : command.Summary.Trim();
        string content = (command.ContentMarkdown ?? string.Empty).Trim();
        string status = (command.Status ?? string.Empty).Trim();

        if (title.Length == 0 || title.Length > TitleMaxLength)
        {
            return new LessonSaveResult(false, "Tiêu đề bắt buộc, tối đa 200 ký tự.");
        }

        if (content.Length == 0)
        {
            return new LessonSaveResult(false, "Nội dung bài học bắt buộc.");
        }

        if (summary is { Length: > SummaryMaxLength })
        {
            return new LessonSaveResult(false, "Tóm tắt tối đa 500 ký tự.");
        }

        if (!LessonStatus.IsValid(status))
        {
            return new LessonSaveResult(false, "Trạng thái không hợp lệ.");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        if (command.Id is null or 0)
        {
            int nextOrder = command.SortOrder
                ?? await NextLessonSortOrderAsync(cancellationToken);

            Lesson created = new()
            {
                Title = title,
                Summary = summary,
                ContentMarkdown = content,
                Status = LessonStatus.IsValid(status) ? status : LessonStatus.Draft,
                SortOrder = nextOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = command.ActorUserId,
                UpdatedByUserId = command.ActorUserId
            };

            _db.Lessons.Add(created);
            await _db.SaveChangesAsync(cancellationToken);
            return new LessonSaveResult(true, LessonId: created.Id);
        }

        Lesson? existing = await _db.Lessons
            .SingleOrDefaultAsync(lesson => lesson.Id == command.Id.Value, cancellationToken);
        if (existing is null)
        {
            return new LessonSaveResult(false, "Không tìm thấy bài học.");
        }

        existing.Title = title;
        existing.Summary = summary;
        existing.ContentMarkdown = content;
        existing.Status = status;
        if (command.SortOrder.HasValue)
        {
            existing.SortOrder = command.SortOrder.Value;
        }

        existing.UpdatedAtUtc = now;
        existing.UpdatedByUserId = command.ActorUserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new LessonSaveResult(true, LessonId: existing.Id);
    }

    public string RenderMarkdown(string markdown) => LessonMarkdownRenderer.ToHtml(markdown);

    public async Task<IReadOnlyList<LessonQuestionAdminItem>> ListQuestionsForAdminAsync(
        int lessonId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await _db.Lessons.AsNoTracking()
            .AnyAsync(lesson => lesson.Id == lessonId, cancellationToken);
        if (!exists)
        {
            return [];
        }

        List<LessonQuestion> rows = await _db.LessonQuestions.AsNoTracking()
            .Where(question => question.LessonId == lessonId)
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToAdminItem).ToArray();
    }

    public async Task<LessonQuestionMutationResult> AddMcqQuestionAsync(
        AddMcqQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        string prompt = (command.Prompt ?? string.Empty).Trim();
        if (prompt.Length == 0 || prompt.Length > PromptMaxLength)
        {
            return new LessonQuestionMutationResult(false, "Đề bài bắt buộc.");
        }

        List<string> options = (command.Options ?? [])
            .Select(option => (option ?? string.Empty).Trim())
            .Where(option => option.Length > 0)
            .ToList();

        if (options.Count < MinMcqOptions)
        {
            return new LessonQuestionMutationResult(false, "Cần ít nhất 2 lựa chọn.");
        }

        if (options.Count > MaxMcqOptions)
        {
            return new LessonQuestionMutationResult(false, $"Tối đa {MaxMcqOptions} lựa chọn.");
        }

        if (options.Any(option => option.Length > OptionMaxLength))
        {
            return new LessonQuestionMutationResult(false, "Mỗi lựa chọn tối đa 500 ký tự.");
        }

        if (command.CorrectOptionIndex < 0 || command.CorrectOptionIndex >= options.Count)
        {
            return new LessonQuestionMutationResult(false, "Đáp án đúng không hợp lệ.");
        }

        bool lessonExists = await _db.Lessons.AnyAsync(lesson => lesson.Id == command.LessonId, cancellationToken);
        if (!lessonExists)
        {
            return new LessonQuestionMutationResult(false, "Không tìm thấy bài học.");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int nextOrder = await NextQuestionSortOrderAsync(command.LessonId, cancellationToken);

        LessonQuestion question = new()
        {
            LessonId = command.LessonId,
            Type = LessonQuestionTypes.MultipleChoice,
            Prompt = prompt,
            SortOrder = nextOrder,
            OptionsJson = JsonSerializer.Serialize(options, JsonOptions),
            CorrectOptionIndex = command.CorrectOptionIndex,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.LessonQuestions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);
        return new LessonQuestionMutationResult(true, QuestionId: question.Id);
    }

    public async Task<LessonQuestionMutationResult> AddWritingQuestionAsync(
        AddWritingQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        string prompt = (command.Prompt ?? string.Empty).Trim();
        if (prompt.Length == 0 || prompt.Length > PromptMaxLength)
        {
            return new LessonQuestionMutationResult(false, "Đề bài bắt buộc.");
        }

        List<string> answers = (command.AcceptedAnswers ?? [])
            .Select(answer => (answer ?? string.Empty).Trim())
            .Where(answer => answer.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (answers.Count == 0)
        {
            return new LessonQuestionMutationResult(false, "Cần ít nhất một đáp án chấp nhận.");
        }

        if (answers.Any(answer => answer.Length > OptionMaxLength))
        {
            return new LessonQuestionMutationResult(false, "Mỗi đáp án tối đa 500 ký tự.");
        }

        bool lessonExists = await _db.Lessons.AnyAsync(lesson => lesson.Id == command.LessonId, cancellationToken);
        if (!lessonExists)
        {
            return new LessonQuestionMutationResult(false, "Không tìm thấy bài học.");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int nextOrder = await NextQuestionSortOrderAsync(command.LessonId, cancellationToken);

        LessonQuestion question = new()
        {
            LessonId = command.LessonId,
            Type = LessonQuestionTypes.Writing,
            Prompt = prompt,
            SortOrder = nextOrder,
            AcceptedAnswersJson = JsonSerializer.Serialize(answers, JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.LessonQuestions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);
        return new LessonQuestionMutationResult(true, QuestionId: question.Id);
    }

    public async Task<LessonQuestionMutationResult> DeleteQuestionAsync(
        int lessonId,
        int questionId,
        CancellationToken cancellationToken = default)
    {
        LessonQuestion? question = await _db.LessonQuestions
            .SingleOrDefaultAsync(
                row => row.Id == questionId && row.LessonId == lessonId,
                cancellationToken);

        if (question is null)
        {
            return new LessonQuestionMutationResult(false, "Không tìm thấy câu hỏi.");
        }

        _db.LessonQuestions.Remove(question);
        await _db.SaveChangesAsync(cancellationToken);
        return new LessonQuestionMutationResult(true, QuestionId: questionId);
    }

    public async Task<PracticeBundle?> GetPracticeBundleAsync(
        int lessonId,
        CancellationToken cancellationToken = default)
    {
        Lesson? lesson = await _db.Lessons.AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == lessonId && row.Status == LessonStatus.Published,
                cancellationToken);

        if (lesson is null)
        {
            return null;
        }

        List<LessonQuestion> questions = await _db.LessonQuestions.AsNoTracking()
            .Where(question =>
                question.LessonId == lessonId
                && (question.Type == LessonQuestionTypes.MultipleChoice
                    || question.Type == LessonQuestionTypes.Writing))
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return null;
        }

        IReadOnlyList<PracticeQuestionItem> items = questions
            .Select(question => new PracticeQuestionItem(
                question.Id,
                question.Type,
                question.Prompt,
                question.Type == LessonQuestionTypes.MultipleChoice
                    ? ReadStringList(question.OptionsJson)
                    : Array.Empty<string>()))
            .ToArray();

        return new PracticeBundle(lesson.Id, lesson.Title, items);
    }

    public async Task<GradeMcqResult> GradeMcqAsync(
        int lessonId,
        int questionId,
        int selectedIndex,
        bool publishedOnly = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Lesson> lessonQuery = _db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Id == lessonId);
        if (publishedOnly)
        {
            lessonQuery = lessonQuery.Where(lesson => lesson.Status == LessonStatus.Published);
        }

        bool lessonOk = await lessonQuery.AnyAsync(cancellationToken);
        if (!lessonOk)
        {
            return new GradeMcqResult(false, Error: "Bài học không khả dụng.");
        }

        LessonQuestion? question = await _db.LessonQuestions.AsNoTracking()
            .SingleOrDefaultAsync(
                row =>
                    row.Id == questionId
                    && row.LessonId == lessonId
                    && row.Type == LessonQuestionTypes.MultipleChoice,
                cancellationToken);

        if (question is null || question.CorrectOptionIndex is null)
        {
            return new GradeMcqResult(false, Error: "Không tìm thấy câu hỏi.");
        }

        IReadOnlyList<string> options = ReadStringList(question.OptionsJson);
        if (selectedIndex < 0 || selectedIndex >= options.Count)
        {
            return new GradeMcqResult(false, Error: "Lựa chọn không hợp lệ.");
        }

        int correct = question.CorrectOptionIndex.Value;
        return new GradeMcqResult(
            Succeeded: true,
            IsCorrect: selectedIndex == correct,
            CorrectOptionIndex: correct);
    }

    public async Task<GradeWritingResult> GradeWritingAsync(
        int lessonId,
        int questionId,
        string answer,
        bool publishedOnly = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Lesson> lessonQuery = _db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Id == lessonId);
        if (publishedOnly)
        {
            lessonQuery = lessonQuery.Where(lesson => lesson.Status == LessonStatus.Published);
        }

        bool lessonOk = await lessonQuery.AnyAsync(cancellationToken);
        if (!lessonOk)
        {
            return new GradeWritingResult(false, Error: "Bài học không khả dụng.");
        }

        LessonQuestion? question = await _db.LessonQuestions.AsNoTracking()
            .SingleOrDefaultAsync(
                row =>
                    row.Id == questionId
                    && row.LessonId == lessonId
                    && row.Type == LessonQuestionTypes.Writing,
                cancellationToken);

        if (question is null)
        {
            return new GradeWritingResult(false, Error: "Không tìm thấy câu hỏi.");
        }

        IReadOnlyList<string> accepted = ReadStringList(question.AcceptedAnswersJson);
        if (accepted.Count == 0)
        {
            return new GradeWritingResult(false, Error: "Câu hỏi chưa có đáp án.");
        }

        string normalized = NormalizeWritingAnswer(answer);
        if (normalized.Length == 0)
        {
            return new GradeWritingResult(false, Error: "Hãy nhập câu trả lời.");
        }

        bool isCorrect = accepted.Any(item => NormalizeWritingAnswer(item) == normalized);
        return new GradeWritingResult(
            Succeeded: true,
            IsCorrect: isCorrect,
            AcceptedAnswers: accepted);
    }

    public static string NormalizeWritingAnswer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            value.Trim().ToLowerInvariant(),
            "\\s+",
            " ");
    }

    private async Task<LessonDetail?> GetDetailAsync(
        int id,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        IQueryable<Lesson> query = _db.Lessons.AsNoTracking().Where(lesson => lesson.Id == id);
        if (publishedOnly)
        {
            query = query.Where(lesson => lesson.Status == LessonStatus.Published);
        }

        var row = await query
            .Select(lesson => new
            {
                Lesson = lesson,
                QuestionCount = lesson.Questions.Count
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToDetail(row.Lesson, row.QuestionCount);
    }

    private async Task<int> NextLessonSortOrderAsync(CancellationToken cancellationToken)
    {
        int? max = await _db.Lessons.MaxAsync(lesson => (int?)lesson.SortOrder, cancellationToken);
        return (max ?? 0) + 1;
    }

    private async Task<int> NextQuestionSortOrderAsync(int lessonId, CancellationToken cancellationToken)
    {
        int? max = await _db.LessonQuestions
            .Where(question => question.LessonId == lessonId)
            .MaxAsync(question => (int?)question.SortOrder, cancellationToken);
        return (max ?? 0) + 1;
    }

    private LessonDetail ToDetail(Lesson lesson, int questionCount) =>
        new(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ContentMarkdown,
            RenderMarkdown(lesson.ContentMarkdown),
            lesson.Status,
            lesson.SortOrder,
            lesson.CreatedAtUtc,
            lesson.UpdatedAtUtc,
            questionCount);

    private static LessonQuestionAdminItem ToAdminItem(LessonQuestion question) =>
        new(
            question.Id,
            question.Type,
            question.Prompt,
            question.SortOrder,
            ReadStringList(question.OptionsJson),
            question.CorrectOptionIndex,
            ReadStringList(question.AcceptedAnswersJson));

    private static IReadOnlyList<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
