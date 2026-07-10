# Sentence Dictation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `SentenceDictation` study mode that plays each card's `ExampleSentence` and checks whether the user types the full sentence correctly.

**Architecture:** Reuse the existing `Dictation.cshtml` view with a new `IsSentenceMode` flag. Introduce a `DictationSource` enum in `DictationService` to decide which field is spoken and graded. A new controller action `/Study/{setId}/Dictation/Sentence` creates a session with `Mode = StudyMode.SentenceDictation` and filters out cards with no example sentence.

**Tech Stack:** ASP.NET Core MVC, Razor, EF Core, Web Speech API, xUnit, Moq.

## Global Constraints

- All new and changed code comments must use `//` style single-line comments; do not use XML doc comments (`///`).
- Do not add new database columns; reuse existing `UserStudySettings` properties.
- Reuse existing filters (`StarredOnly`, `UnlearnedOnly`), shuffle, auto-advance, TTS voice/speed, and hint settings.
- Hide the "Chế độ trả lờ" and "Tùy chọn trả lờ" groups in the settings drawer when `IsSentenceMode` is true.
- Adding an enum value to `StudyMode` does not require a migration because the column is stored as `int`.

---

### Task 1: Add `SentenceDictation` to the `StudyMode` enum

**Files:**
- Modify: `Models/Entities/StudySession.cs:8-15`

**Interfaces:**
- Produces: new enum value `StudyMode.SentenceDictation` usable by services and controllers.

- [ ] **Step 1: Add the enum value**

```csharp
// Enum định nghĩa các chế độ học
public enum StudyMode
{
    Flashcard, // Lật thẻ
    Quiz,      // Trắc nghiệm
    Write,     // Viết chính tả
    Match,     // Ghép đôi
    Dictation, // Nghe chép chính tả
    SentenceDictation // Nghe chép câu
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 3: Commit**

```bash
git add Models/Entities/StudySession.cs
git commit -m "feat(dictation): add SentenceDictation study mode enum"
```

---

### Task 2: Add `DictationSource` enum and update answer-checking logic

**Files:**
- Modify: `Services/DictationService.cs:7-173`

**Interfaces:**
- Produces: `public enum DictationSource { Term, Definition, ExampleSentence }`
- Produces: changed signature `CheckAnswerAsync(..., DictationSource source, bool acceptSynonyms)`
- Consumes: `StudyMode` from Task 1, `Flashcard.ExampleSentence` from existing entity.

- [ ] **Step 1: Add the enum above the service class**

```csharp
// Nguồn nội dung được phát và kiểm tra trong bài nghe chép
public enum DictationSource
{
    Term,            // Thuật ngữ
    Definition,      // Định nghĩa
    ExampleSentence  // Câu ví dụ
}
```

- [ ] **Step 2: Change `CheckAnswerAsync` signature and implementation**

Replace the existing method with:

```csharp
// Kiểm tra đáp án của ngườ dùng
public async Task<DictationCheckResult> CheckAnswerAsync(
    int sessionId,
    int cardId,
    string answeredText,
    string userId,
    DictationSource source,
    bool acceptSynonyms)
{
    var session = await _context.StudySessions.FindAsync(sessionId);
    if (session == null)
        throw new KeyNotFoundException("Phiên học không tồn tại.");

    if (session.UserId != userId)
        throw new UnauthorizedAccessException("Không có quyền truy cập phiên học này.");

    var card = await _context.Flashcards.FindAsync(cardId);
    if (card == null)
        throw new KeyNotFoundException("Thẻ không tồn tại.");

    // Đảm bảo thẻ thuộc về bộ thẻ của phiên học
    if (card.FlashcardSetId != session.FlashcardSetId)
        throw new KeyNotFoundException("Thẻ không thuộc bộ thẻ này.");

    // Đáp án đúng tùy theo nguồn nội dung
    var correctAnswer = source switch
    {
        DictationSource.Definition => card.BackText,
        DictationSource.ExampleSentence => card.ExampleSentence,
        _ => card.FrontText
    };

    // Tập hợp các đáp án được chấp nhận
    var acceptedAnswers = new List<string> { correctAnswer };

    // Nếu chấp nhận từ đồng nghĩa và đang ở chế độ thuật ngữ
    if (acceptSynonyms && source == DictationSource.Term && !string.IsNullOrWhiteSpace(card.Synonyms))
    {
        var synonyms = card.Synonyms
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
        acceptedAnswers.AddRange(synonyms);
    }

    // Chuẩn hóa đáp án ngườ dùng
    var normalizedInput = NormalizeAnswer(answeredText);

    // Kiểm tra khớp
    var isCorrect = acceptedAnswers.Any(a => NormalizeAnswer(a) == normalizedInput);

    // Cập nhật tiến trình học của ngườ dùng
    await UpdateUserProgressAsync(userId, cardId, isCorrect);

    // Lưu chi tiết câu trả lờ
    var detail = new DictationSessionDetail
    {
        StudySessionId = sessionId,
        FlashcardId = cardId,
        IsCorrect = isCorrect,
        AnsweredText = answeredText ?? string.Empty
    };
    await _context.DictationSessionDetails.AddAsync(detail);
    await _context.SaveChangesAsync();

    return new DictationCheckResult
    {
        IsCorrect = isCorrect,
        CorrectAnswer = correctAnswer,
        Hint = isCorrect ? null : BuildHint(card)
    };
}
```

- [ ] **Step 3: Build to catch compile errors**

Run: `dotnet build`
Expected: existing callers/tests will fail; they are fixed in later tasks.

- [ ] **Step 4: Commit**

```bash
git add Services/DictationService.cs
git commit -m "feat(dictation): add DictationSource and sentence answer checking"
```

---

### Task 3: Extend `CreateSessionAsync` to accept a `StudyMode`

**Files:**
- Modify: `Services/DictationService.cs:90-103`

**Interfaces:**
- Produces: `public async Task<StudySession> CreateSessionAsync(string userId, int setId, StudyMode mode)`
- Produces: existing `CreateSessionAsync(string userId, int setId)` delegates to the new overload with `StudyMode.Dictation`.

- [ ] **Step 1: Replace the existing `CreateSessionAsync` method**

```csharp
// Tạo phiên học Dictation mới
public async Task<StudySession> CreateSessionAsync(string userId, int setId)
{
    return await CreateSessionAsync(userId, setId, StudyMode.Dictation);
}

// Tạo phiên học với chế độ tùy chỉnh
public async Task<StudySession> CreateSessionAsync(string userId, int setId, StudyMode mode)
{
    var session = new StudySession
    {
        UserId = userId,
        FlashcardSetId = setId,
        Mode = mode,
        CompletedAt = DateTime.UtcNow
    };

    await _context.StudySessions.AddAsync(session);
    await _context.SaveChangesAsync();
    return session;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL (signature change already made).

- [ ] **Step 3: Commit**

```bash
git add Services/DictationService.cs
git commit -m "feat(dictation): add StudyMode overload to CreateSessionAsync"
```

---

### Task 4: Filter sentence mode cards by non-empty `ExampleSentence`

**Files:**
- Modify: `Services/DictationService.cs:46-74`

**Interfaces:**
- Produces: `GetCardsForDictationAsync(int setId, string userId, UserStudySettings settings, DictationSource source = DictationSource.Term)`
- Consumes: `DictationSource` from Task 2.

- [ ] **Step 1: Update method signature and add source filter**

```csharp
// Lấy danh sách thẻ cho bài nghe chép, áp dụng lọc và xáo trộn
public async Task<List<Flashcard>> GetCardsForDictationAsync(
    int setId,
    string userId,
    UserStudySettings settings,
    DictationSource source = DictationSource.Term)
{
    var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

    // Chỉ lấy thẻ đánh dấu sao
    if (settings.StarredOnly)
    {
        query = query.Where(f => f.IsStarred);
    }

    // Chỉ lấy thẻ chưa thuộc
    if (settings.UnlearnedOnly)
    {
        query = query.Where(f => !_context.UserProgresses.Any(p =>
            p.UserId == userId &&
            p.FlashcardId == f.Id &&
            p.IsLearned));
    }

    // Chế độ câu chỉ lấy thẻ có câu ví dụ
    if (source == DictationSource.ExampleSentence)
    {
        query = query.Where(f => !string.IsNullOrWhiteSpace(f.ExampleSentence));
    }

    var cards = await query.OrderBy(f => f.OrderIndex).ToListAsync();

    // Xáo trộn nếu cài đặt bật
    if (settings.DictationShuffle)
    {
        cards = Shuffle(cards);
    }

    return cards;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 3: Commit**

```bash
git add Services/DictationService.cs
git commit -m "feat(dictation): filter sentence mode cards by ExampleSentence"
```

---

### Task 5: Add `ExampleSentence` to result models

**Files:**
- Modify: `Services/DictationService.cs:25-32` and `Services/DictationService.cs:245-286`
- Modify: `Models/ViewModels/Study/DictationResultCardViewModel.cs:16-22`

**Interfaces:**
- Produces: `DictationResultCard.ExampleSentence`
- Produces: `DictationResultCardViewModel.ExampleSentence`
- Produces: `GetSessionResultAsync` populates `ExampleSentence`.

- [ ] **Step 1: Add property to `DictationResultCard`**

```csharp
// Thông tin một thẻ sai trong màn hình tổng kết
public class DictationResultCard
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Populate `ExampleSentence` in `GetSessionResultAsync`**

In the projection inside `GetSessionResultAsync`, set:

```csharp
var wrongCards = details
    .Where(d => !d.IsCorrect && d.Flashcard != null)
    .Select(d => new DictationResultCard
    {
        Id = d.Flashcard!.Id,
        Term = d.Flashcard.FrontText,
        Definition = d.Flashcard.BackText,
        Pronunciation = d.Flashcard.Pronunciation,
        ExampleSentence = d.Flashcard.ExampleSentence
    })
    .ToList();
```

- [ ] **Step 3: Add property to viewmodel**

```csharp
// Thông tin một thẻ cần ôn trong màn hình tổng kết
public class DictationResultCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 5: Commit**

```bash
git add Services/DictationService.cs Models/ViewModels/Study/DictationResultCardViewModel.cs
git commit -m "feat(dictation): include ExampleSentence in result models"
```

---

### Task 6: Update study viewmodels with sentence-mode flags

**Files:**
- Modify: `Models/ViewModels/Study/DictationStudyViewModel.cs:6-14`
- Modify: `Models/ViewModels/Study/DictationCardViewModel.cs:4-11`

**Interfaces:**
- Produces: `DictationStudyViewModel.IsSentenceMode`, `DictationStudyViewModel.Source`
- Produces: `DictationCardViewModel.ExampleSentence`

- [ ] **Step 1: Update `DictationStudyViewModel`**

```csharp
// Dữ liệu truyền cho view học nghe chép
public class DictationStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<DictationCardViewModel> Cards { get; set; } = new();
    public UserStudySettings Settings { get; set; } = new();
    public int SessionId { get; set; }
    public int StreakDays { get; set; }
    public bool IsSentenceMode { get; set; }
    public string Source { get; set; } = "Term"; // Term | Definition | ExampleSentence
}
```

- [ ] **Step 2: Update `DictationCardViewModel`**

```csharp
// Thông tin một thẻ hiển thị trong bài nghe chép
public class DictationCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string ExampleSentence { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 4: Commit**

```bash
git add Models/ViewModels/Study/DictationStudyViewModel.cs Models/ViewModels/Study/DictationCardViewModel.cs
git commit -m "feat(dictation): add sentence-mode flags to study viewmodels"
```

---

### Task 7: Add controller action and adapt answer checking

**Files:**
- Modify: `Controllers/StudyController.cs:245-322`

**Interfaces:**
- Produces: `SentenceDictation(int setId)` action at `/Study/{setId}/Dictation/Sentence`
- Produces: `DictationCheck` accepts optional `source` parameter and maps it to `DictationSource`.
- Consumes: `DictationStudyViewModel`, `DictationCardViewModel`, `DictationSource`, `StudyMode.SentenceDictation`.

- [ ] **Step 1: Update the existing `Dictation` action**

Replace the viewmodel creation block in `Dictation` with:

```csharp
var session = await _dictationService.CreateSessionAsync(user.Id, setId);

var viewModel = new DictationStudyViewModel
{
    SetId = setId,
    SetTitle = set.Title,
    SessionId = session.Id,
    Settings = settings,
    IsSentenceMode = false,
    Source = settings.DictationAnswerMode.ToString(),
    Cards = cards.Select(c => new DictationCardViewModel
    {
        Id = c.Id,
        Term = c.FrontText,
        Definition = c.BackText,
        Pronunciation = c.Pronunciation,
        ExampleSentence = c.ExampleSentence,
        ImageUrl = !string.IsNullOrWhiteSpace(c.UploadedImagePath) ? c.UploadedImagePath : c.ImageUrl
    }).ToList()
};
```

- [ ] **Step 2: Add the `SentenceDictation` action after `Dictation`**

```csharp
// Hiển thị giao diện học nghe chép câu
// Yêu cầu đăng nhập
[Authorize]
[Route("/Study/{setId}/Dictation/Sentence")]
public async Task<IActionResult> SentenceDictation(int setId)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
    if (set == null) return NotFound();

    var settings = await _studyService.GetSettingsAsync(user.Id);
    var cards = await _dictationService.GetCardsForDictationAsync(
        setId, user.Id, settings, DictationSource.ExampleSentence);

    if (!cards.Any())
    {
        TempData["Message"] = settings.StarredOnly || settings.UnlearnedOnly
            ? "Không có thẻ phù hợp với bộ lọc hiện tại."
            : "Không có câu ví dụ để học.";
        return RedirectToAction("Index", new { setId });
    }

    var session = await _dictationService.CreateSessionAsync(
        user.Id, setId, StudyMode.SentenceDictation);

    var viewModel = new DictationStudyViewModel
    {
        SetId = setId,
        SetTitle = set.Title,
        SessionId = session.Id,
        Settings = settings,
        IsSentenceMode = true,
        Source = "ExampleSentence",
        Cards = cards.Select(c => new DictationCardViewModel
        {
            Id = c.Id,
            Term = c.FrontText,
            Definition = c.BackText,
            Pronunciation = c.Pronunciation,
            ExampleSentence = c.ExampleSentence,
            ImageUrl = !string.IsNullOrWhiteSpace(c.UploadedImagePath) ? c.UploadedImagePath : c.ImageUrl
        }).ToList()
    };

    return View("Dictation", viewModel);
}
```

- [ ] **Step 3: Update `DictationCheck` to accept and parse `source`**

```csharp
// Kiểm tra đáp án nghe chép qua AJAX
[HttpPost]
[Route("/Study/{setId}/Dictation/Check")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DictationCheck(
    int setId,
    int sessionId,
    int cardId,
    string answeredText,
    string source = "Term")
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized();

    try
    {
        var settings = await _studyService.GetSettingsAsync(user.Id);
        if (!Enum.TryParse<DictationSource>(source, out var dictationSource))
        {
            dictationSource = DictationSource.Term;
        }

        var result = await _dictationService.CheckAnswerAsync(
            sessionId, cardId, answeredText, user.Id,
            dictationSource,
            settings.DictationAcceptSynonyms);

        return Json(new
        {
            success = true,
            isCorrect = result.IsCorrect,
            correctAnswer = result.CorrectAnswer,
            hint = result.Hint
        });
    }
    catch (KeyNotFoundException)
    {
        return NotFound();
    }
    catch (UnauthorizedAccessException)
    {
        return Forbid();
    }
}
```

- [ ] **Step 4: Update `DictationResult` mapping**

In the `DictationResult` action, add `ExampleSentence` to the wrong-card projection:

```csharp
WrongCards = result.WrongCards.Select(c => new DictationResultCardViewModel
{
    Id = c.Id,
    Term = c.Term,
    Definition = c.Definition,
    Pronunciation = c.Pronunciation,
    ExampleSentence = c.ExampleSentence
}).ToList()
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 6: Commit**

```bash
git add Controllers/StudyController.cs
git commit -m "feat(dictation): add SentenceDictation action and source-aware check"
```

---

### Task 8: Add the "Nghe chép câu" card to the study index

**Files:**
- Modify: `Views/Study/Index.cshtml:58-66`

**Interfaces:**
- Produces: new card linking to `/Study/{setId}/Dictation/Sentence`.

- [ ] **Step 1: Insert a new card after the existing Nghe chép card**

```html
<div class="col-md-6">
    <a href="/Study/@setId/Dictation/Sentence" class="text-decoration-none">
        <div class="card-custom text-center py-5">
            <i class="ph ph-chat-text" style="font-size: 3rem;"></i>
            <h5 class="mt-3 mb-1" style="font-weight: 600;">Nghe chép câu</h5>
            <p style="color: #787774; font-size: 0.875rem; margin: 0;">Nghe và viết lại câu ví dụ</p>
        </div>
    </a>
</div>
```

- [ ] **Step 2: Verify the view renders**

Run the app locally, navigate to `/Study/1`, and confirm the new card appears.

- [ ] **Step 3: Commit**

```bash
git add Views/Study/Index.cshtml
git commit -m "feat(dictation): add sentence dictation card to study index"
```

---

### Task 9: Update the `Dictation` view for sentence mode

**Files:**
- Modify: `Views/Study/Dictation.cshtml`

**Interfaces:**
- Consumes: `Model.IsSentenceMode`, `Model.Source`, `DictationCardViewModel.ExampleSentence`.

- [ ] **Step 1: Conditional eyebrow, title, card title, and input label**

Replace the eyebrow span at line 20:

```html
<span class="dictation-eyebrow">@(Model.IsSentenceMode ? "Nghe chép câu" : "Nghe chép chính tả")</span>
```

Replace the card title at line 39:

```html
<h2 class="dictation-card-title">@(Model.IsSentenceMode ? "Bài tập nghe chép câu" : "Bài tập nghe chép")</h2>
```

Replace the input label at line 58:

```html
<label for="dictation-answer" class="dictation-input-label">@(Model.IsSentenceMode ? "Điền lại câu ví dụ bạn nghe được" : "Điền lại nội dung bạn nghe được")</label>
```

- [ ] **Step 2: Serialize `ExampleSentence` and pass flags to JavaScript**

Replace the `cards` declaration with:

```javascript
const cards = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.Cards.Select(c => new
{
    c.Id,
    c.Term,
    c.Definition,
    c.Pronunciation,
    c.ExampleSentence,
    Image = c.ImageUrl
})));
const isSentenceMode = @(Model.IsSentenceMode ? "true" : "false");
const source = '@Model.Source';
```

- [ ] **Step 3: Rename and update the audio playback function**

Replace `playCurrentTerm` with `playCurrentPrompt`:

```javascript
function playCurrentPrompt() {
    if (!synth || cards.length === 0) return;
    const text = source === 'ExampleSentence'
        ? cards[currentIndex].ExampleSentence
        : cards[currentIndex].Term;
    speak(text);
}
```

Update the play button `onclick` at line 42:

```html
<button type="button" id="dictation-play-btn" class="dictation-audio-btn" onclick="playCurrentPrompt()" title="Phát âm (Ctrl + 1)">
```

Update all other calls to `playCurrentTerm` to `playCurrentPrompt`:
- In `nextCard`.
- In the document body click handler.
- In the keyboard shortcut handler.

- [ ] **Step 4: Send `source` in the check request**

In `submitCheck`, after appending `answeredText`, add:

```javascript
params.append('source', source);
```

- [ ] **Step 5: Hide answer-mode and synonym options in sentence mode**

Wrap the "Chế độ trả lờ" group with:

```html
@if (!Model.IsSentenceMode)
{
    <div class="dictation-settings-group">
        <div class="dictation-settings-group-title">Chế độ trả lờ</div>
        <label class="dictation-settings-option"><input type="radio" name="answerMode" value="Term" data-setting="DictationAnswerMode" checked> Trả lờ bằng thuật ngữ</label>
        <label class="dictation-settings-option"><input type="radio" name="answerMode" value="Definition" data-setting="DictationAnswerMode"> Trả lờ bằng định nghĩa</label>
    </div>
}
```

Wrap the "Tùy chọn trả lờ" group with:

```html
@if (!Model.IsSentenceMode)
{
    <div class="dictation-settings-group">
        <div class="dictation-settings-group-title">Tùy chọn trả lờ</div>
        <label class="dictation-settings-option"><input type="checkbox" data-setting="DictationAcceptSynonyms"> Chấp nhận từ đồng nghĩa</label>
    </div>
}
```

- [ ] **Step 6: Build and smoke-test**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

Manually verify both `/Study/1/Dictation` and `/Study/1/Dictation/Sentence` render without errors.

- [ ] **Step 7: Commit**

```bash
git add Views/Study/Dictation.cshtml
git commit -m "feat(dictation): adapt dictation view for sentence mode"
```

---

### Task 10: Update the result view to show example sentences

**Files:**
- Modify: `Views/Study/DictationResult.cshtml:42-47`

**Interfaces:**
- Consumes: `DictationResultCardViewModel.ExampleSentence`.

- [ ] **Step 1: Show example sentence in wrong-card chips**

Replace the inner content of the `foreach` block with:

```html
<div class="wrong-card-chip">
    <span class="wrong-card-term">@card.Term</span>
    <span class="wrong-card-pronunciation">@card.Pronunciation</span>
    <span class="wrong-card-definition">@card.Definition</span>
    @if (!string.IsNullOrWhiteSpace(card.ExampleSentence))
    {
        <span class="wrong-card-example" style="font-style: italic; color: #555;">@card.ExampleSentence</span>
    }
</div>
```

- [ ] **Step 2: Build and smoke-test**

Run: `dotnet build`
Expected: BUILD SUCCESSFUL.

- [ ] **Step 3: Commit**

```bash
git add Views/Study/DictationResult.cshtml
git commit -m "feat(dictation): show ExampleSentence on result page"
```

---

### Task 11: Update and add unit tests for `DictationService`

**Files:**
- Modify: `tests/ltwnc.Tests/Services/DictationServiceTests.cs`

**Interfaces:**
- Consumes: `DictationSource` enum, changed `CheckAnswerAsync` signature, `GetCardsForDictationAsync` source overload, `CreateSessionAsync` mode overload.

- [ ] **Step 1: Update existing `CheckAnswerAsync` calls**

Replace every `DictationAnswerMode.Term` argument with `DictationSource.Term` and every `DictationAnswerMode.Definition` with `DictationSource.Definition`.

For example, `CheckAnswerAsync_CorrectExactAnswer_ReturnsIsCorrectTrue` becomes:

```csharp
var result = await service.CheckAnswerAsync(
    session.Id, 1, "hello", "user-1",
    DictationSource.Term, acceptSynonyms: true);
```

- [ ] **Step 2: Add sentence-mode tests**

Add the following tests to the class:

```csharp
[Fact]
public async Task CheckAnswerAsync_ExampleSentence_CorrectAnswer_ReturnsTrue()
{
    await using var context = CreateContext();
    await SeedSetAsync(context);
    await SeedCardAsync(context, 1, "hello", "xin chào", synonyms: null);
    context.Flashcards.Find(1)!.ExampleSentence = "Hello, world!";
    await context.SaveChangesAsync();

    var service = new DictationService(context);
    var session = await service.CreateSessionAsync("user-1", 1);

    var result = await service.CheckAnswerAsync(
        session.Id, 1, "Hello, world!", "user-1",
        DictationSource.ExampleSentence, acceptSynonyms: true);

    Assert.True(result.IsCorrect);
    Assert.Equal("Hello, world!", result.CorrectAnswer);
}

[Fact]
public async Task CheckAnswerAsync_ExampleSentence_WrongAnswer_ReturnsFalse()
{
    await using var context = CreateContext();
    await SeedSetAsync(context);
    await SeedCardAsync(context, 1, "hello", "xin chào", synonyms: null);
    context.Flashcards.Find(1)!.ExampleSentence = "Hello, world!";
    await context.SaveChangesAsync();

    var service = new DictationService(context);
    var session = await service.CreateSessionAsync("user-1", 1);

    var result = await service.CheckAnswerAsync(
        session.Id, 1, "wrong sentence", "user-1",
        DictationSource.ExampleSentence, acceptSynonyms: true);

    Assert.False(result.IsCorrect);
}

[Fact]
public async Task CreateSessionAsync_SentenceMode_SetsModeCorrectly()
{
    await using var context = CreateContext();
    await SeedSetAsync(context);

    var service = new DictationService(context);
    var session = await service.CreateSessionAsync("user-1", 1, StudyMode.SentenceDictation);

    Assert.Equal(StudyMode.SentenceDictation, session.Mode);
}

[Fact]
public async Task GetCardsForDictationAsync_SentenceMode_ExcludesCardsWithoutExampleSentence()
{
    await using var context = CreateContext();
    await SeedSetAsync(context);
    await SeedCardAsync(context, 1, "hello", "xin chào", synonyms: null);
    await SeedCardAsync(context, 2, "world", "thế giới", synonyms: null);
    context.Flashcards.Find(1)!.ExampleSentence = "Hello, world!";
    context.Flashcards.Find(2)!.ExampleSentence = "   ";
    await context.SaveChangesAsync();

    var service = new DictationService(context);
    var settings = new UserStudySettings();

    var result = await service.GetCardsForDictationAsync(
        1, "user-1", settings, DictationSource.ExampleSentence);

    Assert.Single(result);
    Assert.Equal("hello", result[0].FrontText);
}
```

- [ ] **Step 3: Run the service tests**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~DictationServiceTests" -v n`
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/ltwnc.Tests/Services/DictationServiceTests.cs
git commit -m "test(dictation): add sentence mode service tests"
```

---

### Task 12: Add controller integration tests for sentence mode

**Files:**
- Modify: `tests/ltwnc.Tests/Controllers/StudyControllerDictationTests.cs`

**Interfaces:**
- Consumes: `StudyController.SentenceDictation`, `DictationCheck` with `source`, `DictationStudyViewModel.Source`.

- [ ] **Step 1: Add a helper to seed cards with/without example sentences**

Add this private method to the test class:

```csharp
private async Task SeedSetWithExampleCardsAsync(AppDbContext context)
{
    var set = new FlashcardSet
    {
        Id = 1,
        Title = "Test Set",
        UserId = "user-1",
        IsPublic = true
    };
    await context.FlashcardSets.AddAsync(set);

    var card = new Flashcard
    {
        Id = 1,
        FlashcardSetId = 1,
        FrontText = "hello",
        BackText = "xin chào",
        Pronunciation = "/həˈloʊ/",
        PartOfSpeech = "exclamation",
        ExampleSentence = "Hello, world!",
        ExampleMeaning = "Xin chào, thế giới!",
        OrderIndex = 0
    };
    await context.Flashcards.AddAsync(card);
    await context.SaveChangesAsync();
}
```

- [ ] **Step 2: Add sentence-mode controller tests**

```csharp
[Fact]
public async Task SentenceDictation_Get_ReturnsViewWithModel()
{
    await using var context = CreateContext();
    await SeedSetWithExampleCardsAsync(context);

    var controller = CreateController(context, "user-1");
    var result = await controller.SentenceDictation(1);

    var viewResult = Assert.IsType<ViewResult>(result);
    var model = Assert.IsType<DictationStudyViewModel>(viewResult.Model);
    Assert.True(model.IsSentenceMode);
    Assert.Equal("ExampleSentence", model.Source);
}

[Fact]
public async Task SentenceDictationCheck_Post_CorrectExample_ReturnsSuccess()
{
    await using var context = CreateContext();
    await SeedSetWithExampleCardsAsync(context);

    var controller = CreateController(context, "user-1");
    var sentenceResult = await controller.SentenceDictation(1);
    var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(sentenceResult).Model);

    var result = await controller.DictationCheck(1, viewModel.SessionId, 1, "Hello, world!", "ExampleSentence");

    var jsonResult = Assert.IsType<JsonResult>(result);
    var element = JsonSerializer.SerializeToElement(jsonResult.Value);
    Assert.True(element.GetProperty("success").GetBoolean());
    Assert.True(element.GetProperty("isCorrect").GetBoolean());
}

[Fact]
public async Task SentenceDictation_EmptyExamples_RedirectsToIndex()
{
    await using var context = CreateContext();
    await SeedSetAndCardAsync(context);
    context.Flashcards.Find(1)!.ExampleSentence = "   ";
    await context.SaveChangesAsync();

    var controller = CreateController(context, "user-1");
    var result = await controller.SentenceDictation(1);

    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Index", redirectResult.ActionName);
}
```

- [ ] **Step 3: Run the controller tests**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~StudyControllerDictationTests" -v n`
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/ltwnc.Tests/Controllers/StudyControllerDictationTests.cs
git commit -m "test(dictation): add sentence mode controller tests"
```

---

### Task 13: Run full test suite and verify manually

**Files:**
- No file changes.

- [ ] **Step 1: Run all tests**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 2: Run the application**

Run: `dotnet run --project ltwnc.csproj`
Open the app in a browser.

- [ ] **Step 3: Manual checklist**

1. `/Study/{setId}` shows a "Nghe chép câu" card.
2. Clicking it opens `/Study/{setId}/Dictation/Sentence`.
3. The page title/eyebrow/input label reflect sentence mode.
4. The play button reads the example sentence aloud.
5. Typing the exact example sentence returns `isCorrect: true`.
6. Typing a wrong sentence shows the correct example sentence as the answer.
7. Completing the session redirects to the result page with example sentences shown for wrong cards.
8. The settings drawer hides answer-mode and synonym options in sentence mode.
9. `/Study/{setId}/Dictation` still works for term/definition mode.

- [ ] **Step 4: Commit any final fixes and finish the branch**

Use `superpowers:finishing-a-development-branch` to verify tests, present options, and complete the branch.

---

## Self-Review

**1. Spec coverage:**
- `StudyMode.SentenceDictation` enum value → Task 1.
- `DictationSource` enum with `Term/Definition/ExampleSentence` → Task 2.
- `CheckAnswerAsync` uses source → Task 2.
- `CreateSessionAsync` mode overload → Task 3.
- Sentence mode filters empty examples → Task 4.
- Controller `SentenceDictation` action → Task 7.
- `DictationCheck` accepts source → Task 7.
- ViewModels carry sentence flags/data → Tasks 5–6.
- Index card → Task 8.
- `Dictation.cshtml` sentence UI + settings drawer hide → Task 9.
- `DictationResult.cshtml` shows example sentences → Task 10.
- Unit + controller tests → Tasks 11–12.
- Manual verification → Task 13.
- No gaps found.

**2. Placeholder scan:**
- No `TBD`, `TODO`, or vague "add error handling" steps remain.
- Every code step contains the actual code.

**3. Type consistency:**
- `DictationSource` enum values match across service, controller, and JS (`Term`, `Definition`, `ExampleSentence`).
- `CheckAnswerAsync` signature uses `DictationSource source` consistently.
- ViewModel property names (`IsSentenceMode`, `Source`, `ExampleSentence`) match controller mapping and view usage.
