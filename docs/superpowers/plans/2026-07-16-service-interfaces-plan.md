# Service Interfaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thêm interface cho toàn bộ application service (và `CardActionCommandFactory`), cho class concrete implement interface đó, rồi đổi DI + consumer sang inject interface — để sau này có thể thay implementation hoặc bọc decorator mà không sửa controller.

**Architecture:** Mỗi application service hiện tại (concrete class) sẽ có một interface tương ứng, mirror toàn bộ public method. Interface đặt file riêng cạnh implementation, cùng namespace. Controllers và service phụ thuộc lẫn nhau inject interface. `Program.cs` đăng ký `AddScoped<Interface, Implementation>()`. Không đổi logic nghiệp vụ, không đổi chữ ký method, không rewrite test sang mock.

**Tech Stack:** ASP.NET Core MVC (.NET 10), C#, dependency injection built-in, xUnit (test suite hiện có).

**Spec:** `docs/superpowers/specs/2026-07-16-service-interfaces-design.md`

---

## Global Constraints

- **Không đổi** thân method (body) của service. Chỉ thêm `: IXxx` và đổi kiểu field/constructor parameter.
- **Không** tách interface nhỏ theo ISP. Mỗi service = một interface, đủ public method hiện có.
- **Không** tạo decorator class thật trong plan này.
- **Không** bắt buộc viết test mock mới. Test đang `new ConcreteService(...)` phải vẫn compile.
- **Không** biến DTO / helper thành interface (`DictationCheckResult`, `AchievementPageModel`, snapshot, …).
- **Không** sửa đăng ký GoF đã có (`IStudyModeStrategy`, `IStudyEventPublisher`, …) trừ khi vô tình đụng file — giữ nguyên.
- Lifetime: **Scoped** cho cả 8 cặp interface/implementation.
- Comment `//` tiếng Việt hiện có: giữ nguyên; interface mới có thể thêm 1–2 dòng comment ngắn kiểu file GoF hiện tại nếu muốn, không bắt buộc dài.
- Mọi bước code phải **copy đúng signature** từ class thật lúc implement (nếu code đã lệch so với plan do commit sau spec, ưu tiên **code hiện tại trên disk**).

---

## File Structure (tổng quan)

| File | Việc sẽ làm |
|------|-------------|
| `Services/IFlashcardSetService.cs` | **Tạo mới** — contract CRUD bộ thẻ / thẻ |
| `Services/FlashcardSetService.cs` | **Sửa** — `class FlashcardSetService : IFlashcardSetService` |
| `Services/IStudyService.cs` | **Tạo mới** — contract học flashcard / hub / progress |
| `Services/StudyService.cs` | **Sửa** — implement `IStudyService` |
| `Services/IDictationService.cs` | **Tạo mới** — contract nghe chép |
| `Services/DictationService.cs` | **Sửa** — implement `IDictationService` |
| `Services/ICardActionService.cs` | **Tạo mới** — namespace `ltwnc.Services.CardActions` |
| `Services/CardActionService.cs` | **Sửa** — implement interface + inject `ICardActionCommandFactory` |
| `Services/CardActions/ICardActionCommandFactory.cs` | **Tạo mới** |
| `Services/CardActions/CardActionCommandFactory.cs` | **Sửa** — implement interface |
| `Services/IAchievementService.cs` | **Tạo mới** |
| `Services/AchievementService.cs` | **Sửa** — implement + inject interface progress/unlock |
| `Services/IAchievementProgressService.cs` | **Tạo mới** |
| `Services/AchievementProgressService.cs` | **Sửa** — implement interface |
| `Services/IAchievementUnlockService.cs` | **Tạo mới** |
| `Services/AchievementUnlockService.cs` | **Sửa** — implement + inject `IAchievementProgressService` |
| `Services/StudyEvents/AchievementStudyObserver.cs` | **Sửa** — inject `IAchievementUnlockService` |
| `Controllers/FlashcardSetController.cs` | **Sửa** — inject `IFlashcardSetService` |
| `Controllers/HomeController.cs` | **Sửa** — inject `IFlashcardSetService` |
| `Controllers/StudyController.cs` | **Sửa** — inject 3 interface service |
| `Controllers/CardActionsController.cs` | **Sửa** — inject 3 interface |
| `Controllers/AchievementsController.cs` | **Sửa** — inject `IAchievementService` |
| `Program.cs` | **Sửa** — 8 dòng `AddScoped<I, Impl>()` |
| `README.md` | **Tùy chọn** — 1 đoạn ngắn về application service interfaces |
| `tests/**` | **Không bắt buộc sửa** (vẫn `new Concrete(...)`) |

---

## Thứ tự thực hiện (tại sao xếp vậy)

1. Tạo interface + cho class implement trước → project vẫn build (consumer còn dùng concrete).
2. Đổi consumer nội bộ (service → service, observer) sang interface.
3. Đổi controller sang interface.
4. Đổi `Program.cs` DI sang `AddScoped<I, Impl>()`.
5. Build + chạy toàn bộ test.

Không đổi DI trước khi controller/consumer đã nhận interface, nếu không request sẽ fail resolve.

---

### Task 1: Interface và implement cho `FlashcardSetService`

**Mục đích task này:**  
`FlashcardSetService` là service lớn nhất (CRUD set/card, copy public). Task này chỉ tạo contract và gắn class vào contract. Chưa đụng controller hay DI.

**Files:**
- Create: `Services/IFlashcardSetService.cs`
- Modify: `Services/FlashcardSetService.cs` (chỉ dòng khai báo class + usings nếu cần)

**Interfaces:**
- Consumes: (không — task nền)
- Produces: `IFlashcardSetService` với đủ public method của `FlashcardSetService`

- [ ] **Step 1: Tạo file interface đầy đủ**

Tạo file `Services/IFlashcardSetService.cs` với nội dung sau (hoặc tương đương: signature phải khớp class hiện tại trên disk).

```csharp
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services;

// Contract CRUD bộ thẻ / thẻ / copy public set.
// Controller và consumer khác inject interface này thay vì FlashcardSetService concrete.
public interface IFlashcardSetService
{
    Task<List<FlashcardSet>> GetMySetsAsync(string userId);

    Task<List<FlashcardSetListItemViewModel>> GetMySetsWithProgressAsync(string userId);

    Task<List<FlashcardSet>> GetPublicSetsAsync();

    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);

    Task<FlashcardSet?> GetSetByIdAsync(int id);

    Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId);

    Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId);

    Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId);

    Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId);

    Task<FlashcardSet?> GetExistingCopyAsync(int sourceSetId, string learnerId);

    Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId);

    Task<FlashcardSet> CreateSetAsync(
        string title,
        string? description,
        bool isPublic,
        string userId);

    Task UpdateSetAsync(
        int id,
        string title,
        string? description,
        bool isPublic,
        string userId);

    Task DeleteSetAsync(int id, string userId);

    Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool isStarred,
        string userId);

    Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool removeUploadedImage,
        bool isStarred,
        string userId);

    Task<int> DeleteCardAsync(int cardId, string userId);

    Task<bool> ToggleStarAsync(int cardId, string userId);
}
```

**Lưu ý quan trọng:**
- **Không** đưa method `private` / `private static` vào interface (ví dụ `RequiredText`, `OptionalText`, `SaveImageAsync` nếu đang private).
- **Không** đưa constructor vào interface.

- [ ] **Step 2: Cho class implement interface**

Trong `Services/FlashcardSetService.cs`, đổi dòng khai báo class từ:

```csharp
public class FlashcardSetService
```

thành:

```csharp
public class FlashcardSetService : IFlashcardSetService
```

Không sửa body method nào.

- [ ] **Step 3: Build để kiểm tra signature khớp**

Chạy:

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** Build thành công (0 Error).  
**Nếu lỗi kiểu “does not implement interface member …”:** so sánh signature method trên class với interface (tên, kiểu trả về, tham số, nullable) và sửa **interface** cho khớp class — không đổi behavior class.

- [ ] **Step 4: Commit**

```powershell
git add Services/IFlashcardSetService.cs Services/FlashcardSetService.cs
git commit -m "refactor: add IFlashcardSetService and implement on FlashcardSetService"
```

---

### Task 2: Interface và implement cho `StudyService`

**Mục đích task này:**  
Tách contract cho nghiệp vụ học (settings, progress, Study Hub, mark learned, complete session). Strategy / Observer bên trong class **không** đưa vào interface — chúng là dependency private.

**Files:**
- Create: `Services/IStudyService.cs`
- Modify: `Services/StudyService.cs` (chỉ khai báo class)

**Interfaces:**
- Consumes: (không)
- Produces: `IStudyService`

- [ ] **Step 1: Tạo file `Services/IStudyService.cs`**

```csharp
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services;

// Contract nghiệp vụ học: settings, tiến độ, hub, đánh dấu thuộc, hoàn thành phiên.
// Không chứa thẻ mode (strategy) và không mở huy hiệu (observer) — những việc đó nằm trong implementation.
public interface IStudyService
{
    Task<List<Flashcard>> GetCardsForModeAsync(
        StudyMode mode,
        int setId,
        UserStudySettings settings,
        string? userId);

    Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId);

    Task<UserStudySettings> GetSettingsAsync(string? userId);

    Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input);

    Task SaveFilterSettingsAsync(string userId, bool? starredOnly, bool? unlearnedOnly);

    Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned);

    Task CompleteSessionAsync(string userId, int setId, StudyMode mode);

    Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId);
}
```

Nếu compiler báo thiếu using cho `StudyMode` (enum nằm trong `ltwnc.Models.Entities` hoặc namespace khác trên disk), bổ sung `using` đúng theo file `StudyService.cs` hiện tại.

- [ ] **Step 2: Gắn implement**

Đổi:

```csharp
public class StudyService
```

thành:

```csharp
public class StudyService : IStudyService
```

- [ ] **Step 3: Build**

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** 0 Error.

- [ ] **Step 4: Commit**

```powershell
git add Services/IStudyService.cs Services/StudyService.cs
git commit -m "refactor: add IStudyService and implement on StudyService"
```

---

### Task 3: Interface và implement cho `DictationService`

**Mục đích task này:**  
Contract nghe chép. Các class DTO trong cùng file (`DictationCheckResult`, `DictationResult`, …) **giữ nguyên class**, chỉ xuất hiện trong signature interface.

**Files:**
- Create: `Services/IDictationService.cs`
- Modify: `Services/DictationService.cs`

**Interfaces:**
- Consumes: (không)
- Produces: `IDictationService`

- [ ] **Step 1: Tạo file `Services/IDictationService.cs`**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Contract nghe chép: lấy thẻ, tạo phiên, chấm đáp án, hoàn thành, xem kết quả.
// DTO (DictationCheckResult, DictationResult, …) vẫn là class concrete trong DictationService.cs.
public interface IDictationService
{
    Task<List<Flashcard>> GetCardsForDictationAsync(
        int setId,
        string userId,
        UserStudySettings settings);

    Task<bool> AnyCardHasExampleSentenceAsync(int setId);

    Task<StudySession> CreateSessionAsync(
        string userId,
        int setId,
        DictationContentMode contentMode = DictationContentMode.Vocabulary);

    Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int cardId,
        string answeredText,
        string userId,
        bool acceptSynonyms);

    Task<StudySession> CompleteSessionAsync(int sessionId, int score);

    Task<DictationResult> GetSessionResultAsync(int sessionId, string userId);
}
```

**Lưu ý:**  
`DictationCheckResult` và `DictationResult` nằm cùng namespace `ltwnc.Services` (cùng file `DictationService.cs`), nên interface cùng namespace dùng được mà không cần file using thêm.  
**Không** đưa method `private static Shuffle` vào interface.

- [ ] **Step 2: Gắn implement**

```csharp
public class DictationService : IDictationService
```

- [ ] **Step 3: Build**

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** 0 Error.

- [ ] **Step 4: Commit**

```powershell
git add Services/IDictationService.cs Services/DictationService.cs
git commit -m "refactor: add IDictationService and implement on DictationService"
```

---

### Task 4: Interface cho Card Actions (`ICardActionService` + `ICardActionCommandFactory`)

**Mục đích task này:**  
Hai type thuộc namespace `ltwnc.Services.CardActions` (lưu ý: `CardActionService.cs` nằm ở `Services/` root nhưng namespace vẫn là `CardActions`). Factory map action string → command; service execute/undo/log.

**Files:**
- Create: `Services/ICardActionService.cs`
- Create: `Services/CardActions/ICardActionCommandFactory.cs`
- Modify: `Services/CardActionService.cs`
- Modify: `Services/CardActions/CardActionCommandFactory.cs`

**Interfaces:**
- Consumes: `ICardActionCommand` (đã có sẵn)
- Produces: `ICardActionService`, `ICardActionCommandFactory`

- [ ] **Step 1: Tạo `Services/CardActions/ICardActionCommandFactory.cs`**

```csharp
namespace ltwnc.Services.CardActions;

// Map action type từ form/API ("Delete" | "Star" | "Unstar") sang ICardActionCommand.
public interface ICardActionCommandFactory
{
    ICardActionCommand Create(
        string actionType,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds);
}
```

- [ ] **Step 2: Tạo `Services/ICardActionService.cs`**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

// Chạy command batch, ghi log, undo theo log + snapshot.
public interface ICardActionService
{
    Task<CardActionLog> ExecuteAsync(ICardActionCommand command);

    Task UndoAsync(int logId, string userId);

    Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(
        int setId,
        string userId,
        int limit = 5);

    Task<CardActionLog?> GetLogByIdAsync(int logId, string userId);
}
```

- [ ] **Step 3: Implement trên factory**

Trong `CardActionCommandFactory.cs`, đổi:

```csharp
public class CardActionCommandFactory
```

thành:

```csharp
public class CardActionCommandFactory : ICardActionCommandFactory
```

Body method `Create` **không đổi**.

- [ ] **Step 4: Implement trên CardActionService và đổi field factory sang interface**

Hiện tại class có dạng:

```csharp
public class CardActionService
{
    private readonly AppDbContext _context;
    private readonly CardActionCommandFactory _commandFactory;

    public CardActionService(AppDbContext context, CardActionCommandFactory commandFactory)
    {
        _context = context;
        _commandFactory = commandFactory;
    }
    // ...
}
```

Đổi thành:

```csharp
public class CardActionService : ICardActionService
{
    private readonly AppDbContext _context;
    private readonly ICardActionCommandFactory _commandFactory;

    public CardActionService(AppDbContext context, ICardActionCommandFactory commandFactory)
    {
        _context = context;
        _commandFactory = commandFactory;
    }
    // ... phần còn lại giữ nguyên
}
```

Chỉ đổi kiểu field + tham số constructor + khai báo class. Không đổi body `ExecuteAsync` / `UndoAsync` / …

- [ ] **Step 5: Build**

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** 0 Error.  
(Lúc này DI vẫn đăng ký concrete; controller vẫn inject concrete — vẫn chạy được cho đến Task 6–7.)

- [ ] **Step 6: Commit**

```powershell
git add Services/ICardActionService.cs Services/CardActions/ICardActionCommandFactory.cs Services/CardActionService.cs Services/CardActions/CardActionCommandFactory.cs
git commit -m "refactor: add card action service and factory interfaces"
```

---

### Task 5: Interface cho Achievement services + nối dependency nội bộ

**Mục đích task này:**  
Ba service thành tích phụ thuộc nhau. Sau task này, chúng inject interface của nhau; Observer cũng inject `IAchievementUnlockService`.

**Files:**
- Create: `Services/IAchievementProgressService.cs`
- Create: `Services/IAchievementUnlockService.cs`
- Create: `Services/IAchievementService.cs`
- Modify: `Services/AchievementProgressService.cs`
- Modify: `Services/AchievementUnlockService.cs`
- Modify: `Services/AchievementService.cs`
- Modify: `Services/StudyEvents/AchievementStudyObserver.cs`

**Interfaces:**
- Consumes: `AchievementProgressSnapshot`, `AchievementCatalog.Definition` (types sẵn có)
- Produces: ba interface achievement + consumer nội bộ dùng interface

- [ ] **Step 1: Tạo `Services/IAchievementProgressService.cs`**

```csharp
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services;

// Đếm metric thành tích (thẻ thuộc, buổi học, nghe chép…) một lần cho một user.
public interface IAchievementProgressService
{
    Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Tạo `Services/IAchievementUnlockService.cs`**

```csharp
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services;

// So metric với catalog, chèn UserAchievement còn thiếu; trả về danh sách vừa mở lần này.
public interface IAchievementUnlockService
{
    Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Tạo `Services/IAchievementService.cs`**

```csharp
namespace ltwnc.Services;

// Dữ liệu trang Thành tích: rescan unlock + map list view model.
// AchievementPageModel vẫn là class concrete (không phải interface).
public interface IAchievementService
{
    Task<AchievementPageModel> GetPageAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```

`AchievementPageModel` nằm cùng namespace `ltwnc.Services` trong file `AchievementService.cs` — interface dùng được type đó.

- [ ] **Step 4: Implement `AchievementProgressService`**

```csharp
public class AchievementProgressService : IAchievementProgressService
```

Body không đổi. Constructor vẫn chỉ nhận `AppDbContext`.

- [ ] **Step 5: Implement `AchievementUnlockService` + inject progress qua interface**

Trước:

```csharp
public class AchievementUnlockService
{
    private readonly AppDbContext _context;
    private readonly AchievementProgressService _progress;

    public AchievementUnlockService(AppDbContext context, AchievementProgressService progress)
    {
        _context = context;
        _progress = progress;
    }
```

Sau:

```csharp
public class AchievementUnlockService : IAchievementUnlockService
{
    private readonly AppDbContext _context;
    private readonly IAchievementProgressService _progress;

    public AchievementUnlockService(AppDbContext context, IAchievementProgressService progress)
    {
        _context = context;
        _progress = progress;
    }
```

Body `SyncEligibleAsync` không đổi (vẫn gọi `_progress.GetSnapshotAsync(...)`).

- [ ] **Step 6: Implement `AchievementService` + inject unlock/progress qua interface**

Trước:

```csharp
public class AchievementService
{
    private readonly AppDbContext _context;
    private readonly AchievementUnlockService _unlock;
    private readonly AchievementProgressService _progress;

    public AchievementService(
        AppDbContext context,
        AchievementUnlockService unlock,
        AchievementProgressService progress)
```

Sau:

```csharp
public class AchievementService : IAchievementService
{
    private readonly AppDbContext _context;
    private readonly IAchievementUnlockService _unlock;
    private readonly IAchievementProgressService _progress;

    public AchievementService(
        AppDbContext context,
        IAchievementUnlockService unlock,
        IAchievementProgressService progress)
```

Gán field trong body constructor giữ nguyên. Method `GetPageAsync` không đổi.

- [ ] **Step 7: Đổi `AchievementStudyObserver` inject interface**

Trong `Services/StudyEvents/AchievementStudyObserver.cs`:

Trước:

```csharp
private readonly AchievementUnlockService _unlockService;

public AchievementStudyObserver(AchievementUnlockService unlockService)
{
    _unlockService = unlockService;
}
```

Sau:

```csharp
private readonly IAchievementUnlockService _unlockService;

public AchievementStudyObserver(IAchievementUnlockService unlockService)
{
    _unlockService = unlockService;
}
```

Method `OnStudyEventAsync` không đổi.

**Using:** File đã có `using ltwnc.Services;` — đủ để thấy `IAchievementUnlockService`.

- [ ] **Step 8: Build**

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** 0 Error.

**Lưu ý về test:**  
Test tạo service bằng `new AchievementService(context, unlock, progress)` với **concrete** vẫn hợp lệ vì concrete implement interface; tham số constructor bây giờ là interface type nhưng concrete assignable. Ví dụ:

```csharp
var progress = new AchievementProgressService(_context);
var unlock = new AchievementUnlockService(_context, progress);
var sut = new AchievementService(_context, unlock, progress);
```

vẫn compile vì `AchievementProgressService` là `IAchievementProgressService`, v.v.

- [ ] **Step 9: Commit**

```powershell
git add Services/IAchievementProgressService.cs Services/IAchievementUnlockService.cs Services/IAchievementService.cs Services/AchievementProgressService.cs Services/AchievementUnlockService.cs Services/AchievementService.cs Services/StudyEvents/AchievementStudyObserver.cs
git commit -m "refactor: add achievement service interfaces and wire internal deps"
```

---

### Task 6: Controllers inject interface

**Mục đích task này:**  
Mọi controller trong scope phụ thuộc abstraction, không còn field kiểu concrete application service.

**Files:**
- Modify: `Controllers/FlashcardSetController.cs`
- Modify: `Controllers/HomeController.cs`
- Modify: `Controllers/StudyController.cs`
- Modify: `Controllers/CardActionsController.cs`
- Modify: `Controllers/AchievementsController.cs`

**Interfaces:**
- Consumes: các `I*` từ Task 1–5
- Produces: controllers chỉ còn constructor parameter interface (trong scope)

Với **mỗi** controller: chỉ đổi kiểu field + tham số constructor. **Không** đổi action method logic.

- [ ] **Step 1: `FlashcardSetController`**

Đổi:

```csharp
private readonly FlashcardSetService _setService;

public FlashcardSetController(
    FlashcardSetService setService,
    UserManager<IdentityUser> userManager)
```

thành:

```csharp
private readonly IFlashcardSetService _setService;

public FlashcardSetController(
    IFlashcardSetService setService,
    UserManager<IdentityUser> userManager)
```

`using ltwnc.Services;` đã có — giữ nguyên.

- [ ] **Step 2: `HomeController`**

Đổi:

```csharp
private readonly FlashcardSetService _setService;

public HomeController(FlashcardSetService setService)
```

thành:

```csharp
private readonly IFlashcardSetService _setService;

public HomeController(IFlashcardSetService setService)
```

- [ ] **Step 3: `StudyController`**

Đổi ba service:

```csharp
private readonly IStudyService _studyService;
private readonly IDictationService _dictationService;
private readonly IFlashcardSetService _setService;

public StudyController(
    IStudyService studyService,
    IDictationService dictationService,
    IFlashcardSetService setService,
    UserManager<IdentityUser> userManager)
{
    _studyService = studyService;
    _dictationService = dictationService;
    _setService = setService;
    _userManager = userManager;
}
```

- [ ] **Step 4: `CardActionsController`**

File đã có `using ltwnc.Services;` và `using ltwnc.Services.CardActions;`.

Đổi:

```csharp
private readonly ICardActionService _cardActionService;
private readonly ICardActionCommandFactory _commandFactory;
private readonly IFlashcardSetService _setService;

public CardActionsController(
    ICardActionService cardActionService,
    ICardActionCommandFactory commandFactory,
    IFlashcardSetService setService,
    UserManager<IdentityUser> userManager)
{
    _cardActionService = cardActionService;
    _commandFactory = commandFactory;
    _setService = setService;
    _userManager = userManager;
}
```

- [ ] **Step 5: `AchievementsController`**

Đổi:

```csharp
private readonly IAchievementService _achievementService;

public AchievementsController(
    IAchievementService achievementService,
    UserManager<IdentityUser> userManager)
```

- [ ] **Step 6: Build solution (main + tests)**

```powershell
dotnet build ltwnc.csproj
dotnet build tests/ltwnc.Tests/ltwnc.Tests.csproj
```

**Kỳ vọng:** Cả hai project build 0 Error.

**Nếu test controller fail compile** vì `new StudyController(studyService, ...)` với concrete: concrete **vẫn assignable** sang interface parameter — không cần sửa. Chỉ sửa nếu có chỗ gán field public concrete type (không có trong codebase hiện tại).

- [ ] **Step 7: Commit**

```powershell
git add Controllers/FlashcardSetController.cs Controllers/HomeController.cs Controllers/StudyController.cs Controllers/CardActionsController.cs Controllers/AchievementsController.cs
git commit -m "refactor: inject application service interfaces in controllers"
```

---

### Task 7: Cập nhật DI trong `Program.cs`

**Mục đích task này:**  
Container resolve interface → concrete. Sau bước này, runtime request controller mới nhận đúng implementation.

**Files:**
- Modify: `Program.cs`

**Interfaces:**
- Consumes: 8 cặp interface/implementation
- Produces: DI graph hoàn chỉnh theo design

- [ ] **Step 1: Thay 8 dòng đăng ký application service**

Trong `Program.cs`, tìm khối:

```csharp
// Add Services
builder.Services.AddScoped<FlashcardSetService>();
builder.Services.AddScoped<StudyService>();
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<DictationService>();
builder.Services.AddScoped<CardActionService>();
builder.Services.AddScoped<CardActionCommandFactory>();
```

và các dòng Achievement:

```csharp
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<AchievementProgressService>();
builder.Services.AddScoped<AchievementUnlockService>();
```

Thay bằng (giữ comment tiếng Việt có ích):

```csharp
// Application services — inject qua interface (swap/decorator sau này không sửa controller)
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
builder.Services.AddScoped<IStudyService, StudyService>();
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<IDictationService, DictationService>();
builder.Services.AddScoped<ICardActionService, CardActionService>();
builder.Services.AddScoped<ICardActionCommandFactory, CardActionCommandFactory>();
```

và:

```csharp
// Service đọc thành tích cho trang UI (không phải observer)
builder.Services.AddScoped<IAchievementService, AchievementService>();
// Service đếm metric tiến độ huy hiệu (snapshot live)
builder.Services.AddScoped<IAchievementProgressService, AchievementProgressService>();
// Service đồng bộ mở khóa huy hiệu đủ điều kiện (Observer + rescan trang)
builder.Services.AddScoped<IAchievementUnlockService, AchievementUnlockService>();
```

**Không xóa / không đổi** các dòng GoF:

```csharp
builder.Services.AddScoped<IStudyCardQueryService, StudyCardQueryService>();
builder.Services.AddScoped<IStudyModeStrategyResolver, StudyModeStrategyResolver>();
builder.Services.AddScoped<IStudyModeStrategy, FlashcardModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, DictationModeStrategy>();
builder.Services.AddScoped<IStudyEventPublisher, StudyEventPublisher>();
builder.Services.AddScoped<IStudyEventObserver, AchievementStudyObserver>();
builder.Services.AddScoped<IStudyEventObserver, LoggingStudyObserver>();
```

`using ltwnc.Services;` và `using ltwnc.Services.CardActions;` đã có sẵn ở đầu file.

- [ ] **Step 2: Build + test đầy đủ**

```powershell
dotnet build
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-build
```

Nếu `--no-build` sau khi chỉ build partial, an toàn hơn:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

**Kỳ vọng:**
- Build: 0 Error
- Tests: toàn bộ pass (số test có thể thay đổi theo thời điểm; không được có fail mới do refactor)

- [ ] **Step 3: Commit**

```powershell
git add Program.cs
git commit -m "refactor: register application services as interface mappings in DI"
```

---

### Task 8: Kiểm tra cuối + README tùy chọn

**Mục đích task này:**  
Khóa success criteria của spec; ghi chú ngắn cho người đọc README (không bắt buộc dài).

**Files:**
- Modify (optional): `README.md`
- Verify only: build + test

- [ ] **Step 1: Checklist success criteria (đánh dấu khi đã đúng)**

| Tiêu chí | Cách kiểm |
|----------|-----------|
| 8 file interface tồn tại | Liệt kê `Services/I*.cs` + `Services/CardActions/ICardActionCommandFactory.cs` |
| 8 class có `: IXxx` | Mở từng service class |
| Controllers inject interface | Grep `FlashcardSetService` / `StudyService` trong `Controllers/` — **không** còn field concrete application service |
| DI `AddScoped<I, Impl>` | Mở `Program.cs` |
| GoF DI nguyên | Strategy / Observer registration còn |
| Test xanh | `dotnet test` |
| Không đổi logic | Diff không sửa body method nghiệp vụ |

Grep hỗ trợ:

```powershell
# Trong Controllers: không nên còn inject concrete application service
# (có thể còn chữ trong comment — kiểm tra field type)
rg "private readonly (FlashcardSetService|StudyService|DictationService|CardActionService|CardActionCommandFactory|AchievementService|AchievementProgressService|AchievementUnlockService)" Controllers
```

**Kỳ vọng:** không match field concrete (0 match).

```powershell
rg "AddScoped<(FlashcardSetService|StudyService|DictationService|CardActionService|CardActionCommandFactory|AchievementService|AchievementProgressService|AchievementUnlockService)>" Program.cs
```

**Kỳ vọng:** 0 match (đã chuyển hết sang cặp interface).

- [ ] **Step 2 (tùy chọn): Cập nhật README**

Trong `README.md`, sau phần GoF (hoặc trong mục cấu trúc), thêm một đoạn ngắn kiểu:

```markdown
### Application service interfaces

Ngoài interface của các mẫu GoF, các application service (`FlashcardSetService`, `StudyService`, `DictationService`, card actions, achievements) cũng có contract `I*` tương ứng. Controllers và service nội bộ inject interface; `Program.cs` đăng ký `AddScoped<IService, Service>()`. Mục đích: sau này thay implementation hoặc bọc decorator mà không sửa call site. Đây không phải mẫu GoF mới — chỉ là abstraction cho DI.
```

Nếu không muốn đụng docs trong PR refactor, bỏ qua Step 2.

- [ ] **Step 3: Commit cuối nếu có README**

```powershell
git add README.md
git commit -m "docs(readme): note application service interfaces for DI extensibility"
```

---

## Verification commands (tóm tắt một lần ở cuối)

Sau khi xong hết task:

```powershell
dotnet build
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

**Pass khi:** build không error, test suite pass.

Smoke thủ công (khuyến nghị, không bắt buộc automated):

1. Chạy app: `dotnet run --project ltwnc.csproj`
2. Vào trang chủ (public sets) — `HomeController` + `IFlashcardSetService`
3. Login → `/Set` — `FlashcardSetController`
4. Vào Study Hub một bộ thẻ — `StudyController` + `IStudyService`
5. Vào `/Achievements` — `IAchievementService` + unlock chain
6. (Nếu có set owner) batch star/unstar trên edit set — `ICardActionService` + factory

Mọi bước trên không được 500 do “Unable to resolve service for type …”.

---

## Những việc **không** làm trong plan này

- Không tạo `CachingStudyService` hay decorator thật.
- Không tạo `ServiceCollectionExtensions.AddApplicationServices`.
- Không tách `IFlashcardSetQuery` / `IFlashcardSetCommand`.
- Không đổi `IStudyModeStrategy` / Observer hiện có.
- Không rewrite unit test sang Moq/NSubstitute (trừ khi build bắt buộc — hiện không).

---

## Self-review (plan vs spec)

| Yêu cầu spec | Task cover |
|--------------|------------|
| 8 interface mirror public API | Task 1–5 |
| File riêng cạnh implementation | Task 1–5 paths |
| Class implement interface | Task 1–5 |
| Controllers inject interface | Task 6 |
| Internal deps inject interface | Task 4 (factory), Task 5 (achievement + observer) |
| `Program.cs` `AddScoped<I, Impl>` | Task 7 |
| GoF DI giữ nguyên | Task 7 ghi rõ không đụng |
| Tests optional / concrete `new` vẫn OK | Task 5 Step 8 + Task 6 Step 6 |
| No behavior change | Global constraints + mỗi task “không sửa body” |
| Success criteria + verify | Task 8 |
| Optional README | Task 8 Step 2 |

Không còn TBD. Signature trong plan lấy từ code tại thời điểm viết plan; nếu disk lệch, ưu tiên disk.
