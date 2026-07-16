# AJAX Batch Card Actions

**Date:** 2026-07-16
**Status:** Approved design, pending implementation
**Scope:** Make Delete, Star, and Unstar batch actions update the flashcard-set editor without reloading the page.

## Problem

The batch toolbar currently submits a normal HTML form. Every Delete, Star, or Unstar action redirects back to the edit page, even though only the selected card rows and their detail panels changed. This interrupts the user's position in the editor and adds unnecessary page work.

## Goals

- Run all three batch actions without a full-page reload when JavaScript is available.
- Update the card list, detail panels, selection state, toolbar, and feedback in place.
- Keep normal form submission and redirect as progressive-enhancement fallback.
- Preserve persistent undo logs and the existing authorization and anti-forgery protection.
- Preserve the project's Command + Factory architecture.

## Approaches Considered

### 1. Fetch JSON and update the existing DOM (selected)

The existing endpoint returns JSON only for an AJAX request. The editor script applies the result to existing elements. This sends the smallest response and reuses the current DOM and `setStarState` behavior.

### 2. Fetch a server-rendered partial

The server would render the complete list and detail region after every action. This reduces client-side DOM logic but sends much more HTML and requires a new partial-rendering boundary.

### 3. Optimistic update before the server responds

The UI would change immediately and roll back if the request failed. This can feel slightly faster but requires snapshots and rollback logic, especially for deletion.

Approach 1 is the smallest reliable extension of the current editor.

## Architecture and Pattern Boundaries

The server-side execution path remains unchanged:

```text
Edit.cshtml batch form
    -> POST /Set/{setId}/BatchAction
    -> CardActionsController (HTTP, auth, response format)
    -> ICardActionCommandFactory.Create(...)
    -> ICardActionService.ExecuteAsync(command)
    -> DeleteCardsCommand / StarCardsCommand / UnstarCardsCommand
    -> database + CardActionLog
```

The AJAX change affects only the transport and presentation layers:

- `CardActionsController` continues to validate identity and ownership, asks the factory for an `ICardActionCommand`, and passes it to `ICardActionService`.
- The controller does not query or mutate flashcards directly and does not switch on an action to implement business behavior.
- Commands continue to own Execute/Undo behavior and snapshots.
- The service continues to invoke commands and persist `CardActionLog`.
- JavaScript only mirrors a confirmed server result in the DOM. It is not the source of truth for the operation.
- No new repository, service, command hierarchy, endpoint, or dependency is introduced.

This keeps the README contract intact: controllers handle request/authorization/View/Redirect/JSON, while card-action business behavior stays in `Services/CardActions` under Command + Factory.

## Request and Response Contract

The existing `#batch-form` remains a normal POST form with its anti-forgery token. The editor script intercepts its `submit` event and sends `FormData` to the form's existing action URL. The clicked submit button supplies the `action` value.

The request includes an AJAX marker (`X-Requested-With: XMLHttpRequest`) and requests JSON. Without that marker, the action keeps its current TempData + redirect response.

Successful AJAX response:

```json
{
  "success": true,
  "message": "Đã đánh sao 3 thẻ.",
  "action": "Star",
  "cardIds": [12, 15, 18],
  "undoLogId": 42
}
```

Failed AJAX response:

```json
{
  "success": false,
  "message": "Không thể thực hiện thao tác."
}
```

An empty selection returns HTTP 400 for AJAX and retains the current redirect error for normal form submission. Authentication and authorization responses remain HTTP 401/403 behavior. Unexpected command/service errors return a non-success JSON status without changing the DOM.

## Client Behavior

While a request is running, the three batch buttons are disabled so duplicate commands cannot be sent. The UI changes only after a successful JSON response.

### Star and Unstar

For every returned card id, the script calls the existing shared star-state updater. It synchronizes:

- the `★`/`☆` symbol in the list;
- the star checkbox in the matching detail form;
- `aria-checked` and visual starred classes.

### Delete

For every returned card id, the script removes:

- its list wrapper and selection checkbox;
- its matching detail panel.

If the active card was deleted, the first remaining card becomes active. If no cards remain, the editor shows its empty state and has no active detail panel.

### Completion and feedback

After success, all remaining batch checkboxes are unchecked and the sidebar toolbar becomes hidden. A live feedback region displays the returned message and an Undo action linked to `undoLogId`. Undo remains the existing server-backed operation; making Undo AJAX is outside this change.

On failure, selected checkboxes and DOM content remain unchanged, the buttons are re-enabled, and the feedback region displays the server message or a generic retry message.

## Progressive Enhancement

The form, button names, action values, endpoint, anti-forgery token, TempData success/error messages, redirect, and Undo form all remain valid without JavaScript. The AJAX path is an enhancement rather than a replacement for MVC form behavior.

## Testing

- Controller tests verify AJAX success JSON, AJAX validation/error JSON, and unchanged normal redirect behavior.
- Script contract tests verify submit interception, `fetch` with `FormData`, pending-state protection, success DOM paths for Delete/Star/Unstar, and failure handling.
- Markup tests verify a live feedback/empty-state target and retain the form fallback contract.
- Existing Command, Factory, service, ownership, anti-forgery, and editor tests remain green.
- Full suite: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore`.
- JavaScript syntax: `node --check wwwroot/js/flashcard-editor.js`.

## Acceptance Criteria

- Delete, Star, and Unstar complete without a full-page reload when JavaScript is enabled.
- Star state stays synchronized between list and detail UI.
- Deleting selected cards removes their list rows and detail panels and selects a valid remaining card.
- Selection clears and the batch toolbar hides after success.
- Errors do not leave the UI showing an unconfirmed state.
- A success message and Undo action remain available after an AJAX action.
- Normal form submission still works when JavaScript is unavailable.
- `CardActionsController` still delegates creation and execution to `ICardActionCommandFactory` and `ICardActionService`; command classes remain the only owners of batch mutation and undo logic.

## Out of Scope

- AJAX Undo.
- Optimistic updates and rollback.
- Re-rendering the complete editor from a partial view.
- New batch action types or changes to command persistence.
