# User Achievements Progress Expansion Design

**Date:** 2026-07-11  
**Status:** Approved for implementation planning  
**Approach:** A — In-code catalog + live progress computation + Observer unlock + page rescan

## 1. Goal

Expand the **UserAchievements** business beyond binary unlock badges so learners see **clear progress** toward count-based milestones and get **actionable next steps**.

Success looks like:

- More count-tier achievements (cards mastered, sessions, dictation answers).
- Locked badges show **current / target**, a **progress bar**, and a **CTA** (e.g. link to study sets).
- Unlock still happens via **Observer** when the user studies.
- Opening `/Achievements` **rescans** and unlocks anything already earned (missed events, new catalog entries).
- Existing Observer pattern stays the extension point for “side effects after study.”

## 2. Non-goals (this cycle)

- XP, levels, leaderboards.
- Streak / calendar habits (phase later).
- Per-set mastery badges (phase later).
- Admin UI or database-stored achievement definitions.
- Real-time toast on every MarkLearned/Complete without a page load (optional later).
- Changing how `UserProgress` or study modes work beyond reading counts.

## 3. Current state (baseline)

| Piece | Today |
|-------|--------|
| Entity | `UserAchievement` (UserId, Code, Title, Description, UnlockedAt); unique (UserId, Code) |
| Catalog | Static `AchievementCatalog` — 5 definitions |
| Unlock | `AchievementStudyObserver` on study events |
| UI | `/Achievements` list locked/unlocked only |
| Progress | Not computed |

## 4. Catalog (medium count scope)

Catalog remains **code-defined** in `AchievementCatalog` (or a structured companion list). Each definition gains machine-readable progress metadata.

### 4.1 Achievement codes and rules

| Code | Title (VN) | Metric | Target | CTA hint |
|------|------------|--------|--------|----------|
| `first_card_mastered` | Thẻ đầu tiên đã thuộc | Cards mastered (`UserProgress.IsLearned`) | 1 | Học Flashcard / mở bộ thẻ |
| `cards_mastered_10` | Thuộc 10 thẻ | Cards mastered | 10 | same |
| `cards_mastered_25` | Thuộc 25 thẻ | Cards mastered | 25 | same |
| `cards_mastered_50` | Thuộc 50 thẻ | Cards mastered | 50 | same |
| `cards_mastered_100` | Thuộc 100 thẻ | Cards mastered | 100 | same |
| `first_flashcard_session` | Buổi Flashcard đầu tiên | Flashcard sessions completed | 1 | Vào Study Hub |
| `flashcard_sessions_5` | 5 buổi Flashcard | Flashcard sessions | 5 | same |
| `flashcard_sessions_10` | 10 buổi Flashcard | Flashcard sessions | 10 | same |
| `flashcard_sessions_20` | 20 buổi Flashcard | Flashcard sessions | 20 | same |
| `first_dictation_session` | Buổi Nghe chép đầu tiên | Dictation sessions completed | 1 | Bắt đầu Dictation |
| `dictation_sessions_5` | 5 buổi Nghe chép | Dictation sessions | 5 | same |
| `dictation_correct_10` | 10 câu nghe chép đúng | Dictation correct answers | 10 | Luyện Dictation |
| `dictation_correct_50` | 50 câu nghe chép đúng | Dictation correct answers | 50 | same |
| `dictation_perfect_session` | Nghe chép điểm tuyệt đối | Perfect dictation sessions (Score == 100) | 1 | Hoàn thành Dictation 100 điểm |

**Notes:**

- Existing codes `first_card_mastered`, `cards_mastered_10`, `first_flashcard_session`, `first_dictation_session`, `dictation_perfect_session` keep stable codes (no migration of existing rows).
- New codes are additive only.
- Title/Description stay user-facing Vietnamese, easy to read.

### 4.2 Metric definitions (exact queries)

All counts are **per UserId**, global across sets:

1. **CardsMastered**  
   `COUNT UserProgresses WHERE UserId = ? AND IsLearned = true`

2. **FlashcardSessions**  
   `COUNT StudySessions WHERE UserId = ? AND Mode = Flashcard`  
   (Any completed session row for that mode; same as today after Complete.)

3. **DictationSessions**  
   `COUNT StudySessions WHERE UserId = ? AND Mode = Dictation`

4. **DictationCorrectAnswers**  
   `COUNT DictationSessionDetails JOIN StudySessions WHERE session.UserId = ? AND detail.IsCorrect = true`

5. **DictationPerfectSessions**  
   `COUNT StudySessions WHERE UserId = ? AND Mode = Dictation AND Score = 100`

Progress for a definition:  
`Current = min(MetricValue, Target)`, `Percent = Current * 100 / Target` (Target > 0).  
Unlocked if `MetricValue >= Target` (or already has `UserAchievement` row).

## 5. Architecture

### 5.1 Components

```text
StudyService / DictationService
        │ PublishAsync(StudyEvent)
        ▼
StudyEventPublisher  (Subject)
        ├─► AchievementStudyObserver  → AchievementUnlockService.TryUnlockEligibleAsync
        └─► LoggingStudyObserver      (unchanged role)

AchievementsController.Index
        │
        ▼
AchievementService
        ├─ AchievementProgressService.GetSnapshotAsync(userId)  // metrics once
        ├─ AchievementUnlockService.SyncEligibleAsync(userId)   // rescan unlock
        └─ Build view models (catalog + progress + CTA + unlocked)
```

| Component | Responsibility |
|-----------|----------------|
| `AchievementCatalog` | Static definitions: Code, Title, Description, Metric kind, Target, CtaText, CtaPath template |
| `AchievementProgressService` | Load metric snapshot for a user (one round of queries) |
| `AchievementUnlockService` | Given snapshot (or recompute), insert missing `UserAchievement` rows; return newly unlocked codes |
| `AchievementStudyObserver` | On study events, call unlock service for that user (no duplicate rule ifs per badge tier) |
| `AchievementService` | Orchestrate for UI: sync + map catalog to view models |
| `AchievementsController` | Authorize, call service, TempData for newly unlocked titles |

### 5.2 Why not store Current/Target on `UserAchievement`

- Current values change every study action; storing them risks stale rows.
- Locked badges have no row today; progress still needs catalog + live metrics.
- Live compute is cheap (few aggregate queries per page / unlock).

`UserAchievement` schema **unchanged** this cycle (no new migration required for progress columns). New achievements only add rows at unlock time.

### 5.3 Catalog structure (conceptual)

```text
enum AchievementMetricKind {
  CardsMastered,
  FlashcardSessions,
  DictationSessions,
  DictationCorrectAnswers,
  DictationPerfectSessions
}

record AchievementDefinition(
  string Code,
  string Title,
  string Description,
  AchievementMetricKind Metric,
  int Target,
  string CtaText,   // e.g. "Học tiếp trong thư viện bộ thẻ"
  string CtaPath    // e.g. "/Set"
)
```

## 6. Data flows

### 6.1 Study-time unlock (existing path, generalized)

1. User marks learned / completes session / checks dictation answer.
2. Domain `SaveChanges` succeeds.
3. Publisher notifies observers.
4. `AchievementStudyObserver` calls `AchievementUnlockService.SyncEligibleAsync(userId)`.
5. Service loads snapshot, for each catalog item with `metric >= target` and no row, inserts unlock.
6. Observer failures still must not fail the study request (publisher catch + log).

### 6.2 Page-load rescan + progress UI

1. User opens `GET /Achievements`.
2. `AchievementService.GetCatalogWithStatusAsync`:
   - `SyncEligibleAsync` → list of **newly** unlocked codes this request (if any).
   - Controller puts friendly message in `TempData` when any new unlocks (e.g. “Bạn vừa mở: Thuộc 25 thẻ”).
   - Build list ordered: unlocked first (by UnlockedAt desc optional), then locked by progress percent desc or catalog order.
3. Each item view model includes:

| Field | Meaning |
|-------|---------|
| Code, Title, Description | From catalog |
| IsUnlocked | Row exists |
| UnlockedAt | From row if any |
| Current, Target | Progress numbers |
| ProgressPercent | 0–100 |
| CtaText, CtaUrl | Shown when **not** unlocked (optional when unlocked: hide CTA or show “Đã hoàn thành”) |

### 6.3 CTA rules

- Default paths:
  - Card metrics → `/Set` (user’s library).
  - Session / dictation metrics → `/Set` as well (user picks a set); no deep-link to last set required this cycle.
- If later we have “last studied setId”, can enrich CTA without changing unlock rules.

## 7. UI

### 7.1 `/Achievements` page

- Keep simple card list (current style).
- Locked card: muted, lock icon, **progress bar**, text `Current/Target`, CTA link button.
- Unlocked card: medal icon, unlock time, full opacity; no progress bar required (or show 100% filled).
- Banner/alert at top if TempData reports new unlocks from rescan.

### 7.2 Nav

- Existing “Thành tích” link stays.

### 7.3 Out of UI scope

- Confetti animations, badge detail modal, share image.

## 8. Error handling

| Case | Behavior |
|------|----------|
| Unlock insert race (unique index) | Catch/ignore duplicate; treat as already unlocked |
| Observer exception | Logged by publisher; study succeeds |
| Progress query fails | Page shows error/empty with safe message; no partial corrupt unlocks without transaction if multi-insert — use short transaction per sync batch optional |
| Unknown metric kind | Skip definition, log warning |

## 9. Testing

| Area | Cases |
|------|--------|
| Progress snapshot | Seed N mastered cards → Current matches; sessions/details counts |
| Unlock tiers | 25 cards → unlocks 1, 10, 25 not 50 |
| Idempotent unlock | Sync twice → no duplicate rows |
| Observer path | Publish CardProgress / SessionCompleted → unlocks when threshold met |
| Page rescan | User already has metric but no row → Index sync creates row + TempData |
| View model | Locked badge has Current < Target and CTA populated |

Use EF InMemory (or Sqlite where unique filters matter). Update existing achievement tests for new catalog shape and service split.

## 10. Implementation outline (for later plan)

1. Extend catalog model + definitions (medium list).
2. Add `AchievementProgressService` + `AchievementUnlockService`.
3. Thin `AchievementStudyObserver` to call unlock service only.
4. Expand `AchievementService` + view model + Index view (bar + CTA + TempData).
5. Tests + README note on progress/rescan.
6. No EF migration if entity unchanged.

## 11. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Page rescan cost | Single snapshot query set; catalog small (~15 items) |
| Observer always full-scans catalog | Acceptable; alternatives (event-specific filters) only if profiling needs it |
| Dictation correct count expensive | Indexed FKs already; count via join is fine at learning-app scale |
| Title copy drift between catalog and stored row | Keep copying Title/Description at unlock (current pattern); catalog is source for locked items |

## 12. Decisions log (brainstorm)

| Decision | Choice |
|----------|--------|
| Primary direction | Many badges + clear progress |
| Badge type first | Count-based |
| Progress UI | Bar + numbers + next-action CTA |
| Scope size | Medium (tiers + dictation correct counts) |
| Backfill | Rescan on achievements page |
| Architecture | In-code catalog + live metrics (Approach A) |
| Toast | TempData on rescan when new unlocks; not full in-session study toast |
| Progress storage | Not persisted on entity |

## 13. Open items explicitly closed

- No admin catalog editor.
- No set-scoped achievements this cycle.
- No streak.
- No change to Prototype / Command / Strategy modules.
