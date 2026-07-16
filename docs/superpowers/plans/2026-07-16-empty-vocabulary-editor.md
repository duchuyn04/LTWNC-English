# Empty Vocabulary Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display polished, accessible empty states in both vocabulary editor columns when a flashcard set has no cards.

**Architecture:** Keep the existing server-rendered Razor flow and branch only on `cards.Any()`. Reuse the existing `#add-card-form` anchor as the call to action and add a small shared CSS component; no JavaScript, controller, service, or database changes are needed.

**Tech Stack:** ASP.NET Core Razor views, CSS, xUnit source-contract tests.

## Global Constraints

- Preserve the existing populated vocabulary list, batch actions, and detail panels unchanged.
- Use the existing `#add-card-form`; do not add another form or JavaScript behavior.
- Decorative icons must use `aria-hidden="true"`.
- At widths of `900px` or less, keep the existing single-column layout and avoid a tall empty list.
- Add no dependencies.

---

### Task 1: Empty-state markup and styling

**Files:**
- Modify: `tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs`
- Modify: `tests/ltwnc.Tests/Views/FlashcardSetEditStyleTests.cs`
- Modify: `Views/FlashcardSet/Edit.cshtml`
- Modify: `wwwroot/css/edit.css`

**Interfaces:**
- Consumes: existing `cards.Any()` view state and the existing element `#add-card-form`.
- Produces: `.vocab-editor.is-empty`, two `.vocab-empty-state` blocks, `.vocab-empty-state--list`, `.vocab-empty-state--detail`, and a native anchor targeting `#add-card-form`.

- [ ] **Step 1: Add failing markup tests**

Add these tests to `FlashcardSetEditMarkupTests`:

```csharp
[Fact]
public void Empty_editor_renders_list_and_detail_guidance_with_add_action()
{
    Assert.Contains("class=\"vocab-editor @(cards.Any() ? null : \"is-empty\")", Source);
    Assert.Contains("class=\"vocab-empty-state vocab-empty-state--list\"", Source);
    Assert.Contains("class=\"vocab-empty-state vocab-empty-state--detail\"", Source);
    Assert.Contains("Chưa có từ vựng", Source);
    Assert.Contains("Chưa có từ để chỉnh sửa", Source);
    Assert.Matches(
        new Regex("vocab-empty-state--detail[\\s\\S]*?<a[^>]+href=\"#add-card-form\"[^>]*>[\\s\\S]*?Thêm từ vựng", RegexOptions.Singleline),
        Source);
    Assert.Equal(2, Regex.Matches(Source, "class=\"ph [^\"]+\" aria-hidden=\"true\"").Count);
}

[Fact]
public void Populated_editor_remains_in_the_cards_any_branch()
{
    Assert.Matches(
        new Regex("@if \\(cards\\.Any\\(\\)\\)[\\s\\S]*?id=\"batch-form\"", RegexOptions.Singleline),
        Source);
    Assert.Matches(
        new Regex("@if \\(cards\\.Any\\(\\)\\)[\\s\\S]*?@foreach \\(var card in cards\\.OrderBy", RegexOptions.Singleline),
        Source);
}
```

- [ ] **Step 2: Add failing style tests**

Add these tests to `FlashcardSetEditStyleTests`:

```csharp
[Fact]
public void Empty_editor_states_are_centered_and_do_not_use_the_tall_list_height()
{
    Assert.Matches(
        Rule(
            "\\.vocab-empty-state",
            "display:\\s*grid[^}]*place-items:\\s*center[^}]*text-align:\\s*center"),
        Source);
    Assert.Matches(
        Rule(
            "\\.vocab-editor\\.is-empty\\s+\\.vocab-list",
            "height:\\s*auto[^}]*max-height:\\s*none"),
        Source);
    Assert.Matches(
        Rule(
            "\\.vocab-empty-state--detail",
            "min-height:\\s*260px"),
        Source);
}

[Fact]
public void Empty_editor_uses_compact_mobile_heights()
{
    Assert.Matches(
        new Regex(
            "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{[\\s\\S]*?" +
            RulePattern(
                "\\.vocab-empty-state--detail",
                "min-height:\\s*200px"),
            RegexOptions.Singleline),
        Source);
}
```

- [ ] **Step 3: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests\ltwnc.Tests\ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~FlashcardSetEditMarkupTests|FullyQualifiedName~FlashcardSetEditStyleTests" --verbosity minimal -p:UseAppHost=false
```

Expected: the new tests fail because the empty-state classes, copy, action, and styles do not exist.

- [ ] **Step 4: Implement the Razor empty states**

Change the editor wrapper in `Views/FlashcardSet/Edit.cshtml` to:

```cshtml
<div id="card-list" class="vocab-editor @(cards.Any() ? null : "is-empty") gsap-fade-up">
```

Replace the current `data-empty-card-list` paragraph with this branch, leaving the existing batch form in the `if` branch:

```cshtml
@if (!cards.Any())
{
    <div class="vocab-empty-state vocab-empty-state--list" data-empty-card-list>
        <i class="ph ph-cards" aria-hidden="true"></i>
        <div>
            <h6>Chưa có từ vựng</h6>
            <p>Thêm từ đầu tiên để bắt đầu xây dựng bộ thẻ.</p>
        </div>
    </div>
}
else
{
    <form asp-controller="CardActions"
          asp-action="BatchAction"
          asp-route-setId="@Model.Id"
          method="post"
          id="batch-form"
          data-undo-url-prefix="@Url.Content("~/CardActions/Undo/")">
        @Html.AntiForgeryToken()
        @foreach (var card in cards.OrderBy(c => c.OrderIndex))
        {
            <div class="vocab-list-item-wrapper">
                <input type="checkbox" name="selectedCardIds" value="@card.Id" class="card-checkbox" />
                <button type="button"
                        class="vocab-list-item @(card == cards.OrderBy(c => c.OrderIndex).First() ? "is-active" : "")"
                        data-card-id="@card.Id">
                    <span class="vocab-star">@(card.IsStarred ? "★" : "☆")</span>
                    <span class="vocab-list-copy">
                        <strong>@card.FrontText</strong>
                        <small>@card.PartOfSpeech · @card.Pronunciation</small>
                        <span>@card.BackText</span>
                    </span>
                </button>
            </div>
        }
    </form>
}
```

Inside `<section class="vocab-detail">`, place this branch before the existing populated-card loop and move that loop into the `else` branch without changing its contents:

```cshtml
@if (!cards.Any())
{
    <div class="vocab-empty-state vocab-empty-state--detail">
        <i class="ph ph-note-pencil" aria-hidden="true"></i>
        <div>
            <h5>Chưa có từ để chỉnh sửa</h5>
            <p>Thêm từ vựng đầu tiên, sau đó bạn có thể chỉnh sửa chi tiết tại đây.</p>
        </div>
        <a href="#add-card-form" class="btn-primary-custom">Thêm từ vựng</a>
    </div>
}
else
{
    @foreach (var card in cards.OrderBy(c => c.OrderIndex))
    {
        <div class="vocab-card-panel" data-card-panel="@card.Id">
            @* Retain the existing edit and delete forms byte-for-byte inside this panel. *@
        </div>
    }
}
```

The ellipsis-free transformation rule for the detail branch is: insert `@if (!cards.Any()) { ... } else {` immediately before the existing `@foreach`, and close the new `else` immediately after the existing `@foreach`; do not edit any line inside `.vocab-card-panel`.

- [ ] **Step 5: Implement the shared responsive styles**

Add this block after the `.vocab-detail` rule in `wwwroot/css/edit.css`:

```css
.vocab-empty-state {
    display: grid;
    place-items: center;
    align-content: center;
    gap: 1rem;
    padding: 2rem 1.25rem;
    color: #787774;
    text-align: center;
}

.vocab-empty-state > i {
    display: grid;
    place-items: center;
    width: 3rem;
    height: 3rem;
    border-radius: 50%;
    background: #F4FAFE;
    color: #58708A;
    font-size: 1.5rem;
}

.vocab-empty-state h5,
.vocab-empty-state h6,
.vocab-empty-state p {
    margin: 0;
}

.vocab-empty-state h5,
.vocab-empty-state h6 {
    color: #292524;
    font-weight: 700;
}

.vocab-empty-state p {
    margin-top: 0.35rem;
    max-width: 32rem;
}

.vocab-editor.is-empty .vocab-list {
    height: auto;
    max-height: none;
}

.vocab-empty-state--list {
    min-height: 180px;
}

.vocab-empty-state--detail {
    min-height: 260px;
}
```

Add this rule inside the existing `@media (max-width: 900px)` block:

```css
.vocab-empty-state--detail {
    min-height: 200px;
}
```

- [ ] **Step 6: Run the focused tests and verify GREEN**

Run the command from Step 3.

Expected: all `FlashcardSetEditMarkupTests` and `FlashcardSetEditStyleTests` pass.

- [ ] **Step 7: Run the complete test suite**

Run:

```powershell
dotnet test tests\ltwnc.Tests\ltwnc.Tests.csproj --no-restore --verbosity minimal -p:UseAppHost=false
```

Expected: all tests pass with zero failures.

- [ ] **Step 8: Verify the UI in both data states**

Start the application, open an empty set, and confirm both empty states render without clipping or excess height. Open a populated set and confirm its list, detail panel, batch buttons, and card navigation remain unchanged. At a viewport at or below `900px`, confirm the columns stack and the detail empty state uses the compact height.

- [ ] **Step 9: Commit the implementation**

```powershell
git add Views/FlashcardSet/Edit.cshtml wwwroot/css/edit.css tests/ltwnc.Tests/Views/FlashcardSetEditMarkupTests.cs tests/ltwnc.Tests/Views/FlashcardSetEditStyleTests.cs docs/superpowers/plans/2026-07-16-empty-vocabulary-editor.md
git commit -m "feat: add empty vocabulary editor state"
```
