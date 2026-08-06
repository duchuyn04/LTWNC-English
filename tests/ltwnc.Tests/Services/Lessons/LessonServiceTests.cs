using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Lessons;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Lessons;

public sealed class LessonServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveAsync_Create_DefaultsDraftAndMaxPlusOneSortOrder()
    {
        await using AppDbContext db = CreateContext();
        await SeedLessonAsync(db, "Existing", LessonStatus.Published, sortOrder: 3);
        LessonService service = CreateService(db);

        LessonSaveResult result = await service.SaveAsync(new LessonSaveCommand(
            Id: null,
            Title: "  Present simple  ",
            Summary: "  Tóm tắt  ",
            ContentMarkdown: "# Hello\n\nBody",
            Status: LessonStatus.Draft,
            SortOrder: null,
            ActorUserId: "admin-1"));

        Assert.True(result.Succeeded);
        Lesson lesson = await db.Lessons.SingleAsync(row => row.Title == "Present simple");
        Assert.Equal(LessonStatus.Draft, lesson.Status);
        Assert.Equal(4, lesson.SortOrder);
        Assert.Equal("Tóm tắt", lesson.Summary);
        Assert.Equal(FixedNow.UtcDateTime, lesson.CreatedAtUtc);
        Assert.Equal("admin-1", lesson.CreatedByUserId);
    }

    [Fact]
    public async Task SaveAsync_Create_FirstLessonGetsSortOrderOne()
    {
        await using AppDbContext db = CreateContext();
        LessonService service = CreateService(db);

        LessonSaveResult result = await service.SaveAsync(ValidCreate());

        Assert.True(result.Succeeded);
        Lesson lesson = await db.Lessons.SingleAsync();
        Assert.Equal(1, lesson.SortOrder);
    }

    [Fact]
    public async Task SaveAsync_RejectsEmptyTitleAndContentAndInvalidStatus()
    {
        await using AppDbContext db = CreateContext();
        LessonService service = CreateService(db);

        Assert.False((await service.SaveAsync(ValidCreate() with { Title = "  " })).Succeeded);
        Assert.False((await service.SaveAsync(ValidCreate() with { ContentMarkdown = " \n " })).Succeeded);
        Assert.False((await service.SaveAsync(ValidCreate() with { Status = "Archived" })).Succeeded);
        Assert.Equal(0, await db.Lessons.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_Update_ChangesFieldsAndStatus()
    {
        await using AppDbContext db = CreateContext();
        Lesson seeded = await SeedLessonAsync(db, "Old", LessonStatus.Draft, sortOrder: 2);
        LessonService service = CreateService(db);

        LessonSaveResult result = await service.SaveAsync(new LessonSaveCommand(
            seeded.Id,
            "New title",
            "New summary",
            "## Updated",
            LessonStatus.Published,
            SortOrder: 9,
            ActorUserId: "admin-2"));

        Assert.True(result.Succeeded);
        await db.Entry(seeded).ReloadAsync();
        Assert.Equal("New title", seeded.Title);
        Assert.Equal("New summary", seeded.Summary);
        Assert.Equal("## Updated", seeded.ContentMarkdown);
        Assert.Equal(LessonStatus.Published, seeded.Status);
        Assert.Equal(9, seeded.SortOrder);
        Assert.Equal("admin-2", seeded.UpdatedByUserId);
        Assert.Equal(FixedNow.UtcDateTime, seeded.UpdatedAtUtc);
    }

    [Fact]
    public async Task ListPublishedAsync_HidesDraft_OrdersBySortThenId()
    {
        await using AppDbContext db = CreateContext();
        Lesson a = await SeedLessonAsync(db, "A", LessonStatus.Published, sortOrder: 2);
        await SeedLessonAsync(db, "Hidden", LessonStatus.Draft, sortOrder: 1);
        Lesson b = await SeedLessonAsync(db, "B", LessonStatus.Published, sortOrder: 2);
        Lesson c = await SeedLessonAsync(db, "C", LessonStatus.Published, sortOrder: 1);
        LessonService service = CreateService(db);

        IReadOnlyList<LessonListItem> list = await service.ListPublishedAsync();

        Assert.Equal(new[] { c.Id, a.Id, b.Id }, list.Select(item => item.Id).ToArray());
        Assert.All(list, item => Assert.Equal(LessonStatus.Published, item.Status));
    }

    [Fact]
    public async Task ListForAdminAsync_IncludesDraft()
    {
        await using AppDbContext db = CreateContext();
        await SeedLessonAsync(db, "Pub", LessonStatus.Published, sortOrder: 1);
        await SeedLessonAsync(db, "Dr", LessonStatus.Draft, sortOrder: 2);
        LessonService service = CreateService(db);

        IReadOnlyList<LessonListItem> list = await service.ListForAdminAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, item => item.Status == LessonStatus.Draft);
    }

    [Fact]
    public async Task GetPublishedAsync_ReturnsNullForDraftOrMissing()
    {
        await using AppDbContext db = CreateContext();
        Lesson draft = await SeedLessonAsync(db, "Draft", LessonStatus.Draft, sortOrder: 1);
        Lesson published = await SeedLessonAsync(db, "Pub", LessonStatus.Published, sortOrder: 2);
        LessonService service = CreateService(db);

        Assert.Null(await service.GetPublishedAsync(draft.Id));
        Assert.Null(await service.GetPublishedAsync(9999));
        LessonDetail? detail = await service.GetPublishedAsync(published.Id);
        Assert.NotNull(detail);
        Assert.Equal("Pub", detail!.Title);
        Assert.Contains("<p>", detail.ContentHtml);
    }

    [Fact]
    public async Task GetForAdminAsync_ReturnsDraft()
    {
        await using AppDbContext db = CreateContext();
        Lesson draft = await SeedLessonAsync(db, "Draft", LessonStatus.Draft, sortOrder: 1);
        LessonService service = CreateService(db);

        LessonDetail? detail = await service.GetForAdminAsync(draft.Id);
        Assert.NotNull(detail);
        Assert.Equal(LessonStatus.Draft, detail!.Status);
    }

    [Fact]
    public void RenderMarkdown_ProducesHeadingAndStripsRawScript()
    {
        LessonService service = CreateService(CreateContext());

        string html = service.RenderMarkdown("# Title\n\nHello **world**\n\n<script>alert(1)</script>");

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>world</strong>", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMcqQuestionAsync_PersistsAndListsWithCount()
    {
        await using AppDbContext db = CreateContext();
        Lesson lesson = await SeedLessonAsync(db, "Pub", LessonStatus.Published, 1);
        LessonService service = CreateService(db);

        LessonQuestionMutationResult result = await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            lesson.Id,
            "Câu nào đúng?",
            ["A wrong", "B right", "C wrong"],
            CorrectOptionIndex: 1));

        Assert.True(result.Succeeded);
        LessonQuestion question = await db.LessonQuestions.SingleAsync();
        Assert.Equal(LessonQuestionTypes.MultipleChoice, question.Type);
        Assert.Equal(1, question.SortOrder);
        Assert.Equal(1, question.CorrectOptionIndex);

        IReadOnlyList<LessonQuestionAdminItem> adminList =
            await service.ListQuestionsForAdminAsync(lesson.Id);
        Assert.Single(adminList);
        Assert.Equal(3, adminList[0].Options.Count);

        LessonDetail? detail = await service.GetPublishedAsync(lesson.Id);
        Assert.Equal(1, detail!.QuestionCount);

        IReadOnlyList<LessonListItem> published = await service.ListPublishedAsync();
        Assert.Equal(1, published[0].QuestionCount);
    }

    [Fact]
    public async Task AddMcqQuestionAsync_RejectsInvalidOptionsAndIndex()
    {
        await using AppDbContext db = CreateContext();
        Lesson lesson = await SeedLessonAsync(db, "Pub", LessonStatus.Published, 1);
        LessonService service = CreateService(db);

        Assert.False((await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            lesson.Id, "Q", ["only-one"], 0))).Succeeded);

        Assert.False((await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            lesson.Id, "Q", ["a", "b"], CorrectOptionIndex: 5))).Succeeded);

        Assert.False((await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            lesson.Id, "  ", ["a", "b"], 0))).Succeeded);

        Assert.Equal(0, await db.LessonQuestions.CountAsync());
    }

    [Fact]
    public async Task DeleteQuestionAsync_RemovesRow()
    {
        await using AppDbContext db = CreateContext();
        Lesson lesson = await SeedLessonAsync(db, "Pub", LessonStatus.Published, 1);
        LessonService service = CreateService(db);
        LessonQuestionMutationResult added = await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            lesson.Id, "Q", ["a", "b"], 0));

        LessonQuestionMutationResult deleted =
            await service.DeleteQuestionAsync(lesson.Id, added.QuestionId!.Value);

        Assert.True(deleted.Succeeded);
        Assert.Equal(0, await db.LessonQuestions.CountAsync());
    }

    [Fact]
    public async Task GetPracticeBundleAsync_NullForDraftOrNoQuestions_HidesAnswer()
    {
        await using AppDbContext db = CreateContext();
        Lesson draft = await SeedLessonAsync(db, "Draft", LessonStatus.Draft, 1);
        Lesson published = await SeedLessonAsync(db, "Pub", LessonStatus.Published, 2);
        LessonService service = CreateService(db);

        await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            draft.Id, "Draft Q", ["a", "b"], 1));
        await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            published.Id, "Pub Q", ["a", "b"], 1));

        Assert.Null(await service.GetPracticeBundleAsync(draft.Id));
        Assert.Null(await service.GetPracticeBundleAsync(published.Id + 99));

        // empty published
        Lesson empty = await SeedLessonAsync(db, "Empty", LessonStatus.Published, 3);
        Assert.Null(await service.GetPracticeBundleAsync(empty.Id));

        PracticeBundle? bundle = await service.GetPracticeBundleAsync(published.Id);
        Assert.NotNull(bundle);
        Assert.Single(bundle!.Questions);
        Assert.Equal(["a", "b"], bundle.Questions[0].Options);
        // PracticeQuestionItem has no correct index field — grade separately
    }

    [Fact]
    public async Task GradeMcqAsync_ScoresCorrectAndWrong_BlocksDraftForLearner()
    {
        await using AppDbContext db = CreateContext();
        Lesson draft = await SeedLessonAsync(db, "Draft", LessonStatus.Draft, 1);
        Lesson published = await SeedLessonAsync(db, "Pub", LessonStatus.Published, 2);
        LessonService service = CreateService(db);

        int draftQ = (await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            draft.Id, "D", ["a", "b"], 1))).QuestionId!.Value;
        int pubQ = (await service.AddMcqQuestionAsync(new AddMcqQuestionCommand(
            published.Id, "P", ["a", "b"], 1))).QuestionId!.Value;

        GradeMcqResult draftGrade = await service.GradeMcqAsync(draft.Id, draftQ, 1, publishedOnly: true);
        Assert.False(draftGrade.Succeeded);

        GradeMcqResult wrong = await service.GradeMcqAsync(published.Id, pubQ, 0);
        Assert.True(wrong.Succeeded);
        Assert.False(wrong.IsCorrect);
        Assert.Equal(1, wrong.CorrectOptionIndex);

        GradeMcqResult right = await service.GradeMcqAsync(published.Id, pubQ, 1);
        Assert.True(right.Succeeded);
        Assert.True(right.IsCorrect);
    }

    private static LessonSaveCommand ValidCreate() =>
        new(null, "Title", null, "Content body", LessonStatus.Draft, null, "admin-1");

    private static LessonService CreateService(AppDbContext db) =>
        new(db, new FixedTimeProvider(FixedNow));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Lesson> SeedLessonAsync(
        AppDbContext db,
        string title,
        string status,
        int sortOrder)
    {
        Lesson lesson = new()
        {
            Title = title,
            Summary = null,
            ContentMarkdown = $"Content for {title}",
            Status = status,
            SortOrder = sortOrder,
            CreatedAtUtc = FixedNow.AddDays(-1).UtcDateTime,
            UpdatedAtUtc = FixedNow.AddDays(-1).UtcDateTime
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
