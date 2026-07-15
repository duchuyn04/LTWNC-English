# Service Interfaces Design

**Date:** 2026-07-16  
**Status:** Approved for planning  
**Goal:** Introduce interfaces for all application services so controllers and internal consumers depend on abstractions, enabling future swap / decorator implementations without changing call sites.

## Context

LTWNC is an ASP.NET Core MVC flashcard app. GoF seams already use interfaces:

| Pattern | Interfaces already present |
| --- | --- |
| Strategy | `IStudyModeStrategy`, `IStudyModeStrategyResolver`, `IStudyCardQueryService` |
| Command | `ICardActionCommand` |
| Observer | `IStudyEventObserver`, `IStudyEventPublisher` |
| Prototype | `IPrototype<T>` |

Application services still inject and register as **concrete classes**:

- `FlashcardSetService`, `StudyService`, `DictationService`
- `CardActionService`, `CardActionCommandFactory`
- `AchievementService`, `AchievementProgressService`, `AchievementUnlockService`

Primary motivation is **extensibility** (swap implementation, wrap decorator) rather than a full test rewrite.

## Goals

1. Every application service in scope has a matching interface.
2. Controllers and service-to-service / observer dependencies inject the interface.
3. DI registers `AddScoped<IAbstraction, Implementation>()`.
4. No behavior change: same public method signatures and lifetimes.

## Non-goals

- Interface Segregation (ISP) splits of fat services
- Real decorator classes in this work
- `AddApplicationServices` DI extension module
- Rewriting unit tests to use mocks / interfaces (tests may keep `new Concrete(...)`)
- Turning DTOs / helpers into interfaces (`DictationCheckResult`, `AchievementPageModel`, snapshots, …)
- Changing existing GoF interfaces or their DI registrations
- Changing business logic, exception types, or method signatures

## Approach

**Mechanical mirror, single batch (Approach 1).**

- One interface per concrete application service / factory
- Interface members = all **public instance methods** of the class (not constructors, not private/static helpers)
- Separate file next to implementation
- One PR-sized change: interfaces → implement → consumers → DI → verify

## Naming & file layout

| Interface | Implementation | File path | Namespace |
| --- | --- | --- | --- |
| `IFlashcardSetService` | `FlashcardSetService` | `Services/IFlashcardSetService.cs` | `ltwnc.Services` |
| `IStudyService` | `StudyService` | `Services/IStudyService.cs` | `ltwnc.Services` |
| `IDictationService` | `DictationService` | `Services/IDictationService.cs` | `ltwnc.Services` |
| `IAchievementService` | `AchievementService` | `Services/IAchievementService.cs` | `ltwnc.Services` |
| `IAchievementProgressService` | `AchievementProgressService` | `Services/IAchievementProgressService.cs` | `ltwnc.Services` |
| `IAchievementUnlockService` | `AchievementUnlockService` | `Services/IAchievementUnlockService.cs` | `ltwnc.Services` |
| `ICardActionService` | `CardActionService` | `Services/ICardActionService.cs` (alongside `CardActionService.cs` under `Services/`) | `ltwnc.Services.CardActions` |
| `ICardActionCommandFactory` | `CardActionCommandFactory` | `Services/CardActions/ICardActionCommandFactory.cs` | `ltwnc.Services.CardActions` |

Rules:

- Interface name = `I` + current class name
- Same namespace as the concrete type
- File name matches interface type name

## Interface surface (mirror public API)

Signatures must match the implementation at extract time (copy from class). Member lists below are the intended surface.

### `IFlashcardSetService`

- `GetMySetsAsync`
- `GetMySetsWithProgressAsync`
- `GetPublicSetsAsync`
- `SearchPublicSetsAsync`
- `GetSetByIdAsync`
- `GetAccessibleSetAsync`
- `GetSetWithCardsAsync`
- `GetAccessibleSetWithCardsAsync`
- `GetOwnedSetAsync`
- `GetExistingCopyAsync`
- `CopyPublicSetAsync`
- `CreateSetAsync`
- `UpdateSetAsync`
- `DeleteSetAsync`
- `AddCardAsync`
- `UpdateCardAsync`
- `DeleteCardAsync`
- `ToggleStarAsync`

Do **not** include private helpers (`RequiredText`, `OptionalText`, image upload helpers, etc.).

### `IStudyService`

- `GetCardsForModeAsync`
- `GetProgressByCardIdAsync`
- `GetSettingsAsync`
- `SaveSettingsAsync`
- `SaveFilterSettingsAsync`
- `MarkLearnedAsync`
- `CompleteSessionAsync`
- `GetStudyModeSelectorDataAsync`

### `IDictationService`

- `GetCardsForDictationAsync`
- `AnyCardHasExampleSentenceAsync`
- `CreateSessionAsync`
- `CheckAnswerAsync`
- `CompleteSessionAsync`
- `GetSessionResultAsync`

Related types in the same file (`DictationCheckResult`, `DictationWordComparison`, `DictationResult`, …) stay concrete classes and may appear as parameter/return types on the interface.

### `ICardActionService`

- `ExecuteAsync`
- `UndoAsync`
- `GetUndoableLogsAsync`
- `GetLogByIdAsync`

### `ICardActionCommandFactory`

- `Create(string actionType, int setId, string userId, IReadOnlyList<int> cardIds)` → `ICardActionCommand`

### `IAchievementService`

- `GetPageAsync`

(`AchievementPageModel` remains a concrete type, not an interface.)

### `IAchievementProgressService`

- `GetSnapshotAsync`

### `IAchievementUnlockService`

- `SyncEligibleAsync`

## Implementation pattern

```csharp
// Services/IStudyService.cs
namespace ltwnc.Services;

public interface IStudyService
{
    // Public methods copied from StudyService with identical signatures
}

// Services/StudyService.cs
public class StudyService : IStudyService
{
    // Existing body unchanged
}
```

Apply the same pattern to all eight pairs.

## Dependency injection

In `Program.cs`, replace concrete-only registration:

```csharp
// Before
builder.Services.AddScoped<StudyService>();

// After
builder.Services.AddScoped<IStudyService, StudyService>();
```

All eight pairs:

| Registration |
| --- |
| `AddScoped<IFlashcardSetService, FlashcardSetService>()` |
| `AddScoped<IStudyService, StudyService>()` |
| `AddScoped<IDictationService, DictationService>()` |
| `AddScoped<ICardActionService, CardActionService>()` |
| `AddScoped<ICardActionCommandFactory, CardActionCommandFactory>()` |
| `AddScoped<IAchievementService, AchievementService>()` |
| `AddScoped<IAchievementProgressService, AchievementProgressService>()` |
| `AddScoped<IAchievementUnlockService, AchievementUnlockService>()` |

Keep existing GoF registrations unchanged (`IStudyModeStrategy`, `IStudyEventPublisher`, observers, etc.).

Lifetime remains **Scoped** for all of the above.

Future decorator / swap (out of scope for implementation, enabled by this design):

```csharp
// Example only — not part of this work
// builder.Services.AddScoped<IStudyService, CachingStudyService>();
```

## Consumer updates

Inject interfaces instead of concrete application services.

| Consumer | Depends on |
| --- | --- |
| `FlashcardSetController` | `IFlashcardSetService` |
| `HomeController` | `IFlashcardSetService` |
| `StudyController` | `IStudyService`, `IDictationService`, `IFlashcardSetService` |
| `CardActionsController` | `ICardActionService`, `ICardActionCommandFactory`, `IFlashcardSetService` |
| `AchievementsController` | `IAchievementService` |
| `CardActionService` | `ICardActionCommandFactory` (plus existing `AppDbContext`) |
| `AchievementService` | `IAchievementUnlockService`, `IAchievementProgressService` |
| `AchievementUnlockService` | `IAchievementProgressService` |
| `AchievementStudyObserver` | `IAchievementUnlockService` |

Dependency shape after change:

```text
Controllers ──► I*Service / ICardActionCommandFactory
                    │
                    ▼
              concrete *Service / *Factory
                    │
         ┌──────────┼──────────┐
         ▼          ▼          ▼
    AppDbContext   IStudy*   IStudyEvent*
                   (existing GoF)
```

## Error handling

- No new exception wrapping or Result types.
- Services keep existing throw/catch behavior.
- Missing DI registration surfaces as resolve failure at request time; catch via build/test and smoke of affected controllers.

## Testing strategy

- **Required:** `dotnet build` and `dotnet test` pass.
- Unit tests that construct services with `new ConcreteService(...)` remain valid; constructors stay public.
- No requirement to introduce mocks or rewrite tests against interfaces.
- No new tests whose only purpose is “interface declares method X” (compile already enforces implementers).

## Rollout order

1. Add eight interface files (mirror public methods).
2. Add `: IXxx` on each concrete class.
3. Update consumer constructors/fields (controllers, services, observer).
4. Update `Program.cs` DI registrations.
5. Run build + full test suite.
6. Optional: short README note that application services are consumed via interfaces (alongside existing GoF docs).

Do not flip DI before consumers accept interfaces in a half-migrated state if that breaks intermediate compiles; within a single batch, complete steps 1–4 before relying on the app.

## Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Interface missing a public method | Extract by copying from class; compile fails if implementer is incomplete |
| Forgotten DI registration | Checklist of 8 pairs; request fails clearly if missed |
| Scope creep into ISP/decorators | Explicit non-goals |
| Accidental logic change | Diff should be type/wiring only |

## Success criteria

- [ ] Eight interfaces exist and match public API of their implementations
- [ ] Eight concrete classes implement their interfaces
- [ ] Controllers and internal application-service consumers inject interfaces only (for types in scope)
- [ ] `Program.cs` uses `AddScoped<I*, *>()` for all eight pairs
- [ ] Existing GoF DI unchanged
- [ ] `dotnet build` and `dotnet test` succeed
- [ ] No intentional behavior change in service methods

## Decisions log

| Decision | Choice |
| --- | --- |
| Primary goal | Extensibility (swap / decorator later) |
| Scope | Application services + `CardActionCommandFactory` |
| Surface | Full public method mirror (not ISP) |
| File layout | Separate interface file next to implementation |
| Consumer/DI depth | End-to-end (controllers + internal deps); tests optional |
| Rollout | Single mechanical batch |
| DI helper extension | Out of scope |
