# Quiz Flow Controls and Timer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a timed quiz setup flow, read-only previous-question navigation, restart, reliable retry-mode switching, and result actions at both ends of the review page.

**Architecture:** Keep `StudySession` as the authoritative quiz attempt record and persist its UTC start plus time limit. `QuizService` owns expiration, abandonment, navigation, restart, and retry transactions; Razor/JavaScript only render server state and request state transitions.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Entity Framework Core/SQL Server and SQLite tests, Razor, plain JavaScript, CSS, xUnit.

## Global Constraints

- Previous answered questions are review-only and cannot be re-answered.
- Presets are exactly 5, 10, 15, and 20 minutes; custom duration accepts whole minutes from 1 through 120.
- Expiration counts every unanswered question as wrong.
- The server is authoritative for deadlines and completion.
- Restart creates a fresh shuffled session from the current attempt's card scope and reuses its time limit.
- Retry wrong/all abandons any other active quiz attempt for the same user and set.
- Result actions render at the top and bottom without duplicating their markup implementation.
- Preserve the uncommitted Study Hub hover changes already present in the worktree.

---

### Task 1: Persist quiz timing and correct the active-session invariant

**Files:**
- Modify: `Models/Entities/StudySession.cs`
- Modify: `Data/AppDbContext.cs`
- Create: `Migrations/20260719154400_AddQuizTimingAndActiveSessionState.cs`
- Create: `Migrations/20260719154400_AddQuizTimingAndActiveSessionState.Designer.cs`
- Modify: `Migrations/AppDbContextModelSnapshot.cs`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`

**Interfaces:**
- Produces: `StudySession.QuizStartedAtUtc : DateTime?`
- Produces: `StudySession.QuizTimeLimitSeconds : int?`
- Produces: active-session filter `[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL`

- [ ] **Step 1: Add a failing model-contract test**

Add a test that reads EF metadata and asserts both timing properties exist and the filtered unique index includes `CompletedAt IS NULL`:

```csharp
[Fact]
public void Quiz_session_model_persists_timing_and_only_incomplete_rows_are_active()
{
    using AppDbContext context = CreateContext();
    IEntityType entity = context.Model.FindEntityType(typeof(StudySession))!;
    Assert.NotNull(entity.FindProperty(nameof(StudySession.QuizStartedAtUtc)));
    Assert.NotNull(entity.FindProperty(nameof(StudySession.QuizTimeLimitSeconds)));
    IIndex activeIndex = entity.GetIndexes().Single(index =>
        index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "UserId", "FlashcardSetId", "Mode" }));
    Assert.Equal("[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL", activeIndex.GetFilter());
}
```

- [ ] **Step 2: Run the focused test and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~Quiz_session_model_persists_timing"
```

Expected: FAIL because the timing properties do not exist and the current filter omits `CompletedAt`.

- [ ] **Step 3: Add entity properties and update EF configuration**

```csharp
public DateTime? QuizStartedAtUtc { get; set; }
public int? QuizTimeLimitSeconds { get; set; }
```

Use this index configuration:

```csharp
entity.HasIndex(e => new { e.UserId, e.FlashcardSetId, e.Mode })
    .IsUnique()
    .HasFilter("[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL");
```

- [ ] **Step 4: Generate and inspect the migration**

```powershell
dotnet ef migrations add AddQuizTimingAndActiveSessionState
```

The migration must add nullable `datetime2`/`int` columns, drop the old active-quiz index, and recreate it with the new filter. It must not modify unrelated tables.

- [ ] **Step 5: Run the focused test and verify GREEN**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 6: Commit the schema change**

```powershell
git add Models/Entities/StudySession.cs Data/AppDbContext.cs Migrations/20260719154400_AddQuizTimingAndActiveSessionState.cs Migrations/20260719154400_AddQuizTimingAndActiveSessionState.Designer.cs Migrations/AppDbContextModelSnapshot.cs tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs
git commit -m "feat(quiz): persist timed active session state"
```

### Task 2: Add setup state, duration validation, and fresh attempt creation

**Files:**
- Create: `Models/ViewModels/Study/QuizSetupViewModel.cs`
- Modify: `Services/Study/QuizModels.cs`
- Modify: `Services/Study/IQuizService.cs`
- Modify: `Services/Study/QuizService.cs`
- Modify: `Controllers/StudyController.cs`
- Create: `Views/Study/QuizSetup.cshtml`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`
- Test: `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs`
- Test: `tests/ltwnc.Tests/Views/QuizViewTests.cs`

**Interfaces:**
- Produces: `Task<QuizSetupState> GetSetupAsync(int setId, string userId)`
- Produces: `Task<StudySession> StartNewAsync(int setId, string userId, UserStudySettings settings, int timeLimitMinutes)`
- Produces: `QuizSetupViewModel` with setup duration and nullable active-session continuation data.

- [ ] **Step 1: Write failing service tests for validation and replacement**

Cover `0`, `121`, and a valid duration. Seed an active session, call `StartNewAsync(..., 15)`, and assert:

```csharp
Assert.NotNull(oldSession.CompletedAt);
Assert.Null(oldSession.Score);
Assert.Equal(15 * 60, newSession.QuizTimeLimitSeconds);
Assert.NotNull(newSession.QuizStartedAtUtc);
Assert.NotEqual(oldSession.Id, newSession.Id);
```

- [ ] **Step 2: Run service tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests&Name~StartNew"
```

Expected: FAIL because `StartNewAsync` is absent.

- [ ] **Step 3: Implement setup and fresh-start service APIs**

```csharp
public const int DefaultQuizMinutes = 10;
public const int MinimumQuizMinutes = 1;
public const int MaximumQuizMinutes = 120;

Task<QuizSetupState> GetSetupAsync(int setId, string userId);
Task<StudySession> StartNewAsync(int setId, string userId, UserStudySettings settings, int timeLimitMinutes);
```

Transactionally mark active rows `CompletedAt = now`, then create questions and a session with `QuizStartedAtUtc = now` and `QuizTimeLimitSeconds = timeLimitMinutes * 60`.

- [ ] **Step 4: Write failing controller/view tests**

Assert the GET renders `QuizSetup`, the POST rejects invalid input, and the view contains preset values `5`, `10`, `15`, `20`, a custom number input with `min="1" max="120"`, antiforgery, and a continue link when an active session exists.

- [ ] **Step 5: Implement setup GET/POST and Razor view**

```csharp
[HttpGet]
[Route("/Study/{setId}/Quiz")]
public async Task<IActionResult> QuizStart(int setId)

[HttpPost]
[Route("/Study/{setId}/Quiz/Start")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> QuizStart(int setId, QuizSetupViewModel input)
```

Resolve preset/custom minutes into one validated integer, call `StartNewAsync`, and redirect to the new session.

- [ ] **Step 6: Verify setup tests and commit**

```powershell
git add Models/ViewModels/Study/QuizSetupViewModel.cs Services/Study/QuizModels.cs Services/Study/IQuizService.cs Services/Study/QuizService.cs Controllers/StudyController.cs Views/Study/QuizSetup.cshtml tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat(quiz): add timed attempt setup"
```

### Task 3: Enforce expiration and count unanswered questions as wrong

**Files:**
- Modify: `Program.cs`
- Modify: `Services/Study/QuizModels.cs`
- Modify: `Services/Study/IQuizService.cs`
- Modify: `Services/Study/QuizService.cs`
- Modify: `Controllers/StudyController.cs`
- Modify: `Models/ViewModels/Study/QuizStudyViewModel.cs`
- Modify: `Models/ViewModels/Study/QuizResultViewModel.cs`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`
- Test: `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs`

**Interfaces:**
- Produces: `Task CompleteExpiredAsync(int setId, int sessionId, string userId)`
- Produces: `QuizExpiredException`
- Produces: `QuizQuestionState.DeadlineUtc` and `RemainingSeconds`
- Changes: pending means `IsCorrect == null`; timed-out means `IsCorrect == false` and `SelectedChoiceIndex == null`.

- [ ] **Step 1: Write failing deterministic expiration tests**

Inject `TimeProvider` into `QuizService`. Use a fixed test provider, advance beyond the deadline, call `CompleteExpiredAsync`, and assert every pending question is wrong, the score includes every question, `CompletedAt` is set, and a second call publishes no duplicate event.

- [ ] **Step 2: Run expiration tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests&Name~Expired"
```

- [ ] **Step 3: Implement server-authoritative deadline handling**

Register `TimeProvider.System`, inject it into `QuizService`, and use `_timeProvider.GetUtcNow().UtcDateTime`. Before returning a question or accepting an answer:

```csharp
if (IsExpired(session, now))
{
    await CompleteExpiredSessionAsync(session, now);
    throw new QuizExpiredException();
}
```

Expiry updates questions where `IsCorrect == null`, setting `IsCorrect = false` and `AnsweredAt = now`, then completes idempotently.

- [ ] **Step 4: Make results safe for unanswered selections**

```csharp
string selectedAnswer = question.SelectedChoiceIndex is int selected
    ? question.Choices[selected]
    : "Chưa trả lời";
```

- [ ] **Step 5: Add the timeout route**

```csharp
[HttpPost]
[Route("/Study/{setId}/Quiz/{sessionId:int}/Timeout")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> QuizTimeout(int setId, int sessionId)
```

Return JSON with the result URL for AJAX and redirect for a normal form post.

- [ ] **Step 6: Verify expiration tests and commit**

```powershell
git add Program.cs Services/Study/QuizModels.cs Services/Study/IQuizService.cs Services/Study/QuizService.cs Controllers/StudyController.cs Models/ViewModels/Study/QuizStudyViewModel.cs Models/ViewModels/Study/QuizResultViewModel.cs tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs
git commit -m "feat(quiz): enforce server timed completion"
```

### Task 4: Add read-only previous-question navigation

**Files:**
- Modify: `Services/Study/QuizModels.cs`
- Modify: `Services/Study/IQuizService.cs`
- Modify: `Services/Study/QuizService.cs`
- Modify: `Controllers/StudyController.cs`
- Modify: `Models/ViewModels/Study/QuizStudyViewModel.cs`
- Modify: `Views/Study/Quiz.cshtml`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`
- Test: `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs`
- Test: `tests/ltwnc.Tests/Views/QuizViewTests.cs`

**Interfaces:**
- Changes: `GetCurrentQuestionAsync(int setId, int sessionId, string userId, int? questionId = null)`
- Produces: `IsReviewOnly`, stored choice indexes, previous/next question ids, and current pending id.

- [ ] **Step 1: Write failing navigation tests**

Answer two questions, request the first by id, and assert review-only state with stored choices, no mutation, and correct navigation ids. Request a question from another session and expect `KeyNotFoundException`.

- [ ] **Step 2: Run navigation tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests&Name~Review"
```

- [ ] **Step 3: Implement question lookup and navigation state**

Load ordered questions once. Select the requested owned question when `questionId` is present; otherwise select the first `IsCorrect == null` question. Never expose correct-choice data for a pending question.

- [ ] **Step 4: Render review-only state**

Add `data-quiz-review-only`. Render reviewed choices disabled with stored correct/wrong classes, Previous/Next links, and `Quay lại câu đang làm`.

- [ ] **Step 5: Verify navigation tests and commit**

```powershell
git add Services/Study/QuizModels.cs Services/Study/IQuizService.cs Services/Study/QuizService.cs Controllers/StudyController.cs Models/ViewModels/Study/QuizStudyViewModel.cs Views/Study/Quiz.cshtml tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat(quiz): add read-only question review navigation"
```

### Task 5: Add restart and reliable retry-mode switching

**Files:**
- Modify: `Services/Study/IQuizService.cs`
- Modify: `Services/Study/QuizService.cs`
- Modify: `Controllers/StudyController.cs`
- Modify: `Views/Study/Quiz.cshtml`
- Test: `tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs`
- Test: `tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs`

**Interfaces:**
- Produces: `Task<StudySession> RestartAsync(int setId, int sessionId, string userId)`
- Changes: `RetryWrongAsync` and `RetryAllAsync` abandon active attempts before creating the requested session.

- [ ] **Step 1: Write failing restart/retry-switch tests**

Verify restart abandons the active source, reuses its card ids and time limit, clears all answers, and creates a newly shuffled attempt. Verify `RetryWrong` followed by `RetryAll` abandons the wrong-only active session and creates a full-size active session.

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests&Name~Restart|FullyQualifiedName~QuizServiceTests&Name~Retry_switch"
```

- [ ] **Step 3: Implement transactional restart and retry replacement**

Replace the `FindActiveQuizSessionAsync` early returns in retry creation with active-session abandonment. Restart loads current question card ids, abandons the attempt, and creates a fresh randomized attempt with the same duration. Use 10 minutes for a legacy source without a duration.

- [ ] **Step 4: Add restart POST and confirmation form**

```csharp
[HttpPost]
[Route("/Study/{setId}/Quiz/{sessionId:int}/Restart")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> QuizRestart(int setId, int sessionId)
```

The view button says `Làm lại từ đầu` and confirms that current answers will be discarded.

- [ ] **Step 5: Verify and commit**

```powershell
git add Services/Study/IQuizService.cs Services/Study/QuizService.cs Controllers/StudyController.cs Views/Study/Quiz.cshtml tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs
git commit -m "fix(quiz): allow restart and retry mode switching"
```

### Task 6: Add countdown behavior and responsive controls

**Files:**
- Modify: `wwwroot/js/quiz.js`
- Modify: `wwwroot/css/quiz.css`
- Modify: `Views/Study/Quiz.cshtml`
- Test: `tests/ltwnc.Tests/Views/QuizViewTests.cs`

**Interfaces:**
- Consumes: `data-quiz-deadline-utc`, `data-quiz-timeout-url`, and the antiforgery token.
- Produces: `[data-quiz-timer]`, final-minute `.is-warning`, and one timeout POST at zero.

- [ ] **Step 1: Write failing JavaScript/view contract tests**

Assert the view renders timer/deadline/timeout contracts and the script uses `Date.parse`, a deadline-based interval, a one-shot guard, antiforgery headers, and redirect from timeout JSON.

- [ ] **Step 2: Run view tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests"
```

- [ ] **Step 3: Implement countdown and timeout submission**

Compute remaining seconds from `deadline - Date.now()` on every tick, render `mm:ss`, add `.is-warning` at 60 seconds, and invoke the timeout endpoint once at zero. If an answer request reports expiration, redirect to its result URL.

- [ ] **Step 4: Style controls and reduced motion**

Add responsive timer, restart, and navigation styles while preserving focus visibility. Include any new transitions in the existing reduced-motion override.

- [ ] **Step 5: Verify and commit**

```powershell
git add Views/Study/Quiz.cshtml wwwroot/js/quiz.js wwwroot/css/quiz.css tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat(quiz): add countdown and attempt controls"
```

### Task 7: Duplicate result actions through a shared partial

**Files:**
- Create: `Views/Study/_QuizResultActions.cshtml`
- Modify: `Views/Study/QuizResult.cshtml`
- Modify: `wwwroot/css/quiz.css`
- Test: `tests/ltwnc.Tests/Views/QuizViewTests.cs`

**Interfaces:**
- Consumes: `QuizResultViewModel`
- Produces: one reusable action partial rendered twice.

- [ ] **Step 1: Write a failing result-view test**

Assert `QuizResult.cshtml` invokes `_QuizResultActions` twice and the partial alone owns both retry forms, antiforgery tokens, and the Study Hub link.

- [ ] **Step 2: Run the result-view test and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests&Name~Result_actions"
```

- [ ] **Step 3: Extract and render the partial**

Render immediately after the summary and after the review/perfect block:

```razor
<partial name="_QuizResultActions" model="Model" />
```

Use modifier classes so the upper group has bottom spacing and the lower group retains top spacing.

- [ ] **Step 4: Verify and commit**

```powershell
git add Views/Study/QuizResult.cshtml Views/Study/_QuizResultActions.cshtml wwwroot/css/quiz.css tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat(quiz): surface result actions above review"
```

### Task 8: Full regression and migration verification

**Files:**
- Verify all files changed by Tasks 1–7.

**Interfaces:**
- Produces: a buildable branch with a SQL Server migration and passing SQLite-based tests.

- [ ] **Step 1: Generate and inspect migration SQL**

```powershell
dotnet ef migrations script 20260716180459_PreventConcurrentActiveQuizSessions AddQuizTimingAndActiveSessionState --idempotent
```

Confirm the script adds only the two timing columns and updates the active-session index filter.

- [ ] **Step 2: Run all tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
```

Expected: zero failures.

- [ ] **Step 3: Build and validate the diff**

```powershell
dotnet build --no-restore
git diff --check
git status --short
```

Expected: build succeeds with zero errors; diff check emits no errors; only intentional hover work and quiz-flow files remain.

- [ ] **Step 4: Perform manual flow verification**

Verify setup presets/custom validation, countdown and expiry, read-only previous questions, restart, retry wrong then retry all switching to the full set, and result actions before and after the review list.
