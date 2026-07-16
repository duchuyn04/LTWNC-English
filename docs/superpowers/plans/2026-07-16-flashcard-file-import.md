# Nhập thẻ từ file Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cho phép chủ sở hữu nhập nhiều flashcard hợp lệ từ CSV UTF-8 hoặc XLSX vào một bộ thẻ hiện có, đồng thời báo lỗi theo từng dòng.

**Architecture:** Tạo `IFlashcardImportService` và service triển khai trong `Services/FlashcardSets`. Service dùng abstraction parser chung để tách CSV/XLSX, xác thực header và từng dòng, sau đó lưu hàng hợp lệ trong một lần `SaveChangesAsync`; controller chỉ nhận HTTP và chuyển result vào TempData. View Edit thêm form upload, hướng dẫn header và báo cáo lỗi.

**Tech Stack:** ASP.NET Core MVC trên .NET 10, EF Core 10, CsvHelper 33.1.0 cho CSV, ClosedXML 0.105.0 cho XLSX, xUnit và EF Core InMemory cho unit/integration-style tests.

## Global Constraints

- Chỉ nhận `.csv` và `.xlsx`; XLS/XLSM không được hỗ trợ.
- Hàng đầu tiên là header chuẩn, không phân biệt hoa/thường và trim khoảng trắng; không ánh xạ header tùy ý.
- Sáu trường bắt buộc là `Thuật ngữ`, `Định nghĩa`, `IPA`, `Loại từ`, `Ví dụ tiếng Anh`, `Nghĩa ví dụ tiếng Việt`.
- Hai trường tùy chọn là `Từ đồng nghĩa` và `URL ảnh`; import chỉ lưu URL ảnh, không upload ảnh nhị phân.
- Hàng trống hoàn toàn được bỏ qua; hàng lỗi bị bỏ qua và không chặn hàng hợp lệ.
- Chỉ owner của set được import; không cập nhật/xóa/ghi đè thẻ hiện có.
- `OrderIndex` của hàng hợp lệ tiếp tục sau giá trị lớn nhất hiện tại theo đúng thứ tự file.
- Không lưu file upload sau khi xử lý; mọi request POST phải có antiforgery.
- Giới hạn file import là 10 MB; file vượt giới hạn bị từ chối trước khi parse.
- Không sửa các thay đổi chưa commit hiện có trong `README.md`, `Views/Study/Flashcard.cshtml`, `appsettings.json`.

---

### Task 1: Thêm dependency và contract dữ liệu import

**Files:**
- Modify: `ltwnc.csproj`
- Create: `Services/FlashcardSets/IFlashcardImportService.cs`
- Create: `Models/ViewModels/FlashcardSet/FlashcardImportResultViewModel.cs`
- Create: `Models/ViewModels/FlashcardSet/FlashcardImportErrorViewModel.cs`
- Test: `tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportContractTests.cs`

**Interfaces:**
- Produces `IFlashcardImportService.ImportAsync(int setId, string userId, IFormFile file, CancellationToken cancellationToken = default)`, returning `FlashcardImportResult`.
- `FlashcardImportResult` exposes `int ImportedCount`, `int SkippedCount`, `IReadOnlyList<FlashcardImportError> Errors`.
- `FlashcardImportError` exposes `int RowNumber`, `string Reason`.

- [ ] **Step 1: Write the failing contract test**

```csharp
[Fact]
public void Import_result_preserves_counts_and_row_errors()
{
    var result = new FlashcardImportResult
    {
        ImportedCount = 2,
        SkippedCount = 1,
        Errors = new[] { new FlashcardImportError { RowNumber = 4, Reason = "IPA không được để trống." } }
    };

    Assert.Equal(2, result.ImportedCount);
    Assert.Equal(1, result.SkippedCount);
    Assert.Equal(4, result.Errors.Single().RowNumber);
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardImportContractTests`

Expected: FAIL because result types do not exist.

- [ ] **Step 3: Add packages and minimal contract types**

Add `CsvHelper` version `33.1.0` and `ClosedXML` version `0.105.0` package references to `ltwnc.csproj`. Define the result/error types as immutable-friendly classes with initialized empty error lists, and define the service interface with the exact signature above.

- [ ] **Step 4: Run the focused test**

Run the same command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ltwnc.csproj Services/FlashcardSets/IFlashcardImportService.cs Models/ViewModels/FlashcardSet/FlashcardImportResultViewModel.cs Models/ViewModels/FlashcardSet/FlashcardImportErrorViewModel.cs tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportContractTests.cs
git commit -m "feat: add flashcard import contracts"
```

### Task 2: Implement CSV/XLSX parsing and row validation

**Files:**
- Create: `Services/FlashcardSets/IFlashcardFileParser.cs`
- Create: `Services/FlashcardSets/FlashcardImportRow.cs`
- Create: `Services/FlashcardSets/CsvFlashcardFileParser.cs`
- Create: `Services/FlashcardSets/XlsxFlashcardFileParser.cs`
- Create: `Services/FlashcardSets/FlashcardFileParserResolver.cs`
- Create: `Services/FlashcardSets/FlashcardImportValidation.cs`
- Test: `tests/ltwnc.Tests/Services/FlashcardSets/FlashcardFileParserTests.cs`

**Interfaces:**
- `IFlashcardFileParser.ParseAsync(Stream stream, CancellationToken cancellationToken = default)` returns `Task<FlashcardFileParseResult>`.
- `FlashcardImportRow` contains `int RowNumber`, the six required strings, and nullable `Synonyms`/`ImageUrl`.
- `FlashcardFileParseResult` contains either normalized header failure information or parsed rows; a missing required header is a file-level error and yields no rows.
- Resolver selects parser by lower-cased extension `.csv`/`.xlsx`; unsupported extensions throw a typed `FlashcardImportException`.

- [ ] **Step 1: Write failing parser tests**

Cover these exact cases: quoted CSV fields containing commas and newlines; UTF-8 Vietnamese header/data; XLSX first worksheet; case-insensitive trimmed headers; missing required header; empty rows; required blank cell; `PartOfSpeech` over 80 characters.

```csharp
[Fact]
public async Task Csv_parser_keeps_quoted_comma_and_newline()
{
    const string csv = "Thuật ngữ,Định nghĩa,IPA,Loại từ,Ví dụ tiếng Anh,Nghĩa ví dụ tiếng Việt\n" +
                       "run, chạy,/rʌn/,verb,\"Run, Forest, run!\",Hãy chạy!\n";
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

    FlashcardFileParseResult result = await _csv.ParseAsync(stream);

    Assert.Equal("Run, Forest, run!", result.Rows.Single().ExampleSentence);
}
```

- [ ] **Step 2: Run parser tests and verify failure**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardFileParserTests`

Expected: FAIL because parser classes do not exist.

- [ ] **Step 3: Implement parsers and shared normalization**

Use CsvHelper with invariant configuration and UTF-8 stream reading; use ClosedXML to read only the first worksheet and convert cell values to strings. Normalize headers with `Trim().ToUpperInvariant()` for matching while retaining original row numbers (header is row 1, first data row is row 2). Treat all-whitespace data rows as empty. Run required-field and 80-character validation through `FlashcardImportValidation`; optional whitespace becomes null.

- [ ] **Step 4: Run parser tests**

Run the focused command. Expected: PASS for all parser/validation cases.

- [ ] **Step 5: Commit**

```bash
git add Services/FlashcardSets tests/ltwnc.Tests/Services/FlashcardSets/FlashcardFileParserTests.cs
git commit -m "feat: parse and validate flashcard import files"
```

### Task 3: Implement transactional import service and register DI

**Files:**
- Create: `Services/FlashcardSets/FlashcardImportService.cs`
- Modify: `Program.cs:38` (service registrations)
- Modify: `Services/FlashcardSets/FlashcardSetService.cs` (extract/reuse required text normalization only if needed; update `UpdatedAt` in import path)
- Test: `tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs`

**Interfaces:**
- Consumes `IFlashcardFileParser` implementations through resolver, `AppDbContext`, and `IFormFile`.
- Produces `FlashcardImportResult`; file-level failures return a result with zero imported rows and one general error or throw a typed exception that the controller maps to a user message.

- [ ] **Step 1: Write failing service tests**

Seed an owned set with an existing card at `OrderIndex = 7`, construct `FormFile` instances in memory, and test: valid CSV imports two cards at indexes 8/9; mixed valid/invalid imports only valid rows and reports original line; non-owner imports zero; missing header imports zero; XLSX imports first sheet.

```csharp
[Fact]
public async Task Mixed_rows_import_valid_cards_and_report_original_row()
{
    var result = await _service.ImportAsync(SetId, OwnerId, FormFileForCsv(MixedCsv), CancellationToken.None);

    Assert.Equal(1, result.ImportedCount);
    Assert.Equal(1, result.SkippedCount);
    Assert.Equal(3, result.Errors.Single().RowNumber);
    Assert.Equal(new[] { 8, 9 }, await _context.Flashcards
        .Where(card => card.FlashcardSetId == SetId).OrderBy(card => card.OrderIndex)
        .Select(card => card.OrderIndex).ToArrayAsync());
}
```

- [ ] **Step 2: Run service tests and verify failure**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardImportServiceTests`

Expected: FAIL because service is not implemented/registered.

- [ ] **Step 3: Implement import service**

Resolve the parser from `Path.GetExtension(file.FileName)`, validate file presence and the 10 MB upload limit before parsing, load the set with cards and enforce `set.UserId == userId`. Parse rows, collect row errors without throwing, compute the next order index from the current maximum, create only valid `Flashcard` entities with `IsStarred = false` and `UploadedImagePath = null`, set `UpdatedAt` when at least one row is imported, and call `SaveChangesAsync` once. Register `AddScoped<IFlashcardImportService, FlashcardImportService>()` and all parser implementations/resolver in `Program.cs`.

- [ ] **Step 4: Run service tests**

Run the focused command. Expected: PASS, including no database mutation for file-level errors and unauthorized sets.

- [ ] **Step 5: Commit**

```bash
git add Services/FlashcardSets/FlashcardImportService.cs Program.cs Services/FlashcardSets/FlashcardSetService.cs tests/ltwnc.Tests/Services/FlashcardSets/FlashcardImportServiceTests.cs
git commit -m "feat: import flashcards into owned sets"
```

### Task 4: Add controller endpoint, sample template, and Edit UI

**Files:**
- Modify: `Controllers/FlashcardSetController.cs`
- Modify: `Views/FlashcardSet/Edit.cshtml`
- Create: `wwwroot/templates/flashcard-import-template.csv`
- Test: `tests/ltwnc.Tests/Controllers/FlashcardSetImportControllerTests.cs`

**Interfaces:**
- Controller action: `[HttpPost] [Route("/Set/{id}/Import")] Task<IActionResult> Import(int id, IFormFile? file)`.
- Consumes `IFlashcardImportService` and `ICurrentUser`; redirects to `Edit` with TempData keys `Success`, `Error`, and serialized import errors.

- [ ] **Step 1: Write failing controller tests**

Test unauthenticated user returns `Challenge`; authenticated owner delegates to service and redirects to Edit; typed file-level exception sets `TempData["Error"]`; result errors are exposed for the view; antiforgery attribute is present on the action.

- [ ] **Step 2: Run controller tests and verify failure**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~FlashcardSetImportControllerTests`

Expected: FAIL because action and UI do not exist.

- [ ] **Step 3: Implement endpoint and UI**

Inject `IFlashcardImportService`, preserve current owner/Challenge behavior, and redirect after every POST. Add an `enctype="multipart/form-data"` upload form in the Edit page with `accept=".csv,.xlsx"`, six required headers/two optional headers, and a link to the CSV template. Render imported count and each `RowNumber`/`Reason` from TempData safely; do not echo file contents. Add the template with the exact eight headers and one valid example row.

- [ ] **Step 4: Run controller tests and full suite**

Run focused controller tests, then `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Controllers/FlashcardSetController.cs Views/FlashcardSet/Edit.cshtml wwwroot/templates/flashcard-import-template.csv tests/ltwnc.Tests/Controllers/FlashcardSetImportControllerTests.cs
git commit -m "feat: add flashcard import UI"
```

### Task 5: Verification and documentation

**Files:**
- Modify: `README.md`
- Test: existing full test suite and manual browser flow

- [ ] **Step 1: Update README**

Document the two supported extensions, exact header names, optional columns, partial-import behavior, and the `/Set/{id}/Edit` entry point. Include a short CSV example and state that XLSX reads the first worksheet.

- [ ] **Step 2: Run verification commands**

Run `dotnet build ltwnc.csproj`, `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`, and `git diff --check`. Expected: build succeeds, all tests pass, and diff check has no whitespace errors.

- [ ] **Step 3: Perform manual smoke test**

Start the app with `dotnet run`, create/open an owned set, import the sample CSV, import a mixed-validity CSV, confirm the count and row error report, then confirm the cards appear after refresh and existing cards remain unchanged.

- [ ] **Step 4: Commit documentation and verified implementation**

```bash
git add README.md
git commit -m "docs: document flashcard file import"
```

## Plan self-review

- Spec coverage: format, UI, permission, partial success, row errors, order, updated timestamp, parser separation, no file retention, and tests are covered by Tasks 1–5.
- Placeholder scan: no `TBD`, `TODO`, or unspecified “handle errors” steps remain; the import file limit is explicitly 10 MB.
- Type consistency: `ImportAsync` returns `FlashcardImportResult`; parser rows use `FlashcardImportRow`; controller consumes the service result and maps it to TempData.
