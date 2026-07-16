# AJAX Batch Card Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute Delete, Star, and Unstar from the set editor without reloading while retaining MVC fallback and Command + Factory.

**Architecture:** CardActionsController keeps the authorization → factory → service path and varies only its HTTP response for XMLHttpRequest. Razor retains a normal POST form and adds DOM targets; flashcard-editor.js intercepts submission and mirrors confirmed JSON results into the UI.

**Tech Stack:** ASP.NET Core MVC .NET 10, Razor, vanilla JavaScript fetch/FormData, xUnit, Moq.

## Global Constraints

- Preserve ICardActionCommandFactory.Create and ICardActionService.ExecuteAsync as the controller execution path.
- Mutation and undo stay in DeleteCardsCommand, StarCardsCommand, and UnstarCardsCommand.
- Keep authorization, ownership, anti-forgery, CardActionLog, TempData, redirect, and normal form fallback.
- Add no service, repository, endpoint, dependency, or action type.
- AJAX Undo and optimistic updates are out of scope.
- Do not stage .superpowers/sdd/task-2-report.md or pre-existing uncommitted hunks in wwwroot/css/edit.css.

## File Map

- Create tests/ltwnc.Tests/Controllers/CardActionsControllerTests.cs for transport and delegation tests.
- Modify Controllers/CardActionsController.cs only for AJAX response selection.
- Modify Views/FlashcardSet/Edit.cshtml and its markup tests for feedback/empty-state targets.
- Modify wwwroot/js/flashcard-editor.js and its contract tests for fetch and DOM updates.

---

### Task 1: Controller AJAX response contract

**Files:**
- Create: tests/ltwnc.Tests/Controllers/CardActionsControllerTests.cs
- Modify: Controllers/CardActionsController.cs

**Interfaces:**
- Consumes: ICardActionCommandFactory.Create(string, int, string, IReadOnlyList<int>).
- Consumes: ICardActionService.ExecuteAsync(ICardActionCommand).
- Produces successful JSON: success, message, action, cardIds, undoLogId.
- Produces failed JSON: success, message.

- [ ] **Step 1: Write failing tests**

Build the controller with mocked action service, factory, set service, and current user. Attach DefaultHttpContext and TempDataDictionary. Mark AJAX with:

    controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith =
        "XMLHttpRequest";

Test AJAX success with set 9, user-1, Star, card ids 3 and 5, and log id 41. Assert JsonResult fields and verify exactly these calls:

    factory.Create("Star", 9, "user-1", new[] { 3, 5 });
    actionService.ExecuteAsync(command);

Test AJAX empty selection returns BadRequestObjectResult with success false and never calls factory/service. Test a thrown InvalidOperationException returns HTTP 500 JSON with success false. Test a non-AJAX request still returns RedirectToActionResult to FlashcardSet.Edit and stores UndoLogId in TempData.

- [ ] **Step 2: Verify RED**

Run:

    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~CardActionsControllerTests" --no-restore

Expected: AJAX tests fail because BatchAction always redirects.

- [ ] **Step 3: Implement minimal response branching**

Add:

    private bool IsAjaxRequest() =>
        string.Equals(
            Request.Headers.XRequestedWith,
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

For empty AJAX selection return:

    return BadRequest(new { success = false, message });

Keep the existing factory and service calls. After ExecuteAsync succeeds, return for AJAX:

    return Json(new
    {
        success = true,
        message,
        action = action.ToString(),
        cardIds = selectedCardIds,
        undoLogId = log.Id
    });

For caught exceptions on AJAX return:

    return StatusCode(
        StatusCodes.Status500InternalServerError,
        new { success = false, message = ex.Message });

Use the current TempData and redirect code for non-AJAX. Do not add EF access, an action switch, or concrete command construction.

- [ ] **Step 4: Verify GREEN and architecture tests**

Run:

    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~CardActionsControllerTests|FullyQualifiedName~CardActionCommandFactoryTests|FullyQualifiedName~DeleteCardsCommandTests" --no-restore

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

    git add -- Controllers/CardActionsController.cs tests/ltwnc.Tests/Controllers/CardActionsControllerTests.cs
    git commit -m "feat: return ajax batch action results"

---

### Task 2: Progressive-enhancement markup targets

**Files:**
- Modify: tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs
- Modify: Views/FlashcardSet/Edit.cshtml

**Interfaces:**
- Produces: #batch-feedback with aria-live=polite.
- Produces: data-empty-card-list and #batch-form[data-undo-url-prefix].
- Retains normal method=post, action buttons, and anti-forgery token.

- [ ] **Step 1: Write failing markup test**

Add Batch_form_exposes_ajax_feedback_empty_state_and_undo_metadata. Assert:

    Assert.Matches(
        new Regex("id=\"batch-feedback\"[^>]+aria-live=\"polite\""),
        Source);
    Assert.Contains("data-empty-card-list", Source);
    Assert.Matches(
        new Regex("<form[^>]+id=\"batch-form\"[^>]+data-undo-url-prefix="),
        Source);
    Assert.Contains("@Html.AntiForgeryToken()", Source);

- [ ] **Step 2: Verify RED**

    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~FlashcardSetEditMarkupTests.Batch_form_exposes_ajax_feedback_empty_state_and_undo_metadata" --no-restore

Expected: FAIL because the targets do not exist.

- [ ] **Step 3: Add minimal Razor targets**

Near the TempData alerts add:

    <div id="batch-feedback" aria-live="polite"></div>

Inside .vocab-list render one reusable state:

    <p data-empty-card-list @(cards.Any() ? "hidden" : null)
       style="color: #787774;">Chưa có thẻ nào.</p>

Remove the duplicate conditional empty paragraph. Add to the existing batch form:

    data-undo-url-prefix="@Url.Content("~/CardActions/Undo/")"

Do not change its controller, action, route id, method, token, or buttons.

- [ ] **Step 4: Verify all view tests**

    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~FlashcardSetEditMarkupTests|FullyQualifiedName~FlashcardEditorScriptTests|FullyQualifiedName~FlashcardSetEditStyleTests" --no-restore

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

    git add -- Views/FlashcardSet/Edit.cshtml tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs
    git commit -m "feat: add batch action feedback targets"

---

### Task 3: Fetch submission and confirmed DOM updates

**Files:**
- Modify: tests/ltwnc.Tests/Views/FlashcardEditorScriptTests.cs
- Modify: wwwroot/js/flashcard-editor.js

**Interfaces:**
- Consumes Task 1 JSON and Task 2 targets.
- Reuses setStarState, selectCard, syncBatchToolbar, and syncEditorPanelHeights.
- Adds submitBatchAction, applyBatchResult, removeCards, and showBatchFeedback.

- [ ] **Step 1: Write failing script tests**

Add Batch_form_submits_with_fetch_formdata_and_ajax_header asserting:

    "function submitBatchAction(form, submitter)"
    "new FormData(form)"
    "formData.append('action', submitter.value)"
    "'X-Requested-With': 'XMLHttpRequest'"
    "fetch(form.action"
    "event.submitter"

Add Confirmed_batch_result_updates_star_delete_selection_and_feedback asserting:

    "function applyBatchResult(form, result)"
    "setStarState(cardId, result.action === 'Star')"
    "function removeCards(cardIds)"
    "wrapper?.remove()"
    "panel?.remove()"
    "input.checked = false"
    "syncBatchToolbar(form)"
    "showBatchFeedback"

- [ ] **Step 2: Verify RED**

    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~FlashcardEditorScriptTests" --no-restore

Expected: the new tests fail because batch submit is not intercepted.

- [ ] **Step 3: Implement confirmed-result helpers**

removeCards(cardIds) must locate each selected checkbox, remove its closest .vocab-list-item-wrapper and matching [data-card-panel], show data-empty-card-list only when no list item remains, select the first remaining card, and resync panel height.

applyBatchResult(form, result) must execute:

    if (result.action === 'Delete') {
        removeCards(cardIds);
    } else if (result.action === 'Star' || result.action === 'Unstar') {
        cardIds.forEach(function (cardId) {
            setStarState(cardId, result.action === 'Star');
        });
    }

Then uncheck every selectedCardIds input, call syncBatchToolbar(form), and call showBatchFeedback with result.message and result.undoLogId.

showBatchFeedback must use createElement/textContent, not server text in innerHTML. It clears #batch-feedback, renders a role=alert message, and on success creates a normal POST Undo form using form.dataset.undoUrlPrefix plus undoLogId. Copy the batch form's __RequestVerificationToken value into the Undo form.

- [ ] **Step 4: Implement fetch with pending/error handling**

submitBatchAction(form, submitter) must return when no submitter or data-batch-pending=true. Otherwise:

    const formData = new FormData(form);
    formData.append('action', submitter.value);

Set data-batch-pending=true and disable toolbar buttons. Fetch form.action with POST, body formData, Accept application/json, and X-Requested-With XMLHttpRequest. Parse JSON; apply results only when response.ok and result.success are true. On error retain selection/DOM and call showBatchFeedback with the returned or generic message. In finally remove pending and re-enable buttons.

In bindBatchSelection add once per form:

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        submitBatchAction(form, event.submitter);
    });

Keep the Delete button confirm attribute as confirmation and no-JavaScript fallback.

- [ ] **Step 5: Verify syntax and focused behavior**

    node --check wwwroot/js/flashcard-editor.js
    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~FlashcardEditorScriptTests|FullyQualifiedName~FlashcardSetEditMarkupTests|FullyQualifiedName~CardActionsControllerTests" --no-restore

Expected: syntax exits 0 and all selected tests pass.

- [ ] **Step 6: Commit without user CSS**

    git add -- wwwroot/js/flashcard-editor.js tests/ltwnc.Tests/Views/FlashcardEditorScriptTests.cs
    git commit -m "feat: update batch card actions without reload"

---

### Task 4: Full verification and architecture audit

**Files:**
- Read: README.md
- Read: Controllers/CardActionsController.cs
- Read: Services/CardActions/*.cs
- Verify all Task 1–3 files.

**Interfaces:** Verifies earlier contracts and produces no new API.

- [ ] **Step 1: Run complete verification**

    node --check wwwroot/js/flashcard-editor.js
    dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
    git diff --check
    git status --short

Expected: Node exits 0 and all .NET tests pass. Status may still show only preserved user changes in .superpowers/sdd/task-2-report.md and wwwroot/css/edit.css.

- [ ] **Step 2: Audit README pattern boundaries**

Confirm Controller has no EF access or concrete command creation; it calls factory then service exactly once. Confirm JavaScript changes DOM only after confirmed success. Confirm commands, snapshots, CardActionService, and CardActionLog are unchanged. Confirm form POST, anti-forgery, redirect fallback, TempData, and server Undo remain.

- [ ] **Step 3: Inspect commit scope**

    git log --oneline -5
    git diff HEAD~3..HEAD --stat
    git status --short

Expected: three focused implementation commits after this plan commit and no unrelated CSS/scratch report included.
