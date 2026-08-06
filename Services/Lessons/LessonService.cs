using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Lessons;

public sealed class LessonService : ILessonService
{
    private const int TitleMaxLength = 200;
    private const int SummaryMaxLength = 500;

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
        List<Lesson> rows = await _db.Lessons.AsNoTracking()
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToListItem).ToArray();
    }

    public async Task<IReadOnlyList<LessonListItem>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        List<Lesson> rows = await _db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Status == LessonStatus.Published)
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToListItem).ToArray();
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
                ?? await NextSortOrderAsync(cancellationToken);

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

        Lesson? lesson = await query.SingleOrDefaultAsync(cancellationToken);
        return lesson is null ? null : ToDetail(lesson);
    }

    private async Task<int> NextSortOrderAsync(CancellationToken cancellationToken)
    {
        int? max = await _db.Lessons.MaxAsync(lesson => (int?)lesson.SortOrder, cancellationToken);
        return (max ?? 0) + 1;
    }

    private static LessonListItem ToListItem(Lesson lesson) =>
        new(lesson.Id, lesson.Title, lesson.Summary, lesson.Status, lesson.SortOrder, lesson.UpdatedAtUtc);

    private LessonDetail ToDetail(Lesson lesson) =>
        new(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ContentMarkdown,
            RenderMarkdown(lesson.ContentMarkdown),
            lesson.Status,
            lesson.SortOrder,
            lesson.CreatedAtUtc,
            lesson.UpdatedAtUtc);
}
