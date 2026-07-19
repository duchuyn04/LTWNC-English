# Quiz flow controls and timer design

## Goal

Improve the existing quiz study flow with restart and read-only question navigation, configurable timed attempts, reliable switching between retry modes, and duplicated result actions at the top and bottom of long result pages.

## Confirmed behavior

- Previous answered questions are review-only. Users cannot change an answer after it has been graded.
- Time presets are 5, 10, 15, and 20 minutes, with a custom duration from 1 through 120 minutes.
- Time is selected on a setup screen before an attempt starts.
- When time expires, the attempt is submitted automatically and unanswered questions count as wrong.
- Restarting during an attempt creates a new session, reshuffles questions and choices, and resets the timer.
- Starting a different retry mode abandons any active retry attempt and creates the newly requested attempt immediately.
- Result actions appear at both the top and bottom of the result page.

## Architecture

The server remains authoritative for attempt state, answers, and expiration. The browser renders a countdown from server-provided remaining time, but every question read and answer write verifies the deadline on the server. This keeps the result correct after refreshes, multiple tabs, delayed requests, or direct API calls.

`StudySession` will store nullable quiz timing fields:

- `QuizStartedAtUtc`: UTC instant when a timed quiz begins.
- `QuizTimeLimitSeconds`: validated duration for the attempt.

An active quiz session is defined as `Mode == Quiz`, `Score == null`, and `CompletedAt == null`. The filtered unique index will use all three conditions. Replaced sessions are abandoned by setting `CompletedAt` while leaving `Score` null, which preserves history without allowing them to block a new active attempt.

## Setup and attempt creation

`GET /Study/{setId}/Quiz` renders a new setup view instead of immediately creating or resuming an attempt. It shows the four presets and a custom minute field. When an active attempt exists, the setup view also offers a link to continue it.

`POST /Study/{setId}/Quiz/Start` validates the selected duration, abandons any active quiz session for the same user and set, creates a new question set from the current study filters, stores the timing fields, and redirects to the first question.

The accepted duration is 1 through 120 whole minutes. Invalid or unavailable attempts return the user to setup with a validation or availability message without creating a partial session.

## Timer and expiration

The question view receives the authoritative deadline and remaining seconds. JavaScript updates the visible `mm:ss` countdown and changes it to a warning style during the final 60 seconds.

At zero, JavaScript submits an antiforgery-protected timeout request. The following server operations also check expiration before proceeding:

- loading any quiz question;
- grading an answer;
- restarting the attempt.

Expiration marks every pending question as `IsCorrect = false` and sets `AnsweredAt`, while leaving `SelectedChoiceIndex` null. Completion logic treats `IsCorrect == null` as pending. The result screen renders null selections as `Chưa trả lời`, includes them in wrong answers, and calculates the score over every question.

The timeout operation is idempotent. Repeated browser requests or a simultaneous answer and timeout cannot publish duplicate completion events or produce multiple scores.

## Question navigation

The question route accepts an optional question identifier for review navigation. Without it, the service selects the first pending question. With it, the service returns that question only when it belongs to the current session.

The state returned to the view includes:

- the question order and total count;
- previous and next review question identifiers;
- the current pending question identifier;
- selected and correct choice indexes for answered questions;
- whether the page is review-only;
- the deadline and remaining seconds.

The normal unanswered question keeps the existing grading flow. A reviewed answered question renders disabled choices with the stored correct/wrong states and cannot call the answer endpoint. It provides Previous/Next navigation and a direct `Quay lại câu đang làm` action.

## Restart and retry switching

The in-progress page contains an antiforgery-protected `Làm lại từ đầu` form with a confirmation prompt. Restart abandons the current attempt, creates a new attempt from the same flashcard scope, reshuffles directions and choices, reuses the current time limit, and redirects to its first question.

`RetryWrong` and `RetryAll` no longer return an unrelated active session. Each operation abandons any active quiz attempt for the same user and set inside the creation transaction, then creates the requested retry session:

- Retry wrong uses only wrong or timed-out questions and preserves their directions.
- Retry all uses all questions from the selected completed source session and generates directions again.

Both retry attempts reuse the source attempt's time limit. If older source sessions have no stored time limit, the default is 10 minutes.

## Result actions

A shared partial renders the three result actions. The partial appears immediately below the result summary and again after the review list:

- `Làm lại câu sai`, when wrong answers exist;
- `Làm lại tất cả`;
- `Về trang học`.

Both form groups include antiforgery tokens. The duplicated controls share the same accessible labels and responsive styling.

## Error handling and concurrency

- Ownership checks remain mandatory for setup, question reads, answers, timeout, restart, and retries.
- Invalid durations return model validation errors.
- Expired questions cannot accept answers; the response directs the browser to the result page.
- Retry creation and active-session abandonment run transactionally.
- The filtered unique index remains the final concurrency guard for one active quiz attempt per user and set.
- Concurrent timeout/completion calls update the session only when it is still active and publish at most one completion event.

## Testing

- Service tests cover duration validation, persisted deadlines, server-side expiration, unanswered questions counted as wrong, idempotent timeout, read-only review state, restart, and retry-mode switching.
- Controller tests cover setup GET/POST, previous-question navigation, timeout, restart, and authentication/antiforgery response behavior.
- View and JavaScript contract tests cover presets, custom time validation, countdown data, previous/restart controls, disabled review choices, timeout submission, and duplicated result actions.
- Migration tests or model assertions cover nullable timing columns and the updated filtered unique index.
- The full test suite and `dotnet build --no-restore` must pass.
