# Spaced Review Focus Deck Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chuyển toàn bộ luồng `/Review` sang giao diện Focus Deck của Prototype A mà không đổi nghiệp vụ ôn tập.

**Architecture:** Ba Razor view hiện có cùng dùng `wwwroot/css/review.css`; mỗi view tự giữ markup phù hợp với trạng thái bắt đầu, đang ôn hoặc kết quả. `Session.cshtml` tiếp tục submit về các endpoint hiện tại và chỉ dùng JavaScript nhỏ để hiện đáp án và hỗ trợ phím tắt.

**Tech Stack:** ASP.NET Core MVC, Razor, CSS thuần, JavaScript thuần, xUnit, Playwright MCP.

## Global Constraints

- Không thay đổi controller, service, entity, migration hoặc database.
- Không thêm thư viện frontend.
- Giữ nguyên anti-forgery token và các endpoint POST hiện tại.
- Tôn trọng toàn bộ `StudySettingsViewModel` khi render mặt trước, mặt sau và ảnh.
- Hỗ trợ desktop, mobile, bàn phím và `prefers-reduced-motion`.
- Không stage hoặc sửa các thay đổi không liên quan đang tồn tại trong working tree.

---

### Task 1: Khóa hợp đồng giao diện Review bằng test tĩnh

**Files:**
- Create: `tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs`
- Test: `tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs`

**Interfaces:**
- Consumes: các file Razor tại `Views/Review` và stylesheet `wwwroot/css/review.css`.
- Produces: test bảo vệ shell Focus Deck, form nghiệp vụ, phím tắt, responsive và accessibility.

- [ ] **Step 1: Viết test thất bại**

Tạo test đọc file theo pattern hiện có:

```csharp
namespace ltwnc.Tests.Views;

public class ReviewFocusDeckViewTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void ReviewFlow_UsesOneFocusDeckShell()
    {
        string index = Read("Views/Review/Index.cshtml");
        string session = Read("Views/Review/Session.cshtml");
        string result = Read("Views/Review/Result.cshtml");

        Assert.Contains("~/css/review.css", index);
        Assert.Contains("~/css/review.css", session);
        Assert.Contains("~/css/review.css", result);
        Assert.Contains("ViewData[\"HideLayoutChrome\"] = true", index);
        Assert.Contains("ViewData[\"HideLayoutChrome\"] = true", session);
        Assert.Contains("ViewData[\"HideLayoutChrome\"] = true", result);
        Assert.Contains("review-focus", index);
        Assert.Contains("review-focus", session);
        Assert.Contains("review-focus", result);
    }

    [Fact]
    public void Session_PreservesReviewPostsAndKeyboardControls()
    {
        string view = Read("Views/Review/Session.cshtml");

        Assert.Contains("action=\"/Review/@Model.SessionId/End\"", view);
        Assert.Contains("action=\"/Review/@Model.SessionId/Rate\"", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("name=\"flashcardId\"", view);
        Assert.Contains("name=\"answerRevealed\"", view);
        Assert.Contains("name=\"rating\" value=\"Again\"", view);
        Assert.Contains("name=\"rating\" value=\"Hard\"", view);
        Assert.Contains("name=\"rating\" value=\"Good\"", view);
        Assert.Contains("name=\"rating\" value=\"Easy\"", view);
        Assert.Contains("event.code === 'Space'", view);
        Assert.Contains("['1', '2', '3', '4']", view);
    }

    [Fact]
    public void FocusDeckStyles_AreResponsiveAndAccessible()
    {
        string css = Read("wwwroot/css/review.css");

        Assert.Contains("width: min(100%, 56.25rem)", css);
        Assert.Contains("grid-template-columns: repeat(4", css);
        Assert.Contains("@media (max-width: 40rem)", css);
        Assert.Contains("grid-template-columns: repeat(2", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("prefers-reduced-motion: reduce", css);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~ReviewFocusDeckViewTests
```

Expected: FAIL vì `wwwroot/css/review.css` chưa tồn tại và các view chưa có Focus Deck shell.

- [ ] **Step 3: Commit riêng test đỏ**

```bash
git add tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs
git commit -m "test(review): define focus deck UI contract"
```

---

### Task 2: Tạo stylesheet Focus Deck dùng chung

**Files:**
- Create: `wwwroot/css/review.css`
- Test: `tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs`

**Interfaces:**
- Consumes: design token từ `wwwroot/css/site.css`: `--paper`, `--surface`, `--ink`, `--muted`, `--line`, `--brass`, `--success`, `--error`, radius và motion token.
- Produces: class namespace `review-focus*` cho cả ba Razor view.

- [ ] **Step 1: Tạo CSS tối thiểu theo Prototype A**

Stylesheet phải định nghĩa các khối sau:

```css
.review-focus { min-height: 100dvh; padding: 1.5rem 1rem 4rem; background: var(--paper); color: var(--ink); }
.review-focus__shell { width: min(100%, 56.25rem); margin-inline: auto; }
.review-focus__header { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; border-bottom: 1px solid var(--line); padding-bottom: 1rem; }
.review-focus__progress-track { height: .25rem; overflow: hidden; border-radius: var(--radius-pill); background: var(--surface-sunken); }
.review-focus__progress-value { display: block; height: 100%; background: var(--brass); }
.review-focus__card { display: grid; min-height: 32rem; align-content: center; justify-items: center; border: 1px solid var(--line-strong); border-radius: var(--radius-card); background: var(--surface); }
.review-focus__ratings { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .65rem; }
.review-focus :focus-visible { outline: 3px solid var(--brass); outline-offset: 3px; }
@media (max-width: 40rem) { .review-focus__header { grid-template-columns: 1fr auto; } .review-focus__ratings { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (prefers-reduced-motion: reduce) { .review-focus *, .review-focus *::before, .review-focus *::after { scroll-behavior: auto !important; transition: none !important; } }
```

Mở rộng đúng namespace này cho intro panel, alert, flashcard label/content/image, answer, reveal button, rating variants, result stats/list và CTA. Không tạo selector toàn cục ngoài `.review-focus`.

- [ ] **Step 2: Chạy riêng test CSS**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ReviewFocusDeckViewTests.FocusDeckStyles"
```

Expected: PASS.

- [ ] **Step 3: Commit stylesheet**

```bash
git add wwwroot/css/review.css
git commit -m "style(review): add focus deck shell"
```

---

### Task 3: Chuyển trang bắt đầu và kết quả sang Focus Deck

**Files:**
- Modify: `Views/Review/Index.cshtml`
- Modify: `Views/Review/Result.cshtml`
- Test: `tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs`

**Interfaces:**
- Consumes: class `review-focus*` từ Task 2, `TempData["Message"]`, `ReviewSessionViewModel`.
- Produces: trang bắt đầu và kết quả có chung shell, không thay đổi route.

- [ ] **Step 1: Chuyển `Index.cshtml`**

Đặt:

```csharp
ViewData["HideLayoutChrome"] = true;
```

Thêm `@section Styles` tải `~/css/review.css`. Markup phải có `.review-focus > .review-focus__shell`, header với liên kết `/Set`, intro, `role="status"` cho TempData và form:

```html
<form method="post" action="/Review/Start">
    @Html.AntiForgeryToken()
    <button type="submit" class="review-focus__primary">Bắt đầu ôn tập</button>
</form>
```

- [ ] **Step 2: Chuyển `Result.cshtml`**

Dùng cùng shell/header; tính phần trăm an toàn:

```csharp
int progressPercent = Model.TotalCards == 0
    ? 0
    : (int)Math.Round(Model.RatedCards * 100d / Model.TotalCards);
```

Render thanh tiến độ với `style="width: @(progressPercent)%"`, bốn thống kê mức nhớ, toàn bộ danh sách `Model.Cards`, trạng thái từng thẻ và CTA `/Review`.

- [ ] **Step 3: Chạy test shell**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ReviewFocusDeckViewTests.ReviewFlow"
```

Expected: còn FAIL chỉ vì `Session.cshtml` chưa chuyển.

- [ ] **Step 4: Build Razor**

```bash
dotnet build ltwnc.csproj
```

Expected: exit 0, không có Razor compilation error.

- [ ] **Step 5: Commit hai view**

```bash
git add Views/Review/Index.cshtml Views/Review/Result.cshtml
git commit -m "style(review): apply focus deck to flow shell"
```

---

### Task 4: Chuyển phiên ôn và giữ nguyên hành vi nghiệp vụ

**Files:**
- Modify: `Views/Review/Session.cshtml`
- Test: `tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs`

**Interfaces:**
- Consumes: `ReviewSessionViewModel`, `ReviewCardViewModel`, `StudySettingsViewModel`, `ReviewRatingPreviewViewModel`, endpoint `/Review/{id}/Rate` và `/Review/{id}/End`.
- Produces: UI Focus Deck có reveal, rating submit và phím tắt.

- [ ] **Step 1: Chuẩn bị dữ liệu Razor**

Giữ cách tìm thẻ hiện tại và preview; thêm:

```csharp
ViewData["HideLayoutChrome"] = true;
int currentNumber = Math.Min(Model.RatedCards + 1, Model.TotalCards);
int progressPercent = Model.TotalCards == 0
    ? 0
    : (int)Math.Round(Model.RatedCards * 100d / Model.TotalCards);
```

- [ ] **Step 2: Chuyển markup sang Focus Deck**

Dùng header, progress, `.review-focus__card`, `.review-focus__answer`, `.review-focus__reveal` và `.review-focus__ratings`. Giữ nguyên tất cả nhánh `Model.Settings.ShowFront*`, `ShowBack*`, `HideImage`, `BlurImage`; giữ hai form POST và anti-forgery token. Bốn nút rating có `data-shortcut="1"` đến `data-shortcut="4"`, bị disabled trước reveal và có nhãn thời gian từ preview.

- [ ] **Step 3: Thêm JavaScript reveal và phím tắt**

Dùng một IIFE lấy `#review-reveal`, `#review-answer`, `#answer-revealed`, form rate và các nút rating. Hàm reveal mở đáp án, đặt hidden input thành `true`, disable nút reveal và enable rating. Listener bàn phím bỏ qua input/textarea/select/contenteditable; `Space` gọi reveal; `['1', '2', '3', '4']` tìm nút bằng `data-shortcut` và gọi `requestSubmit(button)` khi đã reveal.

- [ ] **Step 4: Chạy toàn bộ contract test**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~ReviewFocusDeckViewTests
```

Expected: 3 tests PASS, 0 failed.

- [ ] **Step 5: Chạy Review controller/service tests**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ReviewControllerTests|FullyQualifiedName~ReviewServiceTests"
```

Expected: tất cả test được chọn PASS.

- [ ] **Step 6: Commit session view**

```bash
git add Views/Review/Session.cshtml
git commit -m "style(review): apply focus deck to study session"
```

---

### Task 5: Smoke test luồng thật và responsive

**Files:**
- Verify only; không tạo source file.

**Interfaces:**
- Consumes: ứng dụng local, database đã migrate và tài khoản smoke test do người dùng cung cấp.
- Produces: bằng chứng desktop/mobile và server log sạch.

- [ ] **Step 1: Build và chạy test cuối**

```bash
dotnet build ltwnc.csproj
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~Review"
```

Expected: hai lệnh exit 0.

- [ ] **Step 2: Khởi động ứng dụng local**

```bash
dotnet run --no-launch-profile --urls http://localhost:5000
```

Expected: ứng dụng lắng nghe tại `http://localhost:5000`.

- [ ] **Step 3: Smoke test desktop bằng Playwright MCP**

Tại viewport 1440×900: đăng nhập, mở `/Review`, kiểm tra navbar/footer bị ẩn, bắt đầu lượt ôn, hiện đáp án bằng `Space`, chọn một mức nhớ bằng phím `1–4`, xác nhận chuyển sang thẻ kế tiếp, kết thúc sớm và xác nhận trang Result hiển thị đúng số đã đánh giá.

- [ ] **Step 4: Smoke test mobile bằng Playwright MCP**

Đổi viewport 390×844; mở lại ba trạng thái Index, Session và Result. Xác nhận không có horizontal overflow và bốn rating hiển thị hai cột.

- [ ] **Step 5: Kiểm tra lỗi runtime**

Lấy console messages và server log. Expected: không có console error, HTTP 500, `fail:`, `Unhandled exception` hoặc `SqlException` mới.

- [ ] **Step 6: Kiểm tra phạm vi diff**

```bash
git diff -- Views/Review/Index.cshtml Views/Review/Session.cshtml Views/Review/Result.cshtml wwwroot/css/review.css tests/ltwnc.Tests/Views/ReviewFocusDeckViewTests.cs
```

Expected: chỉ có thay đổi Review UI và test hợp đồng; không có controller/service/model/migration change từ công việc này.
