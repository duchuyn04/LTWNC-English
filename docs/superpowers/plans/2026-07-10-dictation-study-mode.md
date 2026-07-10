# Dictation Study Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new "Nghe chép chính tả" (Dictation) study mode where users hear an English term via Web Speech API, type the answer, get instant feedback, and see a session summary screen.

**Architecture:** Server-rendered Razor view with embedded JavaScript data. A new `DictationService` handles answer validation, progress tracking, and session details. `StudyController` exposes endpoints for rendering the page, checking answers, completing the session, and showing results.

**Tech Stack:** ASP.NET Core MVC, Entity Framework Core, SQL Server, Razor, vanilla JavaScript, Web Speech API, xUnit + Moq for testing.

## Global Constraints

- All comments use `//` single-line style, not XML doc comments (`///`), so fresher interns can read them easily.
- Login is required for the Dictation mode (anonymous users are redirected to `/Account/Login`).
- Audio source is the English term (`FrontText`) only.
- Answer validation is case-insensitive, ignores extra spaces/punctuation, and optionally accepts synonyms.
- Follow existing project patterns: `Services/*Service.cs`, `Controllers/StudyController.cs`, `Views/Study/*.cshtml`, `Models/Entities/*.cs`, `Models/ViewModels/Study/*.cs`.
- Use `ValidateAntiForgeryToken` on state-changing POST actions.
- Return JSON for AJAX endpoints and full Views for page endpoints.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Models/Entities/UserStudySettings.cs` | Add dictation-specific settings properties and the `DictationAnswerMode` enum. |
| `Models/Entities/StudySession.cs` | Add `Dictation` to the `StudyMode` enum. |
| `Models/Entities/DictationSessionDetail.cs` | New entity storing each answer attempt during a dictation session. |
| `Models/ViewModels/Study/DictationStudyViewModel.cs` | Data passed to the dictation study view. |
| `Models/ViewModels/Study/DictationResultViewModel.cs` | Data passed to the session result view. |
| `Services/DictationService.cs` | Core business logic: load cards, validate answers, update progress, manage session lifecycle. |
| `Controllers/StudyController.cs` | Add `Dictation`, `DictationCheck`, `DictationComplete`, `DictationResult` actions. |
| `Data/AppDbContext.cs` | Add `DictationSessionDetails` DbSet and relationship configuration. |
| `Migrations/` | EF Core migration generated after model changes. |
| `Views/Study/Dictation.cshtml` | Main dictation study UI with embedded JavaScript. |
| `Views/Study/DictationResult.cshtml` | Session summary / feedback screen. |
| `Views/Study/Index.cshtml` | Add a card that links to the new Dictation mode. |
| `Program.cs` | Register `DictationService` in the DI container. |
| `ltwnc.Tests/ltwnc.Tests.csproj` | New xUnit test project. |
| `ltwnc.Tests/Services/DictationServiceTests.cs` | Unit tests for answer validation and session logic. |
| `ltwnc.Tests/Controllers/StudyControllerDictationTests.cs` | Integration-style tests for the new controller actions. |

---

### Task 1: Add `DictationAnswerMode` enum and extend `UserStudySettings`

**Files:**
- Modify: `Models/Entities/UserStudySettings.cs`

**Interfaces:**
- Produces: `DictationAnswerMode` enum with values `Term` and `Definition`.
- Produces: new boolean/string/float properties on `UserStudySettings` for dictation configuration.

- [ ] **Step 1: Add enum and properties**

Add the enum above the `UserStudySettings` class:

```csharp
// Chế độ trả lờ trong bài nghe chép
// Term: đọc thuật ngữ, nhập thuật ngữ
// Definition: đọc thuật ngữ, nhập nghĩa
public enum DictationAnswerMode
{
    Term,
    Definition
}
```

Add properties inside `UserStudySettings`:

```csharp
// Cài đặt riêng cho chế độ nghe chép
public DictationAnswerMode DictationAnswerMode { get; set; } = DictationAnswerMode.Term;
public bool DictationAutoAdvance { get; set; }
public float DictationPlaybackSpeed { get; set; } = 1.0f;
public string? DictationVoiceUri { get; set; }
public bool DictationShowHint { get; set; } = true;
public bool DictationAcceptSynonyms { get; set; } = true;
public bool DictationShuffle { get; set; }
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Models/Entities/UserStudySettings.cs
git commit -m "feat(dictation): add answer mode enum and user settings

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Add `Dictation` to `StudyMode` and create `DictationSessionDetail` entity

**Files:**
- Modify: `Models/Entities/StudySession.cs`
- Create: `Models/Entities/DictationSessionDetail.cs`

**Interfaces:**
- Produces: `StudyMode.Dictation` enum value.
- Produces: `DictationSessionDetail` entity class.

- [ ] **Step 1: Add `Dictation` to `StudyMode`**

In `Models/Entities/StudySession.cs`:

```csharp
public enum StudyMode
{
    Flashcard, // Lật thẻ
    Quiz,      // Trắc nghiệm
    Write,     // Viết chính tả
    Match,     // Ghép đôi
    Dictation  // Nghe chép chính tả
}
```

- [ ] **Step 2: Create `DictationSessionDetail.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Lưu chi tiết từng câu trả lờ trong một phiên nghe chép
// Một StudySession có nhiều DictationSessionDetail
public class DictationSessionDetail
{
    // Khóa chính tự tăng
    [Key]
    public int Id { get; set; }

    // Khóa ngoại đến phiên học
    [Required]
    public int StudySessionId { get; set; }

    // Khóa ngoại đến thẻ được hỏi
    [Required]
    public int FlashcardId { get; set; }

    // true nếu ngườ dùng trả lờ đúng
    public bool IsCorrect { get; set; }

    // Nội dung ngườ dùng đã nhập
    public string AnsweredText { get; set; } = string.Empty;

    // Thờ điểm trả lờ
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property đến phiên học
    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }

    // Navigation property đến thẻ
    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
```

- [ ] **Step 3: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 4: Commit**

```bash
git add Models/Entities/StudySession.cs Models/Entities/DictationSessionDetail.cs
git commit -m "feat(dictation): add Dictation mode and session detail entity

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Update `AppDbContext` and create EF migration

**Files:**
- Modify: `Data/AppDbContext.cs`
- Create: `Migrations/20260710_AddDictationSessionDetails.cs` and designer files (generated)

**Interfaces:**
- Consumes: `DictationSessionDetail` entity.
- Produces: `DbSet<DictationSessionDetail> DictationSessionDetails`.

- [ ] **Step 1: Add DbSet and configuration**

In `Data/AppDbContext.cs`, add the DbSet property:

```csharp
public DbSet<DictationSessionDetail> DictationSessionDetails => Set<DictationSessionDetail>();
```

Inside `OnModelCreating`, add configuration:

```csharp
// Cấu hình bảng DictationSessionDetails
builder.Entity<DictationSessionDetail>(entity =>
{
    // Index để lấy nhanh các câu trả lờ của một phiên
    entity.HasIndex(e => e.StudySessionId);

    // Quan hệ: nhiều detail thuộc về 1 session
    // Cascade xóa: xóa phiên sẽ xóa luôn chi tiết
    entity.HasOne(e => e.StudySession)
          .WithMany()
          .HasForeignKey(e => e.StudySessionId)
          .OnDelete(DeleteBehavior.Cascade);

    // Quan hệ: nhiều detail thuộc về 1 flashcard
    // Restrict: không cho xóa thẻ nếu còn lịch sử trả lờ
    entity.HasOne(e => e.Flashcard)
          .WithMany()
          .HasForeignKey(e => e.FlashcardId)
          .OnDelete(DeleteBehavior.Restrict);
});
```

- [ ] **Step 2: Generate migration**

Run:
```bash
dotnet ef migrations add AddDictationSessionDetails
```
Expected: migration files created in `Migrations/`.

- [ ] **Step 3: Review generated migration**

Open `Migrations/20260710_AddDictationSessionDetails.cs` and confirm:
- `DictationSessionDetails` table is created.
- Columns match the entity.
- Foreign keys point to `StudySessions` and `Flashcards`.

- [ ] **Step 4: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 5: Commit**

```bash
git add Data/AppDbContext.cs Migrations/
git commit -m "feat(dictation): add DbSet and migration for session details

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Create xUnit test project

**Files:**
- Create: `ltwnc.Tests/ltwnc.Tests.csproj`
- Create: `ltwnc.Tests/Usings.cs`

**Interfaces:**
- Produces: test project referencing the main web project.

- [ ] **Step 1: Create test project directory and file**

```bash
mkdir ltwnc.Tests
```

Create `ltwnc.Tests/ltwnc.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ltwnc.csproj" />
  </ItemGroup>

</Project>
```

Create `ltwnc.Tests/Usings.cs`:

```csharp
// Global using cho toàn bộ test project
global using Xunit;
global using Moq;
global using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 2: Add test project to solution**

Run:
```bash
dotnet sln add ltwnc.Tests/ltwnc.Tests.csproj
```
Expected: test project added to solution.

- [ ] **Step 3: Build test project**

Run:
```bash
dotnet build ltwnc.Tests/ltwnc.Tests.csproj
```
Expected: builds successfully.

- [ ] **Step 4: Commit**

```bash
git add ltwnc.Tests/
git commit -m "test: add xUnit test project

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Implement `DictationService` and unit tests

**Files:**
- Create: `Services/DictationService.cs`
- Create: `ltwnc.Tests/Services/DictationServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `UserStudySettings`, `DictationAnswerMode`, `Flashcard`, `UserProgress`, `StudySession`.
- Produces: `DictationService` with methods:
  - `GetCardsForDictationAsync(int setId, string userId, UserStudySettings settings)` → `List<Flashcard>`
  - `CreateSessionAsync(string userId, int setId)` → `StudySession`
  - `CheckAnswerAsync(int sessionId, int cardId, string answeredText, string userId, DictationAnswerMode mode, bool acceptSynonyms)` → `DictationCheckResult`
  - `CompleteSessionAsync(int sessionId, int score)` → `StudySession`
  - `GetSessionResultAsync(int sessionId, string userId)` → `DictationResult`
- Produces: `DictationCheckResult` and `DictationResult` DTOs.

- [ ] **Step 1: Create result DTOs inside `DictationService.cs`**

```csharp
namespace ltwnc.Services;

// Kết quả trả về khi kiểm tra một đáp án
public class DictationCheckResult
{
    public bool IsCorrect { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Hint { get; set; }
}

// Kết quả tổng kết một phiên nghe chép
public class DictationResult
{
    public int SessionId { get; set; }
    public int TotalCards { get; set; }
    public int CorrectCount { get; set; }
    public int Score { get; set; }
    public List<DictationResultCard> WrongCards { get; set; } = new();
}

// Thông tin một thẻ sai trong màn hình tổng kết
public class DictationResultCard
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Implement `DictationService`**

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ nghe chép chính tả
public class DictationService
{
    private readonly AppDbContext _context;

    // Inject DbContext qua constructor
    public DictationService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách thẻ cho bài nghe chép, áp dụng lọc và xáo trộn
    public async Task<List<Flashcard>> GetCardsForDictationAsync(int setId, string userId, UserStudySettings settings)
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

        var cards = await query.OrderBy(f => f.OrderIndex).ToListAsync();

        // Xáo trộn nếu cài đặt bật
        if (settings.DictationShuffle)
        {
            cards = Shuffle(cards);
        }

        return cards;
    }

    // Xáo trộn danh sách bằng thuật toán Fisher-Yates
    private static List<T> Shuffle<T>(List<T> list)
    {
        var random = new Random();
        var result = new List<T>(list);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    // Tạo phiên học Dictation mới
    public async Task<StudySession> CreateSessionAsync(string userId, int setId)
    {
        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.Dictation,
            CompletedAt = DateTime.UtcNow
        };

        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
        return session;
    }

    // Kiểm tra đáp án của ngườ dùng
    public async Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int cardId,
        string answeredText,
        string userId,
        DictationAnswerMode mode,
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

        // Đáp án đúng tùy theo chế độ trả lờ
        var correctAnswer = mode == DictationAnswerMode.Definition
            ? card.BackText
            : card.FrontText;

        // Tập hợp các đáp án được chấp nhận
        var acceptedAnswers = new List<string> { correctAnswer };

        // Nếu chấp nhận từ đồng nghĩa và đang ở chế độ thuật ngữ
        if (acceptSynonyms && mode == DictationAnswerMode.Term && !string.IsNullOrWhiteSpace(card.Synonyms))
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

    // Chuẩn hóa chuỗi đáp án để so sánh
    private static string NormalizeAnswer(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return input
            .Trim()
            .ToLowerInvariant()
            .Replace(",", "")
            .Replace(".", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace(";", "")
            .Replace("  ", " ");
    }

    // Tạo gợi ý khi trả lờ sai: IPA và nghĩa
    private static string? BuildHint(Flashcard card)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.Pronunciation))
            parts.Add($"IPA: {card.Pronunciation}");
        if (!string.IsNullOrWhiteSpace(card.BackText))
            parts.Add($"Nghĩa: {card.BackText}");
        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    // Cập nhật UserProgress sau mỗi câu trả lờ
    private async Task UpdateUserProgressAsync(string userId, int flashcardId, bool isCorrect)
    {
        var progress = await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            await _context.UserProgresses.AddAsync(progress);
        }

        progress.IsLearned = isCorrect;
        progress.Status = isCorrect ? UserProgressStatus.Mastered : UserProgressStatus.Learning;

        if (isCorrect)
            progress.CorrectCount++;
        else
            progress.WrongCount++;

        progress.LastReviewed = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // Đóng phiên học và lưu điểm
    public async Task<StudySession> CompleteSessionAsync(int sessionId, int score)
    {
        var session = await _context.StudySessions.FindAsync(sessionId);
        if (session == null)
            throw new KeyNotFoundException("Phiên học không tồn tại.");

        session.Score = score;
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return session;
    }

    // Lấy dữ liệu tổng kết phiên học
    public async Task<DictationResult> GetSessionResultAsync(int sessionId, string userId)
    {
        var session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new KeyNotFoundException("Phiên học không tồn tại.");

        if (session.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xem phiên học này.");

        var details = await _context.DictationSessionDetails
            .AsNoTracking()
            .Where(d => d.StudySessionId == sessionId)
            .Include(d => d.Flashcard)
            .ToListAsync();

        var total = details.Count;
        var correct = details.Count(d => d.IsCorrect);

        var wrongCards = details
            .Where(d => !d.IsCorrect && d.Flashcard != null)
            .Select(d => new DictationResultCard
            {
                Id = d.Flashcard!.Id,
                Term = d.Flashcard.FrontText,
                Definition = d.Flashcard.BackText,
                Pronunciation = d.Flashcard.Pronunciation
            })
            .ToList();

        return new DictationResult
        {
            SessionId = sessionId,
            TotalCards = total,
            CorrectCount = correct,
            Score = session.Score ?? 0,
            WrongCards = wrongCards
        };
    }
}
```

- [ ] **Step 3: Write unit tests**

Create `ltwnc.Tests/Services/DictationServiceTests.cs`:

```csharp
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Tests.Services;

public class DictationServiceTests
{
    // Tạo DbContext in-memory mới cho mỗi test
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // Tạo bộ thẻ mẫu
    private async Task<FlashcardSet> SeedSetAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Id = 1,
            Title = "Test Set",
            UserId = "user-1",
            IsPublic = true
        };
        await context.FlashcardSets.AddAsync(set);
        await context.SaveChangesAsync();
        return set;
    }

    // Tạo thẻ mẫu
    private async Task<Flashcard> SeedCardAsync(AppDbContext context, int id, string term, string def, string? synonyms = null)
    {
        var card = new Flashcard
        {
            Id = id,
            FlashcardSetId = 1,
            FrontText = term,
            BackText = def,
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example",
            ExampleMeaning = "Ví dụ",
            Synonyms = synonyms,
            OrderIndex = id
        };
        await context.Flashcards.AddAsync(card);
        await context.SaveChangesAsync();
        return card;
    }

    [Fact]
    public async Task GetCardsForDictationAsync_StarredOnly_ReturnsStarredCards()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");
        await SeedCardAsync(context, 2, "world", "thế giớ");
        context.Flashcards.Find(2)!.IsStarred = true;
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var settings = new UserStudySettings { StarredOnly = true };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Single(result);
        Assert.Equal("world", result[0].FrontText);
    }

    [Fact]
    public async Task CheckAnswerAsync_CorrectExactAnswer_ReturnsIsCorrectTrue()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "hello", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongCaseAndSpaces_Accepted()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "Hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "  HELLO  ", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_SynonymAccepted_WhenEnabled()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large, huge");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "large", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_SynonymRejected_WhenDisabled()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "large", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: false);

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_DefinitionMode_ChecksBackText()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "xin chào", "user-1",
            DictationAnswerMode.Definition, acceptSynonyms: false);

        Assert.True(result.IsCorrect);
        Assert.Equal("xin chào", result.CorrectAnswer);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongAnswer_UpdatesProgressAsLearning()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await service.CheckAnswerAsync(
            session.Id, 1, "wrong", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        var progress = await context.UserProgresses.FirstAsync(p => p.UserId == "user-1" && p.FlashcardId == 1);
        Assert.False(progress.IsLearned);
        Assert.Equal(UserProgressStatus.Learning, progress.Status);
        Assert.Equal(1, progress.WrongCount);
    }

    [Fact]
    public async Task CompleteSessionAsync_SetsScore()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await service.CompleteSessionAsync(session.Id, 85);

        var completed = await context.StudySessions.FindAsync(session.Id);
        Assert.Equal(85, completed!.Score);
    }

    [Fact]
    public async Task GetSessionResultAsync_ReturnsWrongCardsOnly()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);
        await service.CheckAnswerAsync(session.Id, 1, "wrong", "user-1", DictationAnswerMode.Term, true);

        var result = await service.GetSessionResultAsync(session.Id, "user-1");

        Assert.Equal(1, result.TotalCards);
        Assert.Equal(0, result.CorrectCount);
        Assert.Single(result.WrongCards);
    }
}
```

- [ ] **Step 4: Run tests**

Run:
```bash
dotnet test ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~DictationServiceTests"
```
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Services/DictationService.cs ltwnc.Tests/Services/DictationServiceTests.cs
git commit -m "feat(dictation): implement DictationService with unit tests

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: Create ViewModels

**Files:**
- Create: `Models/ViewModels/Study/DictationStudyViewModel.cs`
- Create: `Models/ViewModels/Study/DictationResultViewModel.cs`
- Create: `Models/ViewModels/Study/DictationCardViewModel.cs`

**Interfaces:**
- Produces: view models consumed by `DictationService` mapping and Razor views.

- [ ] **Step 1: Create ViewModels**

`Models/ViewModels/Study/DictationCardViewModel.cs`:

```csharp
namespace ltwnc.Models.ViewModels.Study;

// Thông tin một thẻ hiển thị trong bài nghe chép
public class DictationCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
```

`Models/ViewModels/Study/DictationStudyViewModel.cs`:

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho view học nghe chép
public class DictationStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<DictationCardViewModel> Cards { get; set; } = new();
    public UserStudySettings Settings { get; set; } = new();
    public int SessionId { get; set; }
    public int StreakDays { get; set; }
}
```

`Models/ViewModels/Study/DictationResultViewModel.cs`:

```csharp
namespace ltwnc.Models.ViewModels.Study;

// Dữ liệu truyền cho màn hình tổng kết nghe chép
public class DictationResultViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int TotalCards { get; set; }
    public int CorrectCount { get; set; }
    public int Score { get; set; }
    public List<DictationResultCardViewModel> WrongCards { get; set; } = new();
}

// Thông tin một thẻ cần ôn trong màn hình tổng kết
public class DictationResultCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Models/ViewModels/Study/
git commit -m "feat(dictation): add dictation view models

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: Add controller actions

**Files:**
- Modify: `Controllers/StudyController.cs`

**Interfaces:**
- Consumes: `DictationService`, `DictationStudyViewModel`, `DictationResultViewModel`, `UserStudySettings`.
- Produces: four new action methods for dictation routes.

- [ ] **Step 1: Inject `DictationService`**

Update constructor and fields:

```csharp
private readonly StudyService _studyService;
private readonly DictationService _dictationService;
private readonly FlashcardSetService _setService;
private readonly UserManager<IdentityUser> _userManager;

// Inject các service: học tập, nghe chép, bộ thẻ, UserManager
public StudyController(
    StudyService studyService,
    DictationService dictationService,
    FlashcardSetService setService,
    UserManager<IdentityUser> userManager)
{
    _studyService = studyService;
    _dictationService = dictationService;
    _setService = setService;
    _userManager = userManager;
}
```

- [ ] **Step 2: Add `Dictation` action**

```csharp
// Hiển thị giao diện học nghe chép
// Yêu cầu đăng nhập
[Authorize]
[Route("/Study/{setId}/Dictation")]
public async Task<IActionResult> Dictation(int setId)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
    if (set == null) return NotFound();

    var settings = await _studyService.GetSettingsAsync(user.Id);
    var cards = await _dictationService.GetCardsForDictationAsync(setId, user.Id, settings);

    if (!cards.Any())
    {
        TempData["Message"] = settings.StarredOnly || settings.UnlearnedOnly
            ? "Không có thẻ phù hợp với bộ lọc hiện tại."
            : "Bộ thẻ này chưa có thẻ nào.";
        return RedirectToAction("Index", new { setId });
    }

    var session = await _dictationService.CreateSessionAsync(user.Id, setId);

    var viewModel = new DictationStudyViewModel
    {
        SetId = setId,
        SetTitle = set.Title,
        SessionId = session.Id,
        Settings = settings,
        Cards = cards.Select(c => new DictationCardViewModel
        {
            Id = c.Id,
            Term = c.FrontText,
            Definition = c.BackText,
            Pronunciation = c.Pronunciation,
            ImageUrl = !string.IsNullOrWhiteSpace(c.UploadedImagePath) ? c.UploadedImagePath : c.ImageUrl
        }).ToList()
    };

    return View(viewModel);
}
```

- [ ] **Step 3: Add `DictationCheck` action**

```csharp
// Kiểm tra đáp án nghe chép qua AJAX
[HttpPost]
[Route("/Study/{setId}/Dictation/Check")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DictationCheck(int setId, int sessionId, int cardId, string answeredText)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized();

    try
    {
        var settings = await _studyService.GetSettingsAsync(user.Id);
        var result = await _dictationService.CheckAnswerAsync(
            sessionId, cardId, answeredText, user.Id,
            settings.DictationAnswerMode,
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

- [ ] **Step 4: Add `DictationComplete` action**

```csharp
// Hoàn thành phiên nghe chép
[HttpPost]
[Route("/Study/{setId}/Dictation/Complete")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DictationComplete(int setId, int sessionId, int score)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized();

    try
    {
        await _dictationService.CompleteSessionAsync(sessionId, score);
        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("DictationResult", new { setId, sessionId })!
        });
    }
    catch (KeyNotFoundException)
    {
        return NotFound();
    }
}
```

- [ ] **Step 5: Add `DictationResult` action**

```csharp
// Hiển thị màn hình tổng kết phiên nghe chép
[Authorize]
[Route("/Study/{setId}/Dictation/Result/{sessionId}")]
public async Task<IActionResult> DictationResult(int setId, int sessionId)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var set = await _setService.GetAccessibleSetAsync(setId, user.Id);
    if (set == null) return NotFound();

    try
    {
        var result = await _dictationService.GetSessionResultAsync(sessionId, user.Id);
        var viewModel = new DictationResultViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            SessionId = sessionId,
            TotalCards = result.TotalCards,
            CorrectCount = result.CorrectCount,
            Score = result.Score,
            WrongCards = result.WrongCards.Select(c => new DictationResultCardViewModel
            {
                Id = c.Id,
                Term = c.Term,
                Definition = c.Definition,
                Pronunciation = c.Pronunciation
            }).ToList()
        };

        return View(viewModel);
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

- [ ] **Step 6: Add `[Authorize]` using directive**

Ensure `Microsoft.AspNetCore.Authorization` is available. Add at top of file if missing:

```csharp
using Microsoft.AspNetCore.Authorization;
```

- [ ] **Step 7: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 8: Commit**

```bash
git add Controllers/StudyController.cs
git commit -m "feat(dictation): add dictation controller actions

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: Create `Views/Study/Dictation.cshtml`

**Files:**
- Create: `Views/Study/Dictation.cshtml`

**Interfaces:**
- Consumes: `DictationStudyViewModel`.
- Produces: HTML + embedded JavaScript for the dictation study UI.

- [ ] **Step 1: Create the view**

```razor
@model DictationStudyViewModel
@{
    ViewData["Title"] = "Nghe chép chính tả";
}

<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
<link rel="stylesheet" href="~/css/flashcard.css" asp-append-version="true" />

<div class="study-shell">
    @Html.AntiForgeryToken()

    <section id="dictation-view" class="study-stage" aria-label="Nghe chép chính tả">
        <header class="study-header">
            <a class="exit-link" href="/Study/@Model.SetId" title="Thoát về trang học">
                <span class="material-symbols-outlined">arrow_back</span>
                <span class="exit-label">Quay lại</span>
            </a>

            <div class="set-meta">
                <h1 class="set-title">@Model.SetTitle</h1>
            </div>

            <button type="button" class="icon-button study-settings-button" onclick="toggleDictationSettings(event)" title="Cài đặt nghe chép">
                <span class="material-symbols-outlined">tune</span>
            </button>
        </header>

        <div class="progress-strip">
            <div class="progress-track" aria-hidden="true">
                <div id="dictation-progress-fill" class="progress-fill"></div>
            </div>
            <span id="dictation-progress-text" class="progress-text">Câu 1 / @Model.Cards.Count</span>
        </div>

        <main class="card-area">
            <div class="dictation-card glass-card">
                <button type="button" id="dictation-play-btn" class="dictation-play-btn" onclick="playCurrentTerm()" title="Phát âm (Ctrl + 1)">
                    <span class="material-symbols-outlined">volume_up</span>
                </button>

                <div class="dictation-speed-row">
                    <label for="dictation-speed">Tốc độ</label>
                    <select id="dictation-speed" class="form-select premium-select" onchange="saveTtsSettings()">
                        <option value="0.5">0.5x</option>
                        <option value="0.75">0.75x</option>
                        <option value="1.0" selected>1.0x</option>
                        <option value="1.25">1.25x</option>
                        <option value="1.5">1.5x</option>
                    </select>
                </div>

                <div class="dictation-input-wrap">
                    <textarea id="dictation-answer" class="dictation-answer" rows="2" placeholder="Nhập từ hoặc câu bạn nghe được..."></textarea>
                </div>

                <div id="dictation-feedback" class="dictation-feedback" hidden>
                    <div id="dictation-feedback-icon"></div>
                    <div id="dictation-feedback-answer"></div>
                    <div id="dictation-feedback-hint" class="dictation-hint"></div>
                </div>

                <div class="dictation-actions">
                    <button type="button" id="dictation-check-btn" class="primary-dark" onclick="checkAnswer()">
                        Kiểm tra (Enter)
                    </button>
                    <button type="button" class="control-btn" onclick="markUnknown()">
                        Tôi không biết
                    </button>
                </div>

                <p class="keyboard-hint">
                    <span class="keyboard-group"><kbd>Ctrl</kbd> + <kbd>1</kbd> để nghe lại</span>
                </p>
            </div>
        </main>

        <aside class="dictation-sidebar">
            <div class="sidebar-card glass-card">
                <h3>Tiến độ buổi học</h3>
                <div class="sidebar-row"><span>Đã hoàn thành</span><span id="stat-completed">0 / @Model.Cards.Count</span></div>
                <div class="sidebar-row"><span>Tổng số câu hỏi</span><span id="stat-total">@Model.Cards.Count</span></div>
                <div class="sidebar-row"><span>Tỉ lệ chính xác</span><span id="stat-accuracy">--</span></div>
            </div>

            <div class="sidebar-card glass-card">
                <h3>Chuỗi học tập</h3>
                <p>12 ngày liên tiếp</p>
            </div>

            <a class="sidebar-link" href="/Study/@Model.SetId/History">Xem lại lịch sử câu sai</a>
            <a class="sidebar-link text-danger" href="/Study/@Model.SetId">Kết thúc phiên học</a>
        </aside>
    </section>

    <aside id="dictation-settings-drawer" class="study-settings-drawer" hidden>
        <div class="settings-drawer-header">
            <span class="settings-title">Cài đặt nghe chép</span>
            <button type="button" class="icon-button" onclick="toggleDictationSettings(event)" title="Đóng">
                <span class="material-symbols-outlined">close</span>
            </button>
        </div>
        <div class="settings-drawer-body">
            <div>
                <div class="settings-group-title">Lọc từ vựng</div>
                <label><input type="checkbox" data-setting="StarredOnly"> Chỉ học từ đánh dấu sao</label>
                <label><input type="checkbox" data-setting="UnlearnedOnly"> Chỉ học từ chưa thuộc</label>
            </div>

            <div>
                <div class="settings-group-title">Thứ tự học</div>
                <label><input type="checkbox" data-setting="DictationShuffle"> Xáo trộn</label>
            </div>

            <div>
                <div class="settings-group-title">Chế độ trả lời</div>
                <label><input type="radio" name="answerMode" value="Term" data-setting="DictationAnswerMode" checked> Trả lời bằng thuật ngữ</label>
                <label><input type="radio" name="answerMode" value="Definition" data-setting="DictationAnswerMode"> Trả lời bằng định nghĩa</label>
            </div>

            <div>
                <div class="settings-group-title">Tùy chọn trả lời</div>
                <label><input type="checkbox" data-setting="DictationAcceptSynonyms"> Chấp nhận từ đồng nghĩa</label>
            </div>

            <div>
                <div class="settings-group-title">Tùy chọn hành vi</div>
                <label><input type="checkbox" data-setting="DictationAutoAdvance"> Tự động tiếp tục khi đúng</label>
            </div>

            <div>
                <div class="settings-group-title">Tùy chọn âm thanh</div>
                <div class="tts-row">
                    <label for="dictation-voice">Giọng đọc</label>
                    <select id="dictation-voice" class="form-select premium-select" onchange="saveTtsSettings()"></select>
                </div>
            </div>

            <div>
                <div class="settings-group-title">Gợi ý</div>
                <label><input type="checkbox" data-setting="DictationShowHint"> Hiện gợi ý khi trả lời sai</label>
            </div>
        </div>
    </aside>
    <div id="dictation-settings-backdrop" class="settings-drawer-backdrop" hidden onclick="toggleDictationSettings(event)"></div>
</div>

@section Scripts {
    <script>
        // Dữ liệu thẻ và cài đặt được render từ server
        const cards = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.Cards.Select(c => new
        {
            c.Id,
            c.Term,
            c.Definition,
            c.Pronunciation,
            Image = c.ImageUrl
        })));
        const initialSettings = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.Settings));
        const settings = { ...initialSettings };
        const setId = @Model.SetId;
        const sessionId = @Model.SessionId;

        let currentIndex = 0;
        let answeredCount = 0;
        let correctCount = 0;
        let isAnswered = false;

        const synth = window.speechSynthesis;
        const voiceSelect = document.getElementById('dictation-voice');
        const speedSelect = document.getElementById('dictation-speed');
        const answerInput = document.getElementById('dictation-answer');
        const feedbackPanel = document.getElementById('dictation-feedback');

        let voices = [];

        // Tải danh sách giọng đọc tiếng Anh
        function loadVoices() {
            if (!synth || !voiceSelect) return;
            voices = synth.getVoices();
            voiceSelect.innerHTML = '';

            const enVoices = voices.filter(v => v.lang.startsWith('en'));
            if (enVoices.length === 0) {
                const opt = document.createElement('option');
                opt.value = 'default';
                opt.textContent = 'Mặc định hệ thống';
                voiceSelect.appendChild(opt);
                voiceSelect.disabled = true;
            } else {
                enVoices.forEach(voice => {
                    const opt = document.createElement('option');
                    opt.value = voice.voiceURI;
                    opt.textContent = `${voice.name} (${voice.lang})`;
                    voiceSelect.appendChild(opt);
                });
                voiceSelect.disabled = false;
            }

            restoreTtsSettings();
        }

        if (synth) {
            if (synth.onvoiceschanged !== undefined) {
                synth.onvoiceschanged = loadVoices;
            }
            loadVoices();
        }

        // Lưu cài đặt TTS vào localStorage
        function saveTtsSettings() {
            const data = {
                speed: speedSelect.value,
                voiceUri: voiceSelect.value
            };
            localStorage.setItem('ltwnc_dictation_tts', JSON.stringify(data));
        }

        // Khôi phục cài đặt TTS từ localStorage
        function restoreTtsSettings() {
            const raw = localStorage.getItem('ltwnc_dictation_tts');
            if (!raw) return;
            try {
                const data = JSON.parse(raw);
                if (data.speed) speedSelect.value = data.speed;
                if (data.voiceUri) {
                    for (let i = 0; i < voiceSelect.options.length; i++) {
                        if (voiceSelect.options[i].value === data.voiceUri) {
                            voiceSelect.selectedIndex = i;
                            break;
                        }
                    }
                }
            } catch (e) {
                console.error('Không đọc được cài đặt TTS', e);
            }
        }

        // Phát âm thuật ngữ hiện tại
        function playCurrentTerm() {
            if (!synth || cards.length === 0) return;
            const term = cards[currentIndex].Term;
            speak(term);
        }

        // Phát âm một chuỗi văn bản
        function speak(text) {
            if (!synth || !text) return;
            if (synth.speaking) synth.cancel();

            const utterance = new SpeechSynthesisUtterance(text);
            utterance.rate = parseFloat(speedSelect.value) || 1.0;
            utterance.lang = 'en-US';

            const selectedUri = voiceSelect.value;
            const selectedVoice = voices.find(v => v.voiceURI === selectedUri);
            if (selectedVoice) utterance.voice = selectedVoice;

            synth.speak(utterance);
        }

        // Kiểm tra đáp án hiện tại
        async function checkAnswer() {
            if (cards.length === 0 || isAnswered) return;

            const value = answerInput.value.trim();
            if (!value) {
                answerInput.focus();
                return;
            }

            await submitCheck(value);
        }

        // Gửi đáp án lên server
        async function submitCheck(value) {
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            const params = new URLSearchParams();
            params.append('__RequestVerificationToken', token);
            params.append('sessionId', sessionId);
            params.append('cardId', cards[currentIndex].Id);
            params.append('answeredText', value);

            try {
                const response = await fetch(`/Study/${setId}/Dictation/Check`, {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body: params
                });

                if (!response.ok) {
                    console.error('Lỗi kiểm tra đáp án', response.status);
                    return;
                }

                const data = await response.json();
                handleCheckResult(data);
            } catch (err) {
                console.error('Lỗi mạng khi kiểm tra đáp án', err);
            }
        }

        // Xử lý kết quả trả về từ server
        function handleCheckResult(data) {
            isAnswered = true;
            answeredCount++;
            if (data.isCorrect) correctCount++;

            feedbackPanel.hidden = false;
            const icon = document.getElementById('dictation-feedback-icon');
            const answer = document.getElementById('dictation-feedback-answer');
            const hint = document.getElementById('dictation-feedback-hint');

            if (data.isCorrect) {
                feedbackPanel.className = 'dictation-feedback is-correct';
                icon.innerHTML = '<span class="material-symbols-outlined">check_circle</span>';
                answer.textContent = 'Chính xác!';
                hint.textContent = '';
            } else {
                feedbackPanel.className = 'dictation-feedback is-wrong';
                icon.innerHTML = '<span class="material-symbols-outlined">cancel</span>';
                answer.textContent = `Đáp án đúng: ${data.correctAnswer}`;
                hint.textContent = settings.DictationShowHint ? (data.hint || '') : '';
            }

            updateStats();

            if (data.isCorrect && settings.DictationAutoAdvance) {
                setTimeout(nextCard, 1000);
            }
        }

        // Ngườ dùng bấm "Tôi không biết"
        function markUnknown() {
            if (cards.length === 0 || isAnswered) return;
            submitCheck('');
        }

        // Chuyển sang câu tiếp theo
        function nextCard() {
            if (currentIndex < cards.length - 1) {
                currentIndex++;
                resetCard();
                playCurrentTerm();
            } else {
                completeSession();
            }
        }

        // Reset trạng thái câu hiện tại
        function resetCard() {
            isAnswered = false;
            answerInput.value = '';
            feedbackPanel.hidden = true;
            answerInput.focus();
            updateProgress();
        }

        // Cập nhật thanh tiến độ
        function updateProgress() {
            const total = cards.length;
            const current = currentIndex + 1;
            document.getElementById('dictation-progress-text').textContent = `Câu ${current} / ${total}`;
            document.getElementById('dictation-progress-fill').style.width = `${(current / total) * 100}%`;
        }

        // Cập nhật số liệu sidebar
        function updateStats() {
            document.getElementById('stat-completed').textContent = `${answeredCount} / ${cards.length}`;
            const accuracy = answeredCount === 0 ? '--' : `${Math.round((correctCount / answeredCount) * 100)}%`;
            document.getElementById('stat-accuracy').textContent = accuracy;
        }

        // Hoàn thành phiên và gửi kết quả lên server
        async function completeSession() {
            const score = cards.length === 0 ? 0 : Math.round((correctCount / cards.length) * 100);
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            const params = new URLSearchParams();
            params.append('__RequestVerificationToken', token);
            params.append('sessionId', sessionId);
            params.append('score', score);

            try {
                const response = await fetch(`/Study/${setId}/Dictation/Complete`, {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body: params
                });

                const data = await response.json();
                if (data.success && data.redirectUrl) {
                    window.location.href = data.redirectUrl;
                }
            } catch (err) {
                console.error('Lỗi khi hoàn thành phiên', err);
            }
        }

        // Mở/đóng modal cài đặt
        function toggleDictationSettings(event) {
            if (event) event.stopPropagation();
            const drawer = document.getElementById('dictation-settings-drawer');
            const backdrop = document.getElementById('dictation-settings-backdrop');
            const isOpen = !drawer.hidden;
            drawer.hidden = isOpen;
            backdrop.hidden = isOpen;
        }

        // Liên kết các input cài đặt với đối tượng settings
        function bindSettingsInputs() {
            document.querySelectorAll('[data-setting]').forEach(input => {
                const key = input.dataset.setting;
                if (input.type === 'radio') {
                    input.checked = settings[key] === input.value;
                    input.addEventListener('change', () => {
                        if (input.checked) {
                            settings[key] = input.value;
                            saveSettings();
                        }
                    });
                } else {
                    input.checked = !!settings[key];
                    input.addEventListener('change', () => {
                        settings[key] = input.checked;
                        if (key === 'StarredOnly' || key === 'UnlearnedOnly') {
                            saveSettings().then(() => reloadWithFilters());
                        } else {
                            saveSettings();
                        }
                    });
                }
            });

            document.addEventListener('click', (e) => {
                const drawer = document.getElementById('dictation-settings-drawer');
                const btn = document.querySelector('.study-settings-button');
                if (drawer && !drawer.hidden && !drawer.contains(e.target) && !btn.contains(e.target)) {
                    toggleDictationSettings();
                }
            });
        }

        // Gửi cài đặt lên server
        async function saveSettings() {
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            const params = new URLSearchParams();
            params.append('__RequestVerificationToken', token);
            Object.keys(settings).forEach(key => {
                params.append(key, settings[key]);
            });

            try {
                await fetch('/Study/Settings', {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body: params
                });
            } catch (err) {
                console.error('Lỗi lưu cài đặt', err);
            }
        }

        // Reload trang với bộ lọc mới
        function reloadWithFilters() {
            const url = new URL(window.location.href);
            url.searchParams.set('starredOnly', settings.StarredOnly);
            url.searchParams.set('unlearnedOnly', settings.UnlearnedOnly);
            window.location.href = url.toString();
        }

        // Bắt phím tắt
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === '1') {
                e.preventDefault();
                playCurrentTerm();
            } else if (e.key === 'Enter' && !e.shiftKey && document.activeElement === answerInput) {
                e.preventDefault();
                checkAnswer();
            }
        });

        bindSettingsInputs();
        updateProgress();
        updateStats();
        answerInput.focus();

        // Phát âm tự động câu đầu tiên sau lần tương tác đầu tiên
        document.body.addEventListener('click', () => {
            if (currentIndex === 0 && !isAnswered) {
                playCurrentTerm();
            }
        }, { once: true });
    </script>
}
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Views/Study/Dictation.cshtml
git commit -m "feat(dictation): add dictation study view

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 9: Create `Views/Study/DictationResult.cshtml`

**Files:**
- Create: `Views/Study/DictationResult.cshtml`

**Interfaces:**
- Consumes: `DictationResultViewModel`.
- Produces: session summary / feedback screen.

- [ ] **Step 1: Create the result view**

```razor
@model DictationResultViewModel
@{
    ViewData["Title"] = "Kết quả nghe chép";
}

<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
<link rel="stylesheet" href="~/css/flashcard.css" asp-append-version="true" />

<div class="completion-view" aria-label="Kết quả nghe chép">
    <div class="completion-inner">
        <span class="badge badge-green">Hoàn thành</span>
        <h2 class="completion-title">Bạn đã hoàn thành tất cả thẻ!</h2>

        <div class="stats-ring">
            <span class="stats-ring-number">@Model.Score%</span>
            <span class="stats-ring-label">Độ chính xác</span>
            <span class="stats-ring-sub">@Model.CorrectCount đúng · @(Model.TotalCards - Model.CorrectCount) cần ôn</span>
        </div>

        <div class="stats-grid">
            <div class="stat-card">
                <span class="stat-number">@Model.TotalCards</span>
                <span class="stat-label">Tổng thể</span>
            </div>
            <div class="stat-card is-green">
                <span class="stat-number">@Model.CorrectCount</span>
                <span class="stat-label">Đã thuộc</span>
            </div>
            <div class="stat-card is-red">
                <span class="stat-number">@(Model.TotalCards - Model.CorrectCount)</span>
                <span class="stat-label">Cần ôn</span>
            </div>
        </div>

        @if (Model.WrongCards.Any())
        {
            <div class="wrong-cards glass-card">
                <h3>@(Model.WrongCards.Count) thẻ cần ôn lại:</h3>
                <div class="wrong-card-list">
                    @foreach (var card in Model.WrongCards)
                    {
                        <div class="wrong-card-chip">
                            <span class="wrong-card-term">@card.Term</span>
                            <span class="wrong-card-pronunciation">@card.Pronunciation</span>
                            <span class="wrong-card-definition">@card.Definition</span>
                        </div>
                    }
                </div>
                <p class="wrong-cards-hint">Gợi ý: Ôn lại @(Model.WrongCards.Count) thẻ chưa thuộc trước khi chuyển sang kiểm tra.</p>
            </div>
        }

        <div class="completion-actions">
            <a class="study-button is-red" href="/Study/@Model.SetId/Dictation?starredOnly=false&unlearnedOnly=false">
                Ôn lại @(Model.WrongCards.Count) thẻ cần nhớ
            </a>
            <a class="study-button" href="/Study/@Model.SetId/Quiz">
                Chuyển sang kiểm tra
            </a>
            <a class="study-button" href="/Study/@Model.SetId/Dictation">
                Học lại toàn bộ
            </a>
            <a class="primary-dark" href="/Study/@Model.SetId">
                Về bộ từ vựng
            </a>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Views/Study/DictationResult.cshtml
git commit -m "feat(dictation): add dictation result view

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 10: Add Dictation card to `/Study/{setId}` index

**Files:**
- Modify: `Views/Study/Index.cshtml`

**Interfaces:**
- Produces: visible "Nghe chép" study mode card linking to `/Study/{setId}/Dictation`.

- [ ] **Step 1: Replace the placeholder Write card**

In `Views/Study/Index.cshtml`, find the "Viết" placeholder block (around lines 52-58) and replace with a Dictation card:

```html
<div class="col-md-6">
    <a href="/Study/@setId/Dictation" class="text-decoration-none">
        <div class="card-custom text-center py-5">
            <i class="ph ph-headphones" style="font-size: 3rem;"></i>
            <h5 class="mt-3 mb-1" style="font-weight: 600;">Nghe chép</h5>
            <p style="color: #787774; font-size: 0.875rem; margin: 0;">Nghe và viết lại từ</p>
        </div>
    </a>
</div>
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Views/Study/Index.cshtml
git commit -m "feat(dictation): add dictation card to study index

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 11: Register `DictationService` in DI

**Files:**
- Modify: `Program.cs`

**Interfaces:**
- Produces: `DictationService` available via constructor injection.

- [ ] **Step 1: Add DI registration**

In `Program.cs`, after `builder.Services.AddScoped<StudyService>();` add:

```csharp
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<DictationService>();
```

- [ ] **Step 2: Build the project**

Run: `dotnet build`
Expected: builds successfully.

- [ ] **Step 3: Commit**

```bash
git add Program.cs
git commit -m "feat(dictation): register DictationService in DI

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 12: Add controller integration tests

**Files:**
- Create: `ltwnc.Tests/Controllers/StudyControllerDictationTests.cs`

**Interfaces:**
- Consumes: `StudyController`, `DictationService`, `StudyService`, `FlashcardSetService`, `UserManager<IdentityUser>`.
- Produces: tests verifying routing, authorization, and JSON responses.

- [ ] **Step 1: Create integration tests**

```csharp
using System.Security.Claims;
using System.Text.Json;
using ltwnc.Controllers;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Controllers;

public class StudyControllerDictationTests
{
    // Tạo user giả lập cho Controller
    private ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth"));
    }

    // Tạo controller với các dependency in-memory
    private StudyController CreateController(AppDbContext context, string userId)
    {
        var userStore = new Mock<IUserStore<IdentityUser>>();
        var userManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        var user = new IdentityUser { Id = userId, UserName = "test@example.com" };
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        // Mock IWebHostEnvironment để FlashcardSetService không cần web root thật
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), "ltwnc-tests"));

        var setService = new FlashcardSetService(context, environment.Object);
        var studyService = new StudyService(context);
        var dictationService = new DictationService(context);

        var controller = new StudyController(studyService, dictationService, setService, userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(userId) }
            }
        };
        return controller;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedSetAndCardAsync(AppDbContext context)
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
            ExampleMeaning = "Xin chào, thế giớ!",
            OrderIndex = 0
        };
        await context.Flashcards.AddAsync(card);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Dictation_Get_ReturnsViewWithModel()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var result = await controller.Dictation(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<DictationStudyViewModel>(viewResult.Model);
    }

    [Fact]
    public async Task DictationCheck_Post_CorrectAnswer_ReturnsSuccess()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);

        var result = await controller.DictationCheck(1, viewModel.SessionId, 1, "hello");

        var jsonResult = Assert.IsType<JsonResult>(result);
        var element = JsonSerializer.SerializeToElement(jsonResult.Value);
        Assert.True(element.GetProperty("success").GetBoolean());
        Assert.True(element.GetProperty("isCorrect").GetBoolean());
    }

    [Fact]
    public async Task DictationComplete_Post_ReturnsRedirectUrl()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);

        var result = await controller.DictationComplete(1, viewModel.SessionId, 100);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var element = JsonSerializer.SerializeToElement(jsonResult.Value);
        Assert.True(element.GetProperty("success").GetBoolean());
        Assert.Contains("DictationResult", element.GetProperty("redirectUrl").GetString());
    }

    [Fact]
    public async Task DictationResult_Get_ReturnsViewWithModel()
    {
        await using var context = CreateContext();
        await SeedSetAndCardAsync(context);

        var controller = CreateController(context, "user-1");
        var dictationResult = await controller.Dictation(1);
        var viewModel = Assert.IsType<DictationStudyViewModel>(Assert.IsType<ViewResult>(dictationResult).Model);
        await controller.DictationCheck(1, viewModel.SessionId, 1, "hello");
        await controller.DictationComplete(1, viewModel.SessionId, 100);

        var result = await controller.DictationResult(1, viewModel.SessionId);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DictationResultViewModel>(viewResult.Model);
        Assert.Equal(1, model.TotalCards);
        Assert.Equal(1, model.CorrectCount);
    }
}
```

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test ltwnc.Tests/ltwnc.Tests.csproj
```
Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add ltwnc.Tests/Controllers/StudyControllerDictationTests.cs
git commit -m "test(dictation): add controller integration tests

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 13: Apply migration and manually test

**Files:**
- No file changes.

**Interfaces:**
- Consumes: all previous tasks.

- [ ] **Step 1: Apply migration to development database**

Run:
```bash
dotnet ef database update
```
Expected: database schema updated.

- [ ] **Step 2: Start the application**

Run:
```bash
dotnet run
```

- [ ] **Step 3: Manual test checklist**

Open browser at `https://localhost:<port>/Study/1` and verify:
1. Card "Nghe chép" is visible and clickable.
2. `/Study/1/Dictation` loads with audio button, textarea, check button.
3. Clicking play button reads the term aloud.
4. Typing correct answer and pressing Enter shows green feedback.
5. Typing wrong answer shows red feedback with correct answer and hint.
6. "Tôi không biết" marks the card wrong.
7. Settings drawer opens and saves changes.
8. Speed/voice selectors affect playback.
9. Auto-advance moves to next card when enabled.
10. Completion redirects to result screen showing score and wrong cards.
11. Result screen buttons navigate correctly.

- [ ] **Step 4: Stop application**

Press `Ctrl + C` in the terminal.

- [ ] **Step 5: Commit any final fixes**

If no code changes are needed, skip commit. Otherwise:

```bash
git add .
git commit -m "fix(dictation): address manual test findings

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Plan Self-Review

### Spec Coverage Check

| Spec Requirement | Task |
|------------------|------|
| New `Dictation` study mode | Task 2, 10 |
| `UserStudySettings` extensions | Task 1 |
| `DictationSessionDetail` entity | Task 2, 3 |
| `DictationService` with validation | Task 5 |
| Controller endpoints | Task 7 |
| `Dictation.cshtml` study UI | Task 8 |
| Inline feedback after each answer | Task 8 |
| `DictationResult.cshtml` summary | Task 9 |
| Settings modal | Task 8 |
| Web Speech API audio | Task 8 |
| Login required | Task 7 |
| Synonym acceptance | Task 5 |
| Term / Definition answer mode | Task 1, 5 |
| Unit + integration tests | Task 4, 5, 12 |
| `//` style comments | All tasks (Global Constraints) |

### Placeholder Scan

No TBD, TODO, "implement later", or vague steps remain. Every task contains exact file paths, code, commands, and expected output.

### Type Consistency Check

- `DictationAnswerMode` enum defined in Task 1 and used in Tasks 5, 7, 8.
- `DictationCheckResult` and `DictationResult` DTOs defined in Task 5 and used in Tasks 7, 9.
- `DictationStudyViewModel.SessionId` passed from controller (Task 7) to view (Task 8).
- `DictationResultViewModel` populated in controller (Task 7) and rendered in view (Task 9).

### Gaps

- CSS styling for new dictation-specific classes (`.dictation-card`, `.dictation-feedback`, etc.) is not included in this plan. Add a Task 8.5 or extend Task 8 to write styles in `wwwroot/css/flashcard.css` if the existing classes are insufficient.
- No explicit task for handling browser `speechSynthesis` unavailability. This is covered in the manual test checklist (Task 13) and should be handled in Task 8 JavaScript.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-10-dictation-study-mode.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach would you like?
