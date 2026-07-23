# Unified Editor File Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Khôi phục import CSV/XLSX trong Unified Editor với preview an toàn, lựa chọn thêm/ghi đè và vẫn giữ luồng dán nhanh hiện có.

**Architecture:** Mở rộng `FlashcardImportService` thành hai pha preview/commit dùng chung parser, rồi tái sử dụng `FlashcardSetService.BatchImportCardsAsync` cho append/replace nguyên tử. `FlashcardSetController` cung cấp JSON preview và giữ form POST/redirect cho commit; modal trong `Editor.cshtml` cùng `unified-editor.js` điều phối lưu set mới, chọn file, preview, xác nhận và tải lại kết quả.

**Tech Stack:** ASP.NET Core MVC .NET 10, EF Core 10, CsvHelper, ClosedXML, Razor, vanilla JavaScript, CSS, xUnit.

## Global Constraints

- Nhánh phải chứa `origin/master` mới nhất trước khi sửa code; giữ nguyên thay đổi cục bộ trong `appsettings.json`.
- Chỉ nhận `.csv` và `.xlsx`; giới hạn 10 MB và 5.000 dòng.
- Sáu trường bắt buộc: `Thuật ngữ`, `Định nghĩa`, `IPA`, `Loại từ`, `Ví dụ tiếng Anh`, `Nghĩa ví dụ tiếng Việt`.
- Hai trường tùy chọn: `Từ đồng nghĩa`, `URL ảnh`.
- Preview không được ghi database; commit phải parse và xác thực lại file.
- Ghi đè chỉ chạy khi có ít nhất một dòng hợp lệ và phải dọn dữ liệu học liên quan bằng batch service hiện có.
- Giữ tab dán nhanh và không render nội dung do người dùng cung cấp bằng HTML thô.
- Preview/commit yêu cầu owner, antiforgery và upload rate limit.
- Không lưu file upload trên server.

---

### Task 1: Thêm contract preview và tách parse dùng chung

**Files:**
- Modify: `Models/ViewModels/FlashcardSet/FlashcardImportResultViewModel.cs`
- Modify: `Services/FlashcardSets/IFlashcardImportService.cs`
- Modify: `Services/FlashcardSets/FlashcardImportService.cs`
- Test: `tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs`

**Interfaces:**
- Produces `Task<FlashcardImportPreview> PreviewAsync(int setId, string userId, IFormFile file, CancellationToken cancellationToken = default)`.
- `FlashcardImportPreview` exposes `ValidCount`, `SkippedCount`, `Rows`, `Errors`.
- Preview rows use the existing `FlashcardImportRow` type.

- [ ] **Step 1: Write failing preview tests**

```csharp
[Fact]
public async Task Preview_returns_rows_and_errors_without_mutating_database()
{
    string csv = Headers + "\nrun,chạy,/r/,verb,Run!,Chạy!\ninvalid,,/i/,noun,Example,Meaning\n";

    FlashcardImportPreview preview =
        await _service.PreviewAsync(_setId, "owner", FormFile(csv, "cards.csv"));

    Assert.Equal(1, preview.ValidCount);
    Assert.Equal(1, preview.SkippedCount);
    Assert.Equal("run", preview.Rows.Single().FrontText);
    Assert.Equal(3, preview.Errors.Single().RowNumber);
    Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
}

[Fact]
public async Task Preview_rejects_non_owner_without_mutating_database()
{
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _service.PreviewAsync(_setId, "other", FormFile(ValidCsv, "cards.csv")));
    Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FlashcardImportServiceTests
```

Expected: compilation fails because `FlashcardImportPreview` and `PreviewAsync` do not exist.

- [ ] **Step 3: Add preview contract and shared parse helper**

```csharp
public sealed class FlashcardImportPreview
{
    public int ValidCount => Rows.Count;
    public int SkippedCount => Errors.Count;
    public IReadOnlyList<FlashcardImportRow> Rows { get; init; } = [];
    public IReadOnlyList<FlashcardImportError> Errors { get; init; } = [];
}
```

Add `PreviewAsync` to the interface. In the service, extract `ValidateAndParseAsync(IFormFile, CancellationToken)` from the current import method, verify owner before opening the file, convert `FileError` to a row-zero `FlashcardImportError`, and return preview without calling `SaveChangesAsync`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 command. Expected: all `FlashcardImportServiceTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add Models/ViewModels/FlashcardSet/FlashcardImportResultViewModel.cs Services/FlashcardSets/IFlashcardImportService.cs Services/FlashcardSets/FlashcardImportService.cs tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs
git commit -m "feat: preview flashcard import files"
```

### Task 2: Commit file import qua batch service với append/replace

**Files:**
- Modify: `Services/FlashcardSets/IFlashcardImportService.cs`
- Modify: `Services/FlashcardSets/FlashcardImportService.cs`
- Modify: `Program.cs`
- Test: `tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs`

**Interfaces:**
- Changes `ImportAsync` to `ImportAsync(int setId, string userId, IFormFile file, bool replaceAll = false, CancellationToken cancellationToken = default)`.
- `FlashcardImportService` consumes `IFlashcardSetService` for atomic batch persistence.

- [ ] **Step 1: Write failing append/replace safety tests**

```csharp
[Fact]
public async Task Replace_import_removes_old_cards_and_progress_then_keeps_all_file_fields()
{
    Flashcard old = await _context.Flashcards.SingleAsync();
    _context.UserProgresses.Add(new UserProgress { UserId = "owner", FlashcardId = old.Id });
    await _context.SaveChangesAsync();

    FlashcardImportResult result =
        await _service.ImportAsync(_setId, "owner", FormFile(FullCsv, "cards.csv"), replaceAll: true);

    Assert.Equal(1, result.ImportedCount);
    Assert.Empty(await _context.UserProgresses.ToListAsync());
    Flashcard card = await _context.Flashcards.SingleAsync();
    Assert.Equal("/r/", card.Pronunciation);
    Assert.Equal("verb", card.PartOfSpeech);
    Assert.Equal("Run!", card.ExampleSentence);
    Assert.Equal("Chạy!", card.ExampleMeaning);
}

[Fact]
public async Task Replace_import_with_no_valid_rows_keeps_existing_cards()
{
    FlashcardImportResult result =
        await _service.ImportAsync(_setId, "owner", FormFile(InvalidCsv, "cards.csv"), replaceAll: true);

    Assert.Equal(0, result.ImportedCount);
    Assert.Equal("existing", (await _context.Flashcards.SingleAsync()).FrontText);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run the Task 1 test command. Expected: compile/signature failure or replace test failure.

- [ ] **Step 3: Implement minimal batch-backed commit**

Map parsed rows to existing batch items:

```csharp
IReadOnlyList<BatchImportCardItem> items = parsed.Rows.Select(row => new BatchImportCardItem
{
    FrontText = row.FrontText,
    BackText = row.BackText,
    Pronunciation = row.Pronunciation,
    PartOfSpeech = row.PartOfSpeech,
    ExampleSentence = row.ExampleSentence,
    ExampleMeaning = row.ExampleMeaning,
    Synonyms = row.Synonyms,
    ImageUrl = row.ImageUrl,
    IsStarred = false
}).ToArray();

if (items.Count > 0)
{
    await _setService.BatchImportCardsAsync(setId, items, replaceAll, userId);
}
```

Update DI/constructors without changing batch service behavior. Preserve partial row errors in `FlashcardImportResult`.

- [ ] **Step 4: Run service and batch tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~FlashcardImportServiceTests|FullyQualifiedName~FlashcardSetServiceCardTests"
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Services/FlashcardSets/IFlashcardImportService.cs Services/FlashcardSets/FlashcardImportService.cs Program.cs tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs
git commit -m "feat: support replace mode for file imports"
```

### Task 3: Thêm preview endpoint và mở rộng commit controller

**Files:**
- Modify: `Controllers/FlashcardSetController.cs`
- Test: `tests/ltwnc.Tests/Controllers/FlashcardSetImportControllerTests.cs`

**Interfaces:**
- Produces `[HttpPost] [Route("/Set/{id}/Import/Preview")] Task<IActionResult> PreviewImport(int id, IFormFile? file)`.
- Changes existing `Import` action to accept `bool replaceAll`.
- Preview JSON caps visible rows at 5 and visible errors at 100, with `errorsOmittedCount`.

- [ ] **Step 1: Write failing controller tests**

```csharp
[Fact]
public async Task PreviewImport_returns_capped_json_without_committing()
{
    var (controller, import) = Create("owner");
    IFormFile file = File();
    import.Setup(x => x.PreviewAsync(4, "owner", file, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FlashcardImportPreview
        {
            Rows = Enumerable.Range(1, 8).Select(Row).ToArray(),
            Errors = Enumerable.Range(1, 105).Select(Error).ToArray()
        });

    var result = Assert.IsType<JsonResult>(await controller.PreviewImport(4, file));

    Assert.NotNull(result.Value);
    import.VerifyAll();
}

[Fact]
public async Task Import_forwards_replace_mode()
{
    var (controller, import) = Create("owner");
    IFormFile file = File();
    import.Setup(x => x.ImportAsync(4, "owner", file, true, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new FlashcardImportResult { ImportedCount = 2 });

    await controller.Import(4, file, replaceAll: true);

    import.VerifyAll();
}
```

Also assert preview has `ValidateAntiForgeryTokenAttribute` and `EnableRateLimitingAttribute`.

- [ ] **Step 2: Run controller tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FlashcardSetImportControllerTests
```

Expected: compile failure because preview action and new import signature do not exist.

- [ ] **Step 3: Implement controller actions**

```csharp
[HttpPost]
[Route("/Set/{id}/Import/Preview")]
[ValidateAntiForgeryToken]
[EnableRateLimiting("uploads")]
public async Task<IActionResult> PreviewImport(int id, IFormFile? file)
{
    if (_currentUser.UserId is not string userId) return Challenge();
    try
    {
        FlashcardImportPreview preview =
            await _importService.PreviewAsync(id, userId, file!, HttpContext.RequestAborted);
        return Json(new
        {
            validCount = preview.ValidCount,
            skippedCount = preview.SkippedCount,
            rows = preview.Rows.Take(5),
            errors = preview.Errors.Take(MaxDisplayedImportErrors),
            errorsOmittedCount = Math.Max(0, preview.Errors.Count - MaxDisplayedImportErrors)
        });
    }
    catch (UnauthorizedAccessException) { return Forbid(); }
    catch (FlashcardImportException exception) { return BadRequest(new { error = exception.Message }); }
}
```

Forward `replaceAll` from the existing action and keep TempData/redirect behavior.

- [ ] **Step 4: Run controller tests and verify GREEN**

Run the Task 3 command. Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Controllers/FlashcardSetController.cs tests/ltwnc.Tests/Controllers/FlashcardSetImportControllerTests.cs
git commit -m "feat: expose file import preview"
```

### Task 4: Xây markup modal hai tab và khôi phục CSV mẫu

**Files:**
- Modify: `Views/FlashcardSet/Editor.cshtml`
- Create: `wwwroot/templates/flashcard-import-template.csv`
- Create: `tests/ltwnc.Tests/Views/UnifiedEditorImportMarkupTests.cs`

**Interfaces:**
- Produces DOM ids used by Task 5: `import-tab-file`, `import-tab-paste`, `import-panel-file`, `import-panel-paste`, `import-file`, `btn-file-preview`, `file-import-preview`, `import-mode-append`, `import-mode-replace`, `file-import-form`.
- Form action is `/Set/{id}/Import`, method POST, multipart, with antiforgery.

- [ ] **Step 1: Write failing markup tests**

```csharp
[Fact]
public void Import_modal_exposes_accessible_file_and_paste_tabs()
{
    Assert.Contains("role=\"tablist\"", Source);
    Assert.Contains("id=\"import-tab-file\"", Source);
    Assert.Contains("id=\"import-panel-file\"", Source);
    Assert.Contains("accept=\".csv,.xlsx\"", Source);
    Assert.Contains("enctype=\"multipart/form-data\"", Source);
    Assert.Contains("@Html.AntiForgeryToken()", Source);
}

[Fact]
public void File_import_offers_template_preview_and_append_replace_modes()
{
    Assert.Contains("~/templates/flashcard-import-template.csv", Source);
    Assert.Contains("id=\"btn-file-preview\"", Source);
    Assert.Contains("id=\"file-import-preview\"", Source);
    Assert.Contains("id=\"import-mode-append\"", Source);
    Assert.Contains("id=\"import-mode-replace\"", Source);
}
```

- [ ] **Step 2: Run markup tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UnifiedEditorImportMarkupTests
```

Expected: assertions fail because the current modal only has paste controls.

- [ ] **Step 3: Implement accessible modal markup**

Replace the current modal body with a header/close button, tablist, file panel, paste panel, shared mode radio group, status regions and actions. Render TempData import success/errors near the editor header. Restore the exact UTF-8 template:

```csv
THUẬT NGỮ,ĐỊNH NGHĨA,IPA,LOẠI TỪ,VÍ DỤ TIẾNG ANH,NGHĨA VÍ DỤ TIẾNG VIỆT,TỪ ĐỒNG NGHĨA,URL ẢNH
run,chạy,/rʌn/,verb,"Run every morning!",Hãy chạy mỗi sáng!,jog,
```

- [ ] **Step 4: Run markup tests and verify GREEN**

Run the Task 4 command. Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Views/FlashcardSet/Editor.cshtml wwwroot/templates/flashcard-import-template.csv tests/ltwnc.Tests/Views/UnifiedEditorImportMarkupTests.cs
git commit -m "feat: add file import controls to unified editor"
```

### Task 5: Styling responsive theo UI hiện tại

**Files:**
- Modify: `wwwroot/css/unified-editor.css`
- Create: `tests/ltwnc.Tests/Views/UnifiedEditorImportStyleTests.cs`

**Interfaces:**
- Styles `.import-tabs`, `.import-dropzone`, `.import-mode-options`, `.import-preview-summary`, `.import-preview-table-wrap`, `.import-error-list`.

- [ ] **Step 1: Write failing style contract**

```csharp
[Fact]
public void File_import_styles_cover_tabs_dropzone_preview_and_mobile_layout()
{
    Assert.Contains(".import-tabs", Css);
    Assert.Contains(".import-dropzone", Css);
    Assert.Contains(".import-preview-table-wrap", Css);
    Assert.Contains("overflow-x: auto", Css);
    Assert.Matches("(?s)@media \\(max-width: 767px\\).*?\\.modal-actions", Css);
    Assert.Contains("@media (prefers-reduced-motion: reduce)", Css);
}
```

- [ ] **Step 2: Run style test and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UnifiedEditorImportStyleTests
```

Expected: assertions fail because new selectors do not exist.

- [ ] **Step 3: Add minimal styles**

Use existing `--ue-*`, `--surface`, `--ink`, radius and shadow tokens. Expand modal width to at most 860px, provide visible selected/focus states, horizontal table scrolling, stacked mobile actions and no new animation dependency.

- [ ] **Step 4: Run style test and verify GREEN**

Run the Task 5 command. Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add wwwroot/css/unified-editor.css tests/ltwnc.Tests/Views/UnifiedEditorImportStyleTests.cs
git commit -m "style: refine unified editor import modal"
```

### Task 6: Điều phối modal, preview và commit trong JavaScript

**Files:**
- Modify: `wwwroot/js/unified-editor.js`
- Create: `tests/ltwnc.Tests/Views/UnifiedEditorImportScriptTests.cs`

**Interfaces:**
- Produces `openImportModal`, `closeImportModal`, `activateImportTab`, `resetFilePreview`, `previewImportFile`, `submitFileImport`.
- Consumes the Task 4 DOM ids and `/Set/{id}/Import/Preview`.

- [ ] **Step 1: Write failing script contract tests**

```csharp
[Fact]
public void Import_click_confirms_and_saves_a_new_set_before_opening_modal()
{
    Assert.Contains("window.confirm", Script);
    Assert.Contains("await ensureSetCreated()", Script);
    Assert.Matches("btnImport\\.addEventListener[\\s\\S]*?openImportModal", Script);
}

[Fact]
public void File_preview_posts_formdata_with_antiforgery_and_renders_with_textContent()
{
    Assert.Contains("new FormData", Script);
    Assert.Contains("/Import/Preview", Script);
    Assert.Contains("RequestVerificationToken", Script);
    Assert.Contains("textContent", Script);
    Assert.DoesNotContain("previewRow.innerHTML", Script);
}

[Fact]
public void Replace_commit_requires_confirmation_and_file_or_mode_changes_reset_preview()
{
    Assert.Contains("resetFilePreview()", Script);
    Assert.Contains("Toàn bộ thẻ và tiến độ học", Script);
    Assert.Contains("fileImportForm.submit()", Script);
}
```

- [ ] **Step 2: Run script tests and verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UnifiedEditorImportScriptTests
```

Expected: assertions fail against the current paste-only script.

- [ ] **Step 3: Implement modal state and accessible controls**

On Import click, confirm/save only when `isNewSet()`. Implement tabs through `aria-selected`, `hidden`, focus management and Escape close. Keep the paste parser/preview and route its replace value through the shared radio controls.

- [ ] **Step 4: Implement backend file preview**

Build `FormData` with `file` and `__RequestVerificationToken`, POST to `/Set/${setId}/Import/Preview`, parse safe JSON, render counts/table/errors exclusively with `createElement`/`textContent`, and enable commit only when `validCount > 0`.

- [ ] **Step 5: Implement final commit and destructive confirmation**

Set the form action from the saved `setId`, synchronize hidden `replaceAll`, require confirmation for replace, then use native form submission so existing TempData/redirect behavior reloads the editor accurately.

- [ ] **Step 6: Run script and unified editor tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~UnifiedEditorImportScriptTests|FullyQualifiedName~UnifiedEditorFlowTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add wwwroot/js/unified-editor.js tests/ltwnc.Tests/Views/UnifiedEditorImportScriptTests.cs
git commit -m "feat: wire file import preview in unified editor"
```

### Task 7: Tích hợp, browser QA và xác minh toàn bộ

**Files:**
- Modify only if a verified integration defect requires it.
- Test: existing full suite and manual browser flow.

**Interfaces:**
- End-to-end flow: new/existing editor → Import → preview CSV/XLSX → append/replace → reloaded editor result.

- [ ] **Step 1: Run build and full test suite**

```powershell
dotnet build ltwnc.csproj -c Release --no-restore
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj -c Release --no-restore
```

Expected: build exit 0; all tests pass with zero failures.

- [ ] **Step 2: Run source checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intended feature changes plus the pre-existing local `appsettings.json`.

- [ ] **Step 3: Browser smoke test**

Verify on the running local app:

1. Existing set opens modal with file tab selected.
2. Valid CSV previews all counts/fields and appends.
3. Replace shows warning and replaces cards.
4. Invalid header shows inline error and disables commit.
5. Paste tab still imports term/definition.
6. New set asks to save before import.
7. Modal works at desktop and mobile widths.

- [ ] **Step 4: Final regression commit if QA required a fix**

```powershell
git add Controllers/FlashcardSetController.cs Services/FlashcardSets Models/ViewModels/FlashcardSet Views/FlashcardSet/Editor.cshtml wwwroot/css/unified-editor.css wwwroot/js/unified-editor.js wwwroot/templates/flashcard-import-template.csv tests/ltwnc.Tests
git commit -m "fix: harden unified editor file import flow"
```

Skip this commit when QA requires no additional source changes.
