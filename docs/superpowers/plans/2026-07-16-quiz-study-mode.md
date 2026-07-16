# Quiz Study Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng chế độ Trắc nghiệm lưu phiên phía máy chủ trong Study, gồm câu hỏi trộn hai chiều, bốn lựa chọn, chấm ngay, kết quả và làm lại câu sai.

**Architecture:** `QuizModeStrategy` dùng bộ lọc Study hiện có và một `QuizQuestionFactory` chuyên dựng đề/đáp án; `QuizService` sở hữu vòng đời phiên, chấm điểm và retry. `StudySession` lưu phiên tổng quát, `QuizSessionQuestion` lưu snapshot từng câu; controller chỉ map HTTP sang service và Razor/JavaScript không nhận đáp án đúng trước khi người dùng trả lời.

**Tech Stack:** ASP.NET Core MVC trên .NET 10, C# 14, Entity Framework Core 10/SQL Server, Razor Views, vanilla JavaScript, CSS, xUnit, Moq, EF InMemory và SQLite.

## Global Constraints

- Quiz yêu cầu đăng nhập và chỉ học bộ thẻ thuộc người dùng hiện tại.
- Câu hỏi dùng toàn bộ thẻ phù hợp `StarredOnly`/`UnlearnedOnly`; mỗi thẻ xuất hiện đúng một lần.
- Hai chiều Anh → Việt và Việt → Anh chênh nhau tối đa một câu.
- Mỗi câu có đúng bốn lựa chọn phân biệt sau khi trim và so sánh không phân biệt hoa thường.
- Distractor ưu tiên cùng bộ; nếu thiếu mới dùng các bộ khác có cùng `UserId`; không dùng bộ của user khác.
- Client không nhận đáp án đúng trước khi trả lời và không gửi điểm cuối phiên.
- Quiz không tạo hoặc cập nhật `UserProgress`.
- Câu trả lời đã lưu không được đổi; cùng lựa chọn là idempotent, lựa chọn khác trả `409`.
- Hoàn thành phiên và phát `StudySessionCompletedEvent` đúng một lần.
- Không sửa hoặc stage thay đổi hiện có của người dùng trong `appsettings.json`.

## File Map

### Tạo mới

- `Models/Entities/QuizSessionQuestion.cs` — enum chiều hỏi và entity snapshot từng câu.
- `Models/ViewModels/Study/QuizStudyViewModel.cs` — model màn làm một câu.
- `Models/ViewModels/Study/QuizResultViewModel.cs` — model tổng kết và câu sai.
- `Services/Study/IQuizService.cs` — contract vòng đời Quiz.
- `Services/Study/QuizModels.cs` — DTO nội bộ và exception nghiệp vụ Quiz.
- `Services/Study/QuizQuestionFactory.cs` — pool đáp án, cân bằng direction, tạo bốn lựa chọn.
- `Services/Study/QuizService.cs` — tạo/resume, chấm, complete, result và retry.
- `Services/StudyModes/QuizModeStrategy.cs` — mode option và tập câu theo bộ lọc.
- `Views/Study/Quiz.cshtml` — màn câu hỏi.
- `Views/Study/QuizResult.cshtml` — màn kết quả.
- `wwwroot/css/quiz.css` — style riêng của Quiz.
- `wwwroot/js/quiz.js` — POST đáp án và cập nhật feedback.
- `tests/ltwnc.Tests/Services/Study/QuizQuestionFactoryTests.cs` — invariant tạo đề.
- `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs` — vòng đời/chấm/retry/security.
- `tests/ltwnc.Tests/Services/StudyModes/QuizModeStrategyTests.cs` — option/filter/availability.
- `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs` — HTTP mapping.
- `tests/ltwnc.Tests/Views/QuizViewTests.cs` — contract markup/script.
- `Migrations/*_AddQuizSessions.cs` và `Migrations/*_AddQuizSessions.Designer.cs` — migration EF-generated.

### Chỉnh sửa

- `Services/StudyModes/IStudyModeStrategy.cs` — thêm option builder bất đồng bộ có `userId`.
- `Services/Study/StudyService.cs` — await option builder mới.
- `Data/AppDbContext.cs` — `DbSet` và cấu hình Quiz FK/index.
- `Migrations/AppDbContextModelSnapshot.cs` — snapshot do EF cập nhật.
- `Program.cs` — đăng ký factory, Quiz service và Quiz strategy.
- `Controllers/StudyController.cs` — inject `IQuizService` và thêm route Quiz.
- `tests/ltwnc.Tests/Controllers/StudyControllerIndexTests.cs` — bổ sung dependency constructor.
- `tests/ltwnc.Tests/Controllers/StudyControllerDictationTests.cs` — bổ sung dependency constructor.
- `tests/ltwnc.Tests/Services/Study/StudyServiceTests.cs` — xác nhận async option và Quiz rời roadmap.
- `README.md` — ghi nhận mode Trắc nghiệm.

---

### Task 1: Cho phép Study strategy xây option bất đồng bộ

**Files:**

- Modify: `Services/StudyModes/IStudyModeStrategy.cs`
- Modify: `Services/Study/StudyService.cs`
- Modify: `tests/ltwnc.Tests/Services/Study/StudyServiceTests.cs`

**Interfaces:**

- Consumes: `BuildOption(int, IReadOnlyList<Flashcard>, UserStudySettings)` hiện có.
- Produces: `Task<StudyModeOptionViewModel> BuildOptionAsync(int setId, IReadOnlyList<Flashcard> cards, UserStudySettings settings, string? userId)`; Task 3 override method này để query availability của Quiz.

- [ ] **Step 1: Viết test thất bại chứng minh `StudyService` dùng async option**

Trong `StudyServiceTests.cs`, thêm fake strategy có `BuildOption` trả tên `"SYNC"` và `BuildOptionAsync` trả tên `"ASYNC"`, rồi assert mode trên hub có tên `"ASYNC"`:

```csharp
private sealed class AsyncOptionStrategy : IStudyModeStrategy
{
    public StudyMode Mode => StudyMode.Quiz;

    public Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId) => Task.FromResult(new List<Flashcard>());

    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings) => new() { Mode = Mode, Name = "SYNC" };

    public Task<StudyModeOptionViewModel> BuildOptionAsync(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings,
        string? userId) => Task.FromResult(new StudyModeOptionViewModel
        {
            Mode = Mode,
            Name = "ASYNC"
        });
}
```

Test thêm strategy vào danh sách, gọi `GetStudyModeSelectorDataAsync`, rồi `Assert.Equal("ASYNC", result.Modes.Single(m => m.Mode == StudyMode.Quiz).Name)`.

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~StudyServiceTests.GetStudyModeSelectorDataAsync_UsesAsyncOptionBuilder"
```

Expected: FAIL vì `IStudyModeStrategy` chưa có `BuildOptionAsync` hoặc service vẫn trả `"SYNC"`.

- [ ] **Step 3: Thêm default async contract và đổi call site**

Thêm vào `IStudyModeStrategy` để giữ tương thích cho Flashcard, Dictation và các fake hiện có:

```csharp
Task<StudyModeOptionViewModel> BuildOptionAsync(
    int setId,
    IReadOnlyList<Flashcard> cards,
    UserStudySettings settings,
    string? userId)
{
    return Task.FromResult(BuildOption(setId, cards, settings));
}
```

Trong vòng lặp `StudyService.GetStudyModeSelectorDataAsync`, thay call sync bằng:

```csharp
StudyModeOptionViewModel option = await strategy.BuildOptionAsync(
    setId,
    cardsForMode,
    settings,
    userId);
modes.Add(option);
```

- [ ] **Step 4: Chạy test mục tiêu và nhóm StudyModes**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~StudyServiceTests|FullyQualifiedName~StudyModes"
```

Expected: PASS; Flashcard/Dictation vẫn dùng default implementation.

- [ ] **Step 5: Commit**

```powershell
git add Services/StudyModes/IStudyModeStrategy.cs Services/Study/StudyService.cs tests/ltwnc.Tests/Services/Study/StudyServiceTests.cs
git commit -m "refactor(study): support async mode options"
```

---

### Task 2: Thêm schema lưu snapshot câu hỏi Quiz

**Files:**

- Create: `Models/Entities/QuizSessionQuestion.cs`
- Modify: `Data/AppDbContext.cs`
- Create: `Migrations/*_AddQuizSessions.cs`
- Create: `Migrations/*_AddQuizSessions.Designer.cs`
- Modify: `Migrations/AppDbContextModelSnapshot.cs`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`

**Interfaces:**

- Consumes: `StudySession.Id`, `Flashcard.Id`.
- Produces: `QuizSessionQuestion`, `QuizQuestionDirection`, `AppDbContext.QuizSessionQuestions` cho Tasks 3–5.

- [ ] **Step 1: Viết SQLite test thất bại cho unique constraints**

Tạo `QuizServiceTests.cs` với helper mở SQLite in-memory, gọi `Database.EnsureCreatedAsync()`, thêm một session và hai question trùng `(StudySessionId, OrderIndex)`. Assert `SaveChangesAsync` ném `DbUpdateException`. Viết test thứ hai cho trùng `(StudySessionId, FlashcardId)`.

```csharp
await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
```

- [ ] **Step 2: Chạy test để xác nhận thất bại biên dịch**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests.Schema_"
```

Expected: FAIL vì `QuizSessionQuestion` và `DbSet` chưa tồn tại.

- [ ] **Step 3: Tạo entity với dữ liệu snapshot cố định**

Tạo `Models/Entities/QuizSessionQuestion.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public enum QuizQuestionDirection
{
    TermToDefinition,
    DefinitionToTerm
}

public class QuizSessionQuestion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StudySessionId { get; set; }

    [Required]
    public int FlashcardId { get; set; }

    public int OrderIndex { get; set; }
    public QuizQuestionDirection Direction { get; set; }

    [Required]
    public string PromptText { get; set; } = string.Empty;

    [Required]
    public string Choice1Text { get; set; } = string.Empty;

    [Required]
    public string Choice2Text { get; set; } = string.Empty;

    [Required]
    public string Choice3Text { get; set; } = string.Empty;

    [Required]
    public string Choice4Text { get; set; } = string.Empty;

    public int CorrectChoiceIndex { get; set; }
    public int? SelectedChoiceIndex { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime? AnsweredAt { get; set; }

    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }

    [NotMapped]
    public IReadOnlyList<string> Choices =>
        new[] { Choice1Text, Choice2Text, Choice3Text, Choice4Text };
}
```

- [ ] **Step 4: Cấu hình DbContext và migration**

Thêm:

```csharp
public DbSet<QuizSessionQuestion> QuizSessionQuestions => Set<QuizSessionQuestion>();
```

Trong `OnModelCreating`:

```csharp
builder.Entity<QuizSessionQuestion>(entity =>
{
    entity.HasIndex(e => e.StudySessionId);
    entity.HasIndex(e => new { e.StudySessionId, e.OrderIndex }).IsUnique();
    entity.HasIndex(e => new { e.StudySessionId, e.FlashcardId }).IsUnique();

    entity.HasOne(e => e.StudySession)
        .WithMany()
        .HasForeignKey(e => e.StudySessionId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Flashcard)
        .WithMany()
        .HasForeignKey(e => e.FlashcardId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

Generate migration:

```powershell
dotnet ef migrations add AddQuizSessions --project ltwnc.csproj --output-dir Migrations
```

Expected: EF tạo migration với bảng `QuizSessionQuestions`, hai unique index và hai foreign key; snapshot được cập nhật.

- [ ] **Step 5: Chạy schema tests và kiểm tra migration script**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests.Schema_"
dotnet ef migrations script --project ltwnc.csproj --idempotent --output .test-output/quiz-migration.sql
```

Expected: tests PASS; script chứa `CREATE TABLE [QuizSessionQuestions]` và unique indexes.

- [ ] **Step 6: Commit**

```powershell
git add Models/Entities/QuizSessionQuestion.cs Data/AppDbContext.cs Migrations tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs
git commit -m "feat(quiz): add persisted question schema"
```

---

### Task 3: Dựng pool đáp án và kích hoạt Quiz trên Study Hub

**Files:**

- Create: `Services/Study/QuizQuestionFactory.cs`
- Create: `Services/Study/QuizModels.cs`
- Create: `Services/StudyModes/QuizModeStrategy.cs`
- Modify: `Program.cs`
- Create: `tests/ltwnc.Tests/Services/Study/QuizQuestionFactoryTests.cs`
- Create: `tests/ltwnc.Tests/Services/StudyModes/QuizModeStrategyTests.cs`
- Modify: `tests/ltwnc.Tests/Services/Study/StudyServiceTests.cs`

**Interfaces:**

- Consumes: `QuizSessionQuestion`, `QuizQuestionDirection`, `IStudyCardQueryService`, async option contract Task 1.
- Produces:
  - `QuizUnavailableException`
  - `Task<QuizPoolAvailability> GetAvailabilityAsync(int setId, string userId)`
  - `Task<List<QuizSessionQuestion>> BuildQuestionsAsync(int setId, string userId, IReadOnlyList<Flashcard> sourceCards, IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections = null)`
  - `QuizModeStrategy` đăng ký dưới `IStudyModeStrategy`.

- [ ] **Step 1: Viết factory tests thất bại theo invariant**

Seed hai set của `user-1` và một set của `user-2`. Thêm các test có tên cụ thể:

```csharp
[Fact] public async Task BuildQuestions_creates_one_question_per_source_card() { }
[Fact] public async Task BuildQuestions_balances_directions_with_difference_at_most_one() { }
[Fact] public async Task BuildQuestions_creates_four_distinct_choices_with_one_correct_answer() { }
[Fact] public async Task BuildQuestions_prefers_same_set_distractors() { }
[Fact] public async Task BuildQuestions_falls_back_to_owned_sets_only() { }
[Fact] public async Task GetAvailability_requires_four_distinct_terms_and_definitions() { }
```

Mỗi test phải assert dữ liệu cụ thể, ví dụ:

```csharp
Assert.All(questions, question =>
{
    Assert.Equal(4, question.Choices.Count);
    Assert.Equal(4, question.Choices.Select(Normalize).Distinct().Count());
    Assert.InRange(question.CorrectChoiceIndex, 0, 3);
});
Assert.True(Math.Abs(termToDefinition - definitionToTerm) <= 1);
```

- [ ] **Step 2: Chạy factory tests để xác nhận thất bại**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizQuestionFactoryTests"
```

Expected: FAIL vì factory chưa tồn tại.

- [ ] **Step 3: Tạo exception dùng chung và `QuizQuestionFactory`**

Tạo `Services/Study/QuizModels.cs` trước để Task 3–6 dùng cùng một exception:

```csharp
namespace ltwnc.Services.Study;

public sealed class QuizUnavailableException : InvalidOperationException
{
    public QuizUnavailableException(string message) : base(message) { }
}
```

Định nghĩa DTO và signatures ở đầu file:

```csharp
public sealed record QuizPoolAvailability(
    bool IsAvailable,
    int DistinctTermCount,
    int DistinctDefinitionCount,
    string? UnavailableReason);

public class QuizQuestionFactory
{
    private readonly AppDbContext _context;

    public QuizQuestionFactory(AppDbContext context)
    {
        _context = context;
    }

    public Task<QuizPoolAvailability> GetAvailabilityAsync(int setId, string userId);

    public Task<List<QuizSessionQuestion>> BuildQuestionsAsync(
        int setId,
        string userId,
        IReadOnlyList<Flashcard> sourceCards,
        IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections = null);
}
```

Implementation rules:

- Query same-set cards first; query other cards only through `FlashcardSet.UserId == userId`.
- `NormalizeChoice` là `value.Trim().ToUpperInvariant()`.
- Availability cần ít nhất bốn normalized `FrontText` và bốn normalized `BackText`.
- Tạo direction list có `count / 2` mỗi loại và một direction ngẫu nhiên cho số lẻ, rồi Fisher–Yates.
- Với mỗi câu, lấy ba distractor distinct từ same-set pool; chỉ bù phần thiếu từ owned-other pool.
- Thêm correct answer, Fisher–Yates bốn lựa chọn, ghi bốn cột và `CorrectChoiceIndex`.
- Khi `fixedDirections` có card id, bắt buộc dùng direction đó.
- Nếu bất kỳ câu nào không đủ lựa chọn, ném `QuizUnavailableException` và không trả danh sách một phần.

- [ ] **Step 4: Viết strategy tests thất bại**

Trong `QuizModeStrategyTests.cs`, test:

```csharp
[Fact] public async Task GetCardsAsync_applies_study_filters() { }
[Fact] public async Task BuildOptionAsync_is_available_with_valid_pool() { }
[Fact] public async Task BuildOptionAsync_explains_empty_filtered_questions() { }
[Fact] public async Task BuildOptionAsync_explains_insufficient_library_pool() { }
```

Assert metadata chính xác: `StudyMode.Quiz`, `"Trắc nghiệm"`, `"/Study/1/Quiz"`, `ph-question`, `EstimatedSeconds = cards.Count * 30`.

- [ ] **Step 5: Implement `QuizModeStrategy` và DI**

Constructor và method contract:

```csharp
public class QuizModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;
    private readonly QuizQuestionFactory _questionFactory;

    public QuizModeStrategy(
        IStudyCardQueryService queryService,
        QuizQuestionFactory questionFactory)
    {
        _queryService = queryService;
        _questionFactory = questionFactory;
    }

    public StudyMode Mode => StudyMode.Quiz;

    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        return await _queryService.CreateFilteredQuery(setId, settings, userId)
            .OrderBy(card => card.OrderIndex)
            .ToListAsync();
    }
}
```

`BuildOption` chỉ tạo metadata/số câu cơ bản; `BuildOptionAsync` gọi availability khi `userId` không null và gán `IsAvailable`/`UnavailableReason` chính xác. Không đánh dấu Quiz recommended.

Đăng ký trong `Program.cs`:

```csharp
builder.Services.AddScoped<QuizQuestionFactory>();
builder.Services.AddScoped<IStudyModeStrategy, QuizModeStrategy>();
```

- [ ] **Step 6: Xác nhận Quiz rời roadmap và các tests pass**

Thêm `QuizModeStrategy` vào strategy list của test Study Hub, rồi assert:

```csharp
Assert.Contains(result.Modes, option => option.Mode == StudyMode.Quiz);
Assert.DoesNotContain(result.RoadmapModes, option => option.Mode == StudyMode.Quiz);
```

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizQuestionFactoryTests|FullyQualifiedName~QuizModeStrategyTests|FullyQualifiedName~StudyServiceTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add Services/Study/QuizModels.cs Services/Study/QuizQuestionFactory.cs Services/StudyModes/QuizModeStrategy.cs Program.cs tests/ltwnc.Tests/Services/Study/QuizQuestionFactoryTests.cs tests/ltwnc.Tests/Services/StudyModes/QuizModeStrategyTests.cs tests/ltwnc.Tests/Services/Study/StudyServiceTests.cs
git commit -m "feat(quiz): build questions and activate study mode"
```

---

### Task 4: Tạo/resume phiên, hiển thị câu hiện tại và chấm nguyên tử

**Files:**

- Modify: `Services/Study/QuizModels.cs`
- Create: `Services/Study/IQuizService.cs`
- Create: `Services/Study/QuizService.cs`
- Modify: `Program.cs`
- Modify: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`

**Interfaces:**

- Consumes: `QuizQuestionFactory.BuildQuestionsAsync`, `IStudyModeStrategyResolver.Resolve(StudyMode.Quiz)`, `IStudyEventPublisher`.
- Produces:

```csharp
Task<StudySession> StartOrResumeAsync(
    int setId,
    string userId,
    UserStudySettings settings);

Task<QuizQuestionState> GetCurrentQuestionAsync(
    int setId,
    int sessionId,
    string userId);

Task<QuizAnswerResult> AnswerAsync(
    int setId,
    int sessionId,
    int questionId,
    int selectedChoiceIndex,
    string userId);
```

- [ ] **Step 1: Viết lifecycle/security tests thất bại**

Thêm các tests SQLite:

```csharp
[Fact] public async Task StartOrResume_creates_session_and_persisted_questions() { }
[Fact] public async Task StartOrResume_returns_existing_incomplete_session() { }
[Fact] public async Task GetCurrentQuestion_returns_first_unanswered_with_session_counts() { }
[Fact] public async Task Answer_saves_correct_result_and_does_not_create_progress() { }
[Fact] public async Task Answer_same_choice_is_idempotent() { }
[Fact] public async Task Answer_different_choice_throws_conflict() { }
[Fact] public async Task Answer_rejects_question_from_another_session() { }
[Fact] public async Task Answer_rejects_another_user() { }
[Fact] public async Task Last_answer_calculates_score_and_publishes_once() { }
```

Dùng một recording `IStudyEventPublisher` trong test để đếm `StudySessionCompletedEvent` và assert `Mode == StudyMode.Quiz`.

- [ ] **Step 2: Chạy lifecycle tests để xác nhận thất bại**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests.StartOrResume|FullyQualifiedName~QuizServiceTests.GetCurrentQuestion|FullyQualifiedName~QuizServiceTests.Answer|FullyQualifiedName~QuizServiceTests.Last_answer"
```

Expected: FAIL vì service/contracts chưa tồn tại.

- [ ] **Step 3: Tạo DTO và exception nghiệp vụ**

Trong `QuizModels.cs`:

```csharp
public sealed class QuizQuestionState
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int AnsweredCount { get; init; }
    public int CorrectCount { get; init; }
    public QuizSessionQuestion? Question { get; init; }
    public bool IsComplete => Question is null;
}

public sealed record QuizAnswerResult(
    bool IsCorrect,
    int CorrectChoiceIndex,
    bool IsLastQuestion);

public sealed class QuizConflictException : InvalidOperationException
{
    public QuizConflictException(string message) : base(message) { }
}

public sealed class QuizSessionResult
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int CorrectCount { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<QuizWrongAnswer> WrongAnswers { get; init; } =
        Array.Empty<QuizWrongAnswer>();
}

public sealed record QuizWrongAnswer(
    int FlashcardId,
    QuizQuestionDirection Direction,
    string PromptText,
    string SelectedAnswer,
    string CorrectAnswer);
```

- [ ] **Step 4: Tạo `IQuizService` với toàn bộ contract vòng đời**

Ngoài ba method trên, khai báo sẵn:

```csharp
Task<QuizSessionResult> GetResultAsync(int setId, int sessionId, string userId);
Task<StudySession> RetryWrongAsync(int setId, int sessionId, string userId);
Task<StudySession> RetryAllAsync(int setId, int sessionId, string userId);
```

Mọi return type đã được định nghĩa đầy đủ trong `QuizModels.cs`, nên interface compile ngay khi được thêm.

- [ ] **Step 5: Implement create/resume/current**

`QuizService` constructor:

```csharp
public QuizService(
    AppDbContext context,
    IStudyModeStrategyResolver strategyResolver,
    QuizQuestionFactory questionFactory,
    IStudyEventPublisher studyEvents)
```

`StartOrResumeAsync` phải:

- Query set; ném `KeyNotFoundException` nếu thiếu và `UnauthorizedAccessException` nếu `set.UserId != userId`.
- Tìm session `Mode == Quiz`, `Score == null`, đúng set/user và có câu chưa trả lời.
- Resolve Quiz strategy, lấy source cards theo settings.
- Ném `QuizUnavailableException` nếu source rỗng.
- Tạo `StudySession`, gọi factory, gán `StudySession` navigation cho questions và lưu trong một transaction relational.

`GetCurrentQuestionAsync` load đúng session/user/set, đếm tổng/đã trả lời/đúng và trả question có `SelectedChoiceIndex == null` nhỏ nhất.

- [ ] **Step 6: Implement answer với conditional update**

Validation index:

```csharp
if (selectedChoiceIndex is < 0 or > 3)
{
    throw new ArgumentOutOfRangeException(nameof(selectedChoiceIndex));
}
```

Sau ownership checks, nếu đã trả lời thì same index trả stored result, khác index ném `QuizConflictException`.

Với relational provider, update có điều kiện để hai request không cùng thắng:

```csharp
bool isCorrect = selectedChoiceIndex == question.CorrectChoiceIndex;
int affected = await _context.QuizSessionQuestions
    .Where(row => row.Id == questionId && row.SelectedChoiceIndex == null)
    .ExecuteUpdateAsync(updates => updates
        .SetProperty(row => row.SelectedChoiceIndex, selectedChoiceIndex)
        .SetProperty(row => row.IsCorrect, isCorrect)
        .SetProperty(row => row.AnsweredAt, DateTime.UtcNow));
```

Nếu `affected == 0`, reload và áp dụng quy tắc idempotent/conflict. Khi không còn unanswered, tính score từ DB và conditional-update `StudySessions` với predicate `Score == null`; chỉ request update được một row mới publish event.

Tính điểm bằng midpoint rounding rõ ràng:

```csharp
int score = (int)Math.Round(
    correctCount * 100.0 / totalCount,
    MidpointRounding.AwayFromZero);
```

- [ ] **Step 7: Đăng ký DI và chạy tests**

Trong `Program.cs`:

```csharp
builder.Services.AddScoped<IQuizService, QuizService>();
```

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests"
```

Expected: PASS; assert `context.UserProgresses` vẫn rỗng.

- [ ] **Step 8: Commit**

```powershell
git add Services/Study/QuizModels.cs Services/Study/IQuizService.cs Services/Study/QuizService.cs Program.cs tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs
git commit -m "feat(quiz): persist and grade quiz sessions"
```

---

### Task 5: Kết quả và tạo phiên làm lại

**Files:**

- Modify: `Services/Study/QuizService.cs`
- Modify: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`

**Interfaces:**

- Consumes: completed `StudySession`, persisted `QuizSessionQuestion` và các DTO từ Task 4.
- Produces: implementation hoàn chỉnh cho `GetResultAsync`, `RetryWrongAsync` và `RetryAllAsync`:

```csharp
public sealed class QuizSessionResult
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int CorrectCount { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<QuizWrongAnswer> WrongAnswers { get; init; } = Array.Empty<QuizWrongAnswer>();
}

public sealed record QuizWrongAnswer(
    int FlashcardId,
    QuizQuestionDirection Direction,
    string PromptText,
    string SelectedAnswer,
    string CorrectAnswer);
```

- [ ] **Step 1: Viết result/retry tests thất bại**

```csharp
[Fact] public async Task GetResult_returns_score_and_wrong_answer_snapshots() { }
[Fact] public async Task GetResult_rejects_incomplete_session() { }
[Fact] public async Task RetryWrong_contains_only_wrong_cards_and_preserves_directions() { }
[Fact] public async Task RetryWrong_with_no_wrong_answers_throws_conflict() { }
[Fact] public async Task RetryAll_preserves_original_card_scope_and_redistributes_directions() { }
[Fact] public async Task Retry_rolls_back_when_source_card_or_pool_is_unavailable() { }
[Fact] public async Task Retry_rejects_another_user() { }
```

Assert session cũ giữ nguyên `Score`, questions và answers sau retry.

- [ ] **Step 2: Chạy tests để xác nhận thất bại**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests.GetResult|FullyQualifiedName~QuizServiceTests.Retry"
```

Expected: FAIL vì methods chưa implement.

- [ ] **Step 3: Implement `GetResultAsync`**

- Load session `Mode == Quiz`, đúng set/user, `Score != null`; incomplete ném `QuizConflictException`.
- Load questions ordered by `OrderIndex`.
- Map wrong answer bằng `question.Choices[question.SelectedChoiceIndex.Value]` và `question.Choices[question.CorrectChoiceIndex]`.
- Không đọc nội dung hiện tại của Flashcard để tránh thay đổi snapshot lịch sử.

- [ ] **Step 4: Implement retry methods qua một helper dùng chung**

Helper private:

```csharp
private Task<StudySession> CreateRetrySessionAsync(
    StudySession sourceSession,
    IReadOnlyList<QuizSessionQuestion> sourceQuestions,
    bool preserveDirections);
```

`RetryWrongAsync` truyền questions `IsCorrect == false`, `preserveDirections: true`. `RetryAllAsync` truyền toàn bộ questions, `preserveDirections: false`. Helper:

- Load toàn bộ source cards; thiếu một card ném `QuizUnavailableException`.
- Với preserve, tạo dictionary `FlashcardId -> Direction`; nếu không preserve truyền null.
- Tạo session/questions mới bằng factory trong transaction.
- Không sửa hoặc xóa session/questions nguồn.

- [ ] **Step 5: Chạy toàn bộ Quiz service tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizServiceTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add Services/Study/QuizModels.cs Services/Study/QuizService.cs tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs
git commit -m "feat(quiz): add results and retry sessions"
```

---

### Task 6: Thêm routes và HTTP mapping trong `StudyController`

**Files:**

- Create: `Models/ViewModels/Study/QuizStudyViewModel.cs`
- Create: `Models/ViewModels/Study/QuizResultViewModel.cs`
- Modify: `Controllers/StudyController.cs`
- Create: `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs`
- Modify: `tests/ltwnc.Tests/Controllers/StudyControllerIndexTests.cs`
- Modify: `tests/ltwnc.Tests/Controllers/StudyControllerDictationTests.cs`

**Interfaces:**

- Consumes: toàn bộ `IQuizService` Task 4–5.
- Produces routes:
  - `GET /Study/{setId}/Quiz`
  - `GET /Study/{setId}/Quiz/{sessionId:int}`
  - `POST /Study/{setId}/Quiz/{sessionId:int}/Answer`
  - `GET /Study/{setId}/Quiz/Result/{sessionId:int}`
  - `POST /Study/{setId}/Quiz/{sessionId:int}/RetryWrong`
  - `POST /Study/{setId}/Quiz/{sessionId:int}/RetryAll`

- [ ] **Step 1: Viết controller tests thất bại với mock `IQuizService`**

Test cụ thể:

```csharp
[Fact] public async Task QuizStart_redirects_to_created_or_resumed_session() { }
[Fact] public async Task QuizStart_unavailable_redirects_to_hub_with_message() { }
[Fact] public async Task Quiz_returns_view_without_correct_answer() { }
[Fact] public async Task Quiz_completed_redirects_to_result() { }
[Fact] public async Task QuizAnswer_maps_success_json() { }
[Theory]
[InlineData(typeof(ArgumentOutOfRangeException), 400)]
[InlineData(typeof(UnauthorizedAccessException), 403)]
[InlineData(typeof(KeyNotFoundException), 404)]
[InlineData(typeof(QuizConflictException), 409)]
public async Task QuizAnswer_maps_domain_errors(Type exceptionType, int statusCode) { }
[Fact] public async Task QuizResult_returns_result_view() { }
[Fact] public async Task RetryWrong_redirects_to_new_session() { }
[Fact] public async Task RetryAll_redirects_to_new_session() { }
```

- [ ] **Step 2: Chạy controller tests để xác nhận thất bại**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~StudyControllerQuizTests"
```

Expected: FAIL vì view models/actions chưa tồn tại.

- [ ] **Step 3: Tạo view models không làm lộ đáp án**

`QuizStudyViewModel` chỉ chứa:

```csharp
public int SetId { get; set; }
public string SetTitle { get; set; } = string.Empty;
public int SessionId { get; set; }
public int QuestionId { get; set; }
public int CurrentNumber { get; set; }
public int TotalQuestions { get; set; }
public int CorrectCount { get; set; }
public QuizQuestionDirection Direction { get; set; }
public string PromptText { get; set; } = string.Empty;
public List<string> Choices { get; set; } = new();
```

Không thêm `CorrectChoiceIndex`, `IsCorrect` hoặc correct text vào model này.

`QuizResultViewModel` chứa set/session/score/count và `List<QuizWrongAnswerViewModel>` với direction, prompt, selected answer, correct answer.

- [ ] **Step 4: Inject service và cập nhật mọi constructor call site**

Đổi constructor `StudyController` thành:

```csharp
public StudyController(
    IStudyService studyService,
    IDictationService dictationService,
    IQuizService quizService,
    IFlashcardSetService setService,
    ICurrentUser currentUser)
```

Trong hai test class cũ, truyền `Mock.Of<IQuizService>()` ở đúng vị trí để giữ test Flashcard/Dictation compile.

- [ ] **Step 5: Implement start/question/answer/result/retry actions**

Quy tắc mapping:

- Null user → `Challenge()` cho GET, `Unauthorized()` cho AJAX POST.
- Start lấy settings từ `IStudyService`, gọi `StartOrResumeAsync`, catch unavailable để đặt `TempData["Message"]` và về Hub.
- Question map `QuizQuestionState`; nếu complete redirect Result.
- Answer trả JSON `{ success, isCorrect, correctChoiceIndex, isLastQuestion, nextUrl }`.
- Result map domain DTO sang `QuizResultViewModel`.
- Retry POST có `[ValidateAntiForgeryToken]`, redirect tới session mới.
- `KeyNotFoundException` → `NotFound()`, `UnauthorizedAccessException` → `Forbid()`, `ArgumentOutOfRangeException` → `BadRequest()`, `QuizConflictException` → `StatusCode(409, new { success = false, message = exception.Message })`.

- [ ] **Step 6: Chạy controller tests và các controller regression tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~StudyControllerQuizTests|FullyQualifiedName~StudyControllerIndexTests|FullyQualifiedName~StudyControllerDictationTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add Models/ViewModels/Study/QuizStudyViewModel.cs Models/ViewModels/Study/QuizResultViewModel.cs Controllers/StudyController.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs tests/ltwnc.Tests/Controllers/StudyControllerIndexTests.cs tests/ltwnc.Tests/Controllers/StudyControllerDictationTests.cs
git commit -m "feat(quiz): add study controller routes"
```

---

### Task 7: Xây màn làm bài, phản hồi và kết quả

**Files:**

- Create: `Views/Study/Quiz.cshtml`
- Create: `Views/Study/QuizResult.cshtml`
- Create: `wwwroot/css/quiz.css`
- Create: `wwwroot/js/quiz.js`
- Create: `tests/ltwnc.Tests/Views/QuizViewTests.cs`

**Interfaces:**

- Consumes: `QuizStudyViewModel`, `QuizResultViewModel` và JSON answer contract Task 6.
- Produces: UI có bốn lựa chọn, anti-forgery header, feedback đúng/sai, next/result navigation và retry forms.

- [ ] **Step 1: Viết markup/script tests thất bại**

`QuizViewTests` đọc file theo helper pattern của `FlashcardEditorScriptTests` và assert:

```csharp
Assert.Contains("data-quiz-answer", QuizView);
Assert.Contains("data-quiz-root", QuizView);
Assert.Contains("for (int index = 0; index < Model.Choices.Count; index++)", QuizView);
Assert.Contains("@Html.AntiForgeryToken()", QuizView);
Assert.Contains("aria-live=\"polite\"", QuizView);
Assert.Contains("RequestVerificationToken", QuizScript);
Assert.Contains("selectedChoiceIndex", QuizScript);
Assert.Contains("button.disabled = true", QuizScript);
Assert.Contains("correctChoiceIndex", QuizScript);
Assert.Contains("textContent", QuizScript);
Assert.DoesNotContain("innerHTML", QuizScript);
Assert.Contains("RetryWrong", ResultView);
Assert.Contains("RetryAll", ResultView);
```

Thêm assert result chỉ render RetryWrong khi `Model.WrongAnswers.Any()`.

- [ ] **Step 2: Chạy view tests để xác nhận thất bại**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizViewTests"
```

Expected: FAIL vì view/assets chưa tồn tại.

- [ ] **Step 3: Tạo `Quiz.cshtml`**

View phải:

- Import `quiz.css` trong `@section Styles` và `quiz.js` trong `@section Scripts`, đều `asp-append-version="true"`.
- Hiển thị `Câu @Model.CurrentNumber / @Model.TotalQuestions`, `@Model.CorrectCount` và direction label.
- Render đúng bốn `<button type="button" data-quiz-answer data-choice-index="@index">`.
- Root container dùng `data-quiz-root`, `data-question-id="@Model.QuestionId"` và `data-answer-url` từ `Url.Action`; `@Html.AntiForgeryToken()` cung cấp token.
- Feedback container dùng `role="status" aria-live="polite"`.
- Next link ban đầu hidden; JavaScript chỉ hiện sau response.
- Link thoát tới `Index` không hoàn thành hoặc sửa câu hiện tại.

- [ ] **Step 4: Tạo `quiz.js` không chấm phía client**

Flow chính:

```javascript
const root = document.querySelector('[data-quiz-root]');
const buttons = Array.from(root.querySelectorAll('[data-quiz-answer]'));
const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

buttons.forEach((button) => {
    button.addEventListener('click', async () => {
        buttons.forEach((item) => { item.disabled = true; });
        const body = new URLSearchParams({
            questionId: root.dataset.questionId,
            selectedChoiceIndex: button.dataset.choiceIndex
        });

        const response = await fetch(root.dataset.answerUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
                'RequestVerificationToken': token,
                'X-Requested-With': 'XMLHttpRequest'
            },
            credentials: 'same-origin',
            body: body.toString()
        });
    });
});
```

Sau JSON success: thêm class đúng vào index server trả, class sai vào lựa chọn user nếu cần, ghi feedback bằng `textContent`, giữ buttons disabled và hiện next link. Khi network/5xx: báo lỗi bằng `textContent` và enable lại buttons. Với `409`, báo câu đã được chấm và reload để tiếp tục.

- [ ] **Step 5: Tạo `QuizResult.cshtml` và `quiz.css`**

Result view:

- Score, `CorrectCount / TotalQuestions`.
- Loop wrong answers, render direction/prompt/selected/correct bằng Razor encoding.
- RetryWrong form chỉ khi có wrong answers.
- RetryAll form và Hub link luôn có.
- Mọi POST form dùng `@Html.AntiForgeryToken()`.

CSS phải có states `.quiz-choice.is-correct`, `.quiz-choice.is-wrong`, focus-visible, disabled không làm mất màu feedback, responsive một cột ở màn nhỏ và reduced-motion fallback.

- [ ] **Step 6: Chạy view tests và kiểm tra Razor build**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~QuizViewTests"
dotnet build ltwnc.csproj --no-restore
```

Expected: PASS và build 0 errors.

- [ ] **Step 7: Commit**

```powershell
git add Views/Study/Quiz.cshtml Views/Study/QuizResult.cshtml wwwroot/css/quiz.css wwwroot/js/quiz.js tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat(quiz): add question and result experience"
```

---

### Task 8: Cập nhật tài liệu và chạy verification toàn hệ thống

**Files:**

- Modify: `README.md`
- Verify only: toàn bộ project và test suite.

**Interfaces:**

- Consumes: deliverables Tasks 1–7.
- Produces: tài liệu người dùng và bằng chứng build/test/migration sạch.

- [ ] **Step 1: Cập nhật README bằng behavior thật**

Trong danh sách tính năng, thêm:

```markdown
- Trắc nghiệm 4 lựa chọn: trộn câu hỏi Anh–Việt/Việt–Anh, chấm ngay, lưu điểm và làm lại câu sai.
```

Trong phần Strategy, đổi mô tả “sau này có thể thêm Quiz” thành danh sách ba strategy thật: `FlashcardModeStrategy`, `DictationModeStrategy`, `QuizModeStrategy`; nêu Quiz kiểm tra pool đáp án qua async option builder.

- [ ] **Step 2: Kiểm tra không có placeholder và diff lỗi**

```powershell
rg -n "TBD|TODO|FIXME|implement later|fill in" Models/Entities/QuizSessionQuestion.cs Models/ViewModels/Study/Quiz*.cs Services/Study/Quiz*.cs Services/StudyModes/QuizModeStrategy.cs Views/Study/Quiz*.cshtml wwwroot/js/quiz.js wwwroot/css/quiz.css README.md
rg -n "TBD|TODO|FIXME|implement later|fill in" tests/ltwnc.Tests -g 'Quiz*'
git diff --check
```

Expected: không có placeholder mới trong file Quiz; `git diff --check` không có output. Không sửa warning nằm ngoài phạm vi nếu có từ thay đổi của user.

- [ ] **Step 3: Chạy toàn bộ test suite**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
```

Expected: tất cả tests PASS, 0 failed.

- [ ] **Step 4: Build production project**

```powershell
dotnet build ltwnc.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Verify migration state**

```powershell
dotnet ef migrations list --project ltwnc.csproj
dotnet ef migrations script --project ltwnc.csproj --idempotent --output .test-output/quiz-final.sql
Select-String -Path .test-output/quiz-final.sql -Pattern "QuizSessionQuestions"
```

Expected: `AddQuizSessions` là migration cuối; script chứa create table/index/FK của Quiz.

- [ ] **Step 6: Kiểm tra phạm vi git và commit docs**

```powershell
git status --short
git diff --stat
git add README.md
git commit -m "docs: document quiz study mode"
```

Expected: `appsettings.json` vẫn unstaged nếu còn thay đổi của người dùng; commit chỉ chứa README.

- [ ] **Step 7: Review cuối trước khi bàn giao**

Đọc lại `docs/superpowers/specs/2026-07-16-quiz-design.md` và xác nhận từng tiêu chí hoàn thành có test hoặc verification tương ứng. Kiểm tra thủ công hai luồng bằng local app nếu connection string hoạt động:

1. Bộ có đủ pool: Quiz → trả lời đúng/sai → reload → hoàn thành → retry wrong.
2. Bộ/pool thiếu: Study Hub khóa Quiz với lý do, không tạo `StudySession` dở.

Không commit dữ liệu database, `.test-output`, `bin`, `obj` hoặc cấu hình local.
