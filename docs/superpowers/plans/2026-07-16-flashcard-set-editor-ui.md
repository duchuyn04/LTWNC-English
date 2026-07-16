# Flashcard Set Editor UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cải thiện trang Edit bộ thẻ với sidebar anchor, split editor có scroll cố định, form thêm mới tách biệt, star checkbox không reload và input dài dễ chỉnh sửa.

**Architecture:** Giữ nghiệp vụ trong `IFlashcardSetService`; thêm một action JSON mỏng vào `FlashcardSetController` để gọi `ToggleStarAsync`. View render state ban đầu bằng semantic checkbox/data attributes, còn `wwwroot/js/flashcard-editor.js` xử lý panel selection, anchor smooth-scroll, star toggle/rollback và textarea auto-grow; CSS tập trung trong `wwwroot/css/edit.css`.

**Tech Stack:** ASP.NET Core MVC/.NET 10, Razor, CSS, vanilla JavaScript Fetch API, xUnit/Moq.

## Global Constraints

- Chỉ thay đổi trang `Views/FlashcardSet/Edit.cshtml`, CSS/JS liên quan và endpoint toggle sao phục vụ trang này.
- Không thay đổi mô hình dữ liệu, cách import file, CRUD nghiệp vụ hoặc các Study Mode.
- Giữ nguyên checkbox chọn nhiều thẻ cho thao tác Delete/Star/Unstar hàng loạt; checkbox này là lựa chọn hàng loạt, không đại diện cho trạng thái sao của thẻ.
- Panel danh sách có chiều cao `min(70vh, 720px)` và `overflow-y: auto`; panel chi tiết có cùng giới hạn và scroll riêng.
- Form thêm mới nằm ngoài `.vocab-detail` trong section `id="add-card-form"`.
- Star control là `input type="checkbox"` semantic, hiển thị hình ngôi sao; batch selector vẫn là checkbox vuông.
- Star request dùng POST + antiforgery, trả JSON `{ success, isStarred }`, cập nhật UI không reload và rollback khi lỗi.
- `backText` và `exampleMeaning` là textarea auto-grow, có max-height và scroll nội bộ.
- Không thêm thư viện frontend mới hoặc inline layout styles mới.

---

### Task 1: Thêm endpoint toggle sao và controller tests

**Files:**
- Modify: `Controllers/FlashcardSetController.cs`
- Test: `tests/ltwnc.Tests/Controllers/FlashcardSetStarControllerTests.cs`

**Interfaces:**
- Add action `POST /Set/{setId}/Cards/{cardId}/ToggleStar` with signature `Task<IActionResult> ToggleStar(int setId, int cardId)`.
- Authenticated owner success returns `Json(new { success = true, isStarred })`.
- Anonymous returns `Challenge()`; `KeyNotFoundException` returns `NotFound()`; `UnauthorizedAccessException` returns `Forbid()`.

- [ ] **Step 1: Write failing controller tests**

```csharp
[Fact]
public async Task ToggleStar_owner_returns_json_without_redirect()
{
    var service = new Mock<IFlashcardSetService>();
    service.Setup(x => x.ToggleStarAsync(7, "user-1")).ReturnsAsync(true);
    var currentUser = new StubCurrentUser("user-1");
    var controller = new FlashcardSetController(service.Object, currentUser, Mock.Of<IFlashcardImportService>());

    IActionResult result = await controller.ToggleStar(3, 7);

    var json = Assert.IsType<JsonResult>(result);
    Assert.Equal(true, json.Value!.GetType().GetProperty("isStarred")!.GetValue(json.Value));
    service.Verify(x => x.ToggleStarAsync(7, "user-1"), Times.Once);
}
```

Also test anonymous `Challenge`, `KeyNotFoundException` → 404, and `UnauthorizedAccessException` → 403.

- [ ] **Step 2: Run focused tests to verify RED**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardSetStarControllerTests`

Expected: FAIL because the action does not exist.

- [ ] **Step 3: Implement the thin action**

Use the existing `_currentUser` and `_setService` fields. Check `UserId` before calling the service, pass `cardId` and user ID to `ToggleStarAsync`, and return the exact JSON shape. Catch only the existing domain exceptions; keep EF/service logic out of the controller.

- [ ] **Step 4: Run focused tests**

Run the same command. Expected: all controller toggle tests pass.

- [ ] **Step 5: Commit**

```bash
git add Controllers/FlashcardSetController.cs tests/ltwnc.Tests/Controllers/FlashcardSetStarControllerTests.cs
git commit -m "feat: add ajax star toggle for set editor"
```

### Task 2: Restructure Edit markup and add navigation/sidebar

**Files:**
- Modify: `Views/FlashcardSet/Edit.cshtml`
- Test: `tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs`

**Interfaces:**
- Produces element IDs `set-info`, `file-import`, `card-list`, and `add-card-form` for sidebar links.
- Produces `.set-editor-sidebar`, `.vocab-editor`, `.vocab-list`, `.vocab-detail`, and `.add-card-form` with the form outside `.vocab-detail`.
- Each existing-card star checkbox exposes `data-toggle-star-url`, `data-card-id`, `data-star-target`, `checked`, and `aria-label`.

- [ ] **Step 1: Write failing markup tests**

Read the Razor source as text and assert: four sidebar hrefs exist; the list-header `a[href="#add-card-form"]` is gone; `id="add-card-form"` appears after the closing `.vocab-editor` section; existing card panels remain under `.vocab-detail`; batch `selectedCardIds` checkboxes remain present.

- [ ] **Step 2: Run markup tests to verify RED**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardSetEditMarkupTests`

Expected: FAIL on missing sidebar/separation/star attributes.

- [ ] **Step 3: Implement markup changes**

Wrap the current metadata/import/editor sections with anchor IDs and a two-column shell. Add the four sidebar links. Remove only the redundant list-header “Thêm” link. Move the add-card form after `.vocab-editor` into its own card/section. Replace per-card `star-toggle` checkbox markup with a visually hidden checkbox plus a star label/button target; keep `selectedCardIds` batch checkboxes untouched. Add antiforgery token/data URL for existing card toggle controls and a hidden `isStarred` field for the add form.

- [ ] **Step 4: Run markup tests**

Run the focused command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Views/FlashcardSet/Edit.cshtml tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs
git commit -m "feat: restructure flashcard editor layout"
```

### Task 3: Add fixed-height panels, sidebar, star visuals, and responsive CSS

**Files:**
- Modify: `wwwroot/css/edit.css`
- Test: `tests/ltwnc.Tests/Views/FlashcardSetEditStyleTests.cs`

**Interfaces:**
- `.vocab-list` and `.vocab-detail` expose fixed-scroll CSS rules.
- `.set-editor-sidebar` is sticky on desktop and horizontal-scroll on `max-width: 900px`.
- `.star-checkbox` visually renders a star with distinct unchecked/checked/focus states.

- [ ] **Step 1: Write failing style assertions**

Assert the stylesheet contains `overflow-y: auto`, `max-height: min(70vh, 720px)`, `.set-editor-sidebar`, `.star-checkbox`, `:checked`, `:focus-visible`, and the mobile sidebar rule.

- [ ] **Step 2: Run style tests to verify RED**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardSetEditStyleTests`

Expected: FAIL because the new selectors/rules do not exist.

- [ ] **Step 3: Implement CSS**

Set `.vocab-editor { align-items: start; }`, `.vocab-list, .vocab-detail { max-height: min(70vh, 720px); overflow-y: auto; min-height: 0; }`. Add sidebar spacing, sticky positioning, anchor focus style, mobile horizontal layout, and `.star-checkbox` rules that hide only the native visual (`appearance: none`) while preserving keyboard focus and checked state. Use `content`/icon text or existing Phosphor icon classes for empty/filled star; do not use color alone. Add `.vocab-grid > label { min-width: 0; }` and `.form-input-custom { width: 100%; box-sizing: border-box; }` within the editor scope.

- [ ] **Step 4: Run style tests and full existing suite**

Run the focused style test, then `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`. Expected: focused tests pass and full suite remains green.

- [ ] **Step 5: Commit**

```bash
git add wwwroot/css/edit.css tests/ltwnc.Tests/Views/FlashcardSetEditStyleTests.cs
git commit -m "feat: add fixed editor panels and star checkbox styles"
```

### Task 4: Add star toggle, panel navigation, and auto-grow JavaScript

**Files:**
- Create: `wwwroot/js/flashcard-editor.js`
- Modify: `Views/FlashcardSet/Edit.cshtml`
- Test: `tests/ltwnc.Tests/Views/FlashcardEditorScriptTests.cs`

**Interfaces:**
- Script initializes on `DOMContentLoaded` and preserves the current first-card selection behavior.
- `POST` body includes the antiforgery token header `RequestVerificationToken` and reads `{ success, isStarred }`.
- Script updates all controls sharing a card’s `data-card-id`, sets `checked`/`aria-checked`, toggles `.is-starred`, and updates the list icon.

- [ ] **Step 1: Write failing script contract tests**

Read the script source and assert it contains `fetch`, `RequestVerificationToken`, `response.json()`, `isStarred`, rollback logic, `scrollIntoView`, and textarea `scrollHeight` auto-grow logic. Assert the view references `~/js/flashcard-editor.js` with `asp-append-version`.

- [ ] **Step 2: Run script tests to verify RED**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardEditorScriptTests`

Expected: FAIL because the script file/reference does not exist.

- [ ] **Step 3: Implement JavaScript**

Implement these small functions: `selectCard(cardId)` toggles `is-active` on list buttons/panels; `toggleStar(input)` saves prior state, posts to `input.dataset.toggleStarUrl`, updates every matching star control/list indicator on success, and restores state plus an inline error on failure; `bindAutoGrow(textarea)` sets `height = auto` then `height = min(scrollHeight, maxHeight)`; `bindAnchors()` uses smooth scroll and updates focus. Include `X-CSRF-TOKEN`/`RequestVerificationToken` from the form token and prevent double-click requests while pending.

- [ ] **Step 4: Run focused script tests**

Run the focused command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add wwwroot/js/flashcard-editor.js Views/FlashcardSet/Edit.cshtml tests/ltwnc.Tests/Views/FlashcardEditorScriptTests.cs
git commit -m "feat: add no-reload editor interactions"
```

### Task 5: End-to-end verification and responsive smoke test

**Files:**
- Modify: none unless verification finds a concrete defect.
- Test: full test suite and browser/manual inspection of `/Set/{id}/Edit`.

- [ ] **Step 1: Run build and test verification**

Run `dotnet build ltwnc.csproj`, `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`, and `git diff --check`. Expected: build succeeds, all tests pass, no whitespace errors.

- [ ] **Step 2: Run manual desktop smoke test**

With a set containing at least 20 cards, confirm only the list/detail panels scroll, the page does not grow with every card, sidebar anchors land on the correct cards, and the add form is visually separate. Click a card star and verify the list/detail state changes without a page request/reload; force a failed request and verify rollback.

- [ ] **Step 3: Run manual mobile smoke test**

At viewport width ≤900px, confirm the sidebar becomes a horizontal scroll row, panels stack, star controls remain keyboard/focus accessible, and long definitions auto-grow with internal max-height scrolling.

- [ ] **Step 4: Commit only concrete fixes**

If verification exposes a defect, add a regression test first, implement the smallest fix, rerun the covering test and full suite, then commit with a focused message. Otherwise leave the implementation commits unchanged.

## Plan self-review

- Spec coverage: layout, sidebar anchors, redundant button removal, fixed scroll, separated add form, semantic star checkbox, AJAX rollback, input auto-grow, responsive behavior, accessibility, endpoint errors, tests, and no-new-library constraint map to Tasks 1–5.
- Placeholder scan: no TBD/TODO or vague error-handling steps; every task names files, commands, expected outcomes, and exact interfaces.
- Type consistency: controller action returns `IActionResult`/`JsonResult`; JavaScript consumes `{ success, isStarred }`; view data attributes use `cardId`, `setId`, and `toggleStarUrl` consistently.
