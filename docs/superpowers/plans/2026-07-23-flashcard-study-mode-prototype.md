# Flashcard Study Mode Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build three Development-only, switchable prototypes for exposing Flashcard, Dictation, Quiz, and English Mission directly inside the existing Flashcard workspace.

**Architecture:** Keep the existing `/Study/{setId}/Flashcard` controller, model, learning controls, and production mode navigation unchanged. In Development, `Views/Study/Flashcard.cshtml` selects one of three throwaway Razor partials through `?variant=A|B|C`, mounts a shared prototype switcher, and loads prototype-only CSS and JavaScript.

**Tech Stack:** ASP.NET Core MVC, Razor views, existing Phosphor icons and CSS tokens, vanilla JavaScript.

## Global Constraints

- Prototype question: how should all four implemented study modes be exposed directly on the Flashcard screen?
- The four modes are Flashcard, Nghe chép, Kiểm tra, and English Mission.
- Use the existing Flashcard route and real `FlashcardStudyViewModel` data.
- Render prototype UI only when `IWebHostEnvironment.IsDevelopment()` is true.
- Preserve the current Flashcard content, progress, settings, filters, vocabulary list, and keyboard shortcuts.
- Do not add persistence, backend mutations, packages, controller actions, database work, or automated tests.
- Prototype controls may navigate between existing mode routes but may not submit forms or update study progress.
- The prototype switcher changes variants only while keyboard focus is inside the switcher; global arrow keys continue to change Flashcards.
- Keep every variant free of horizontal overflow at 1440×900, 1024×768, 390×844, and 640×384.
- Keep the prototype throwaway and clearly named under `Views/Study/Prototypes` and `*-prototype.*` assets.

---

## File map

- Modify `Views/Study/Flashcard.cshtml`: detect Development, normalize `?variant=`, mount one prototype partial, retain the current production navigation as the non-Development fallback, and load prototype assets.
- Create `Views/Study/Prototypes/_StudyModeVariantA.cshtml`: horizontal four-mode navigation.
- Create `Views/Study/Prototypes/_StudyModeVariantB.cshtml`: desktop vertical mode rail with a responsive horizontal fallback.
- Create `Views/Study/Prototypes/_StudyModeVariantC.cshtml`: compact launcher and mode popover.
- Create `Views/Study/Prototypes/_StudyModePrototypeSwitcher.cshtml`: shared A/B/C evaluation bar.
- Create `wwwroot/css/flashcard-study-mode-prototype.css`: styles isolated under `.study-mode-prototype`.
- Create `wwwroot/js/flashcard-study-mode-prototype.js`: variant URL cycling, switcher keyboard behavior, and Variant C popover behavior.

---

### Task 1: Mount a Development-only prototype host

**Files:**
- Modify: `Views/Study/Flashcard.cshtml:1-12`
- Modify: `Views/Study/Flashcard.cshtml:50-104`
- Modify: `Views/Study/Flashcard.cshtml:306-307`

**Interfaces:**
- Consumes: `IWebHostEnvironment.IsDevelopment()`, `Context.Request.Query["variant"]`, and `FlashcardStudyViewModel`.
- Produces: `prototypeVariant` constrained to `A`, `B`, or `C`; a `study-mode-prototype` host; the current production mode tabs as the non-Development fallback.

- [ ] **Step 1: Add Development and variant selection at the top of the view**

Add the environment injection and normalize untrusted query input:

```cshtml
@model FlashcardStudyViewModel
@inject IWebHostEnvironment HostEnvironment
@{
    ViewData["Title"] = "Học flashcard";
    ViewData["HideLayoutChrome"] = true;

    var dictationMode = Model.Modes.FirstOrDefault(mode => mode.Mode == StudyMode.Dictation);
    var missionMode = Model.Modes.FirstOrDefault(mode => mode.Mode == StudyMode.EnglishMission);

    var isStudyModePrototype = HostEnvironment.IsDevelopment();
    var requestedVariant = Context.Request.Query["variant"].ToString().ToUpperInvariant();
    var prototypeVariant = requestedVariant is "A" or "B" or "C"
        ? requestedVariant
        : "A";
    var prototypePartial = $"Prototypes/_StudyModeVariant{prototypeVariant}";
}

<link rel="stylesheet" href="~/css/flashcard.css" asp-append-version="true" />
@if (isStudyModePrototype)
{
    <link rel="stylesheet"
          href="~/css/flashcard-study-mode-prototype.css"
          asp-append-version="true" />
}
```

- [ ] **Step 2: Mount prototype variants while keeping the production fallback**

Replace the existing `.study-mode-row` block with:

```cshtml
@if (isStudyModePrototype)
{
    <div class="study-mode-prototype"
         data-study-mode-prototype
         data-variant="@prototypeVariant">
        @await Html.PartialAsync(prototypePartial, Model)
        @await Html.PartialAsync(
            "Prototypes/_StudyModePrototypeSwitcher",
            prototypeVariant)
    </div>
}
else
{
    <div class="study-mode-row">
        <nav class="study-mode-tabs" aria-label="Chế độ học">
            <a class="study-mode-tab is-active"
               href="/Study/@Model.SetId/Flashcard"
               aria-current="page">
                <i class="ph ph-cards" aria-hidden="true"></i>
                <span>Flashcard</span>
            </a>

            @if (dictationMode?.IsAvailable == true)
            {
                <a class="study-mode-tab" href="@dictationMode.ActionUrl">
                    <i class="ph ph-headphones" aria-hidden="true"></i>
                    <span>Nghe chép</span>
                </a>
            }
            else
            {
                <span class="study-mode-tab is-disabled"
                      aria-disabled="true"
                      title="@(dictationMode?.UnavailableReason ?? "Chế độ chưa khả dụng")">
                    <i class="ph ph-headphones" aria-hidden="true"></i>
                    <span>Nghe chép</span>
                </span>
            }

            @if (missionMode?.IsAvailable == true)
            {
                <a class="study-mode-tab" href="@missionMode.ActionUrl">
                    <i class="ph ph-chats-circle" aria-hidden="true"></i>
                    <span>English Mission</span>
                </a>
            }
            else
            {
                <span class="study-mode-tab is-disabled"
                      aria-disabled="true"
                      title="@(missionMode?.UnavailableReason ?? "Chế độ chưa khả dụng")">
                    <i class="ph ph-chats-circle" aria-hidden="true"></i>
                    <span>English Mission</span>
                </span>
            }
        </nav>
    </div>
}
```

- [ ] **Step 3: Load prototype JavaScript only in Development**

At the start of `@section Scripts`, before the existing inline Flashcard script, add:

```cshtml
@section Scripts {
    @if (isStudyModePrototype)
    {
        <script src="~/js/flashcard-study-mode-prototype.js"
                asp-append-version="true"></script>
    }

    <script>
```

Keep the remainder of the existing Flashcard script unchanged.

- [ ] **Step 4: Build to validate Razor compilation**

Run:

```powershell
dotnet build --no-restore
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 5: Commit the prototype host**

```powershell
git add Views/Study/Flashcard.cshtml
git commit -m "chore: mount flashcard study mode prototype"
```

---

### Task 2: Add three structurally different study-mode variants

**Files:**
- Create: `Views/Study/Prototypes/_StudyModeVariantA.cshtml`
- Create: `Views/Study/Prototypes/_StudyModeVariantB.cshtml`
- Create: `Views/Study/Prototypes/_StudyModeVariantC.cshtml`
- Create: `wwwroot/css/flashcard-study-mode-prototype.css`

**Interfaces:**
- Consumes: `FlashcardStudyViewModel.Modes`, `StudyMode`, `StudyModeOptionViewModel.ActionUrl`, `IsAvailable`, and `UnavailableReason`.
- Produces: three read-only navigation structures under `.prototype-variant-a`, `.prototype-variant-b`, and `.prototype-variant-c`.

- [ ] **Step 1: Create Variant A — horizontal mode bar**

Create `Views/Study/Prototypes/_StudyModeVariantA.cshtml`:

```cshtml
@model FlashcardStudyViewModel
@{
    var modeOrder = new[]
    {
        StudyMode.Flashcard,
        StudyMode.Dictation,
        StudyMode.Quiz,
        StudyMode.EnglishMission
    };
}

<section class="prototype-variant prototype-variant-a"
         aria-label="Prototype A: thanh chế độ ngang">
    <div class="prototype-section-label">A · Thanh chế độ ngang</div>
    <nav class="prototype-mode-bar" aria-label="Chế độ học">
        @foreach (var modeKey in modeOrder)
        {
            var mode = Model.Modes.FirstOrDefault(item => item.Mode == modeKey);
            if (mode is null)
            {
                continue;
            }

            var isActive = mode.Mode == StudyMode.Flashcard;
            var itemClass = $"prototype-mode-chip{(isActive ? " is-active" : "")}{(!mode.IsAvailable ? " is-disabled" : "")}";

            if (isActive)
            {
                <span class="@itemClass" aria-current="page">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>@mode.Name</span>
                </span>
            }
            else if (mode.IsAvailable)
            {
                <a class="@itemClass" href="@mode.ActionUrl">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>@mode.Name</span>
                </a>
            }
            else
            {
                <span class="@itemClass"
                      aria-disabled="true"
                      title="@mode.UnavailableReason">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>@mode.Name</span>
                </span>
            }
        }
    </nav>
</section>
```

- [ ] **Step 2: Create Variant B — left mode rail**

Create `Views/Study/Prototypes/_StudyModeVariantB.cshtml`:

```cshtml
@model FlashcardStudyViewModel
@{
    var modeOrder = new[]
    {
        StudyMode.Flashcard,
        StudyMode.Dictation,
        StudyMode.Quiz,
        StudyMode.EnglishMission
    };
}

<aside class="prototype-variant prototype-variant-b"
       aria-label="Prototype B: thanh chế độ dọc">
    <div class="prototype-section-label">B · Mode rail</div>
    <nav class="prototype-mode-rail" aria-label="Chế độ học">
        @foreach (var modeKey in modeOrder)
        {
            var mode = Model.Modes.FirstOrDefault(item => item.Mode == modeKey);
            if (mode is null)
            {
                continue;
            }

            var isActive = mode.Mode == StudyMode.Flashcard;
            var itemClass = $"prototype-rail-item{(isActive ? " is-active" : "")}{(!mode.IsAvailable ? " is-disabled" : "")}";

            if (isActive)
            {
                <span class="@itemClass" aria-current="page">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>
                        <strong>@mode.Name</strong>
                        <small>Đang học</small>
                    </span>
                </span>
            }
            else if (mode.IsAvailable)
            {
                <a class="@itemClass" href="@mode.ActionUrl">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>
                        <strong>@mode.Name</strong>
                        <small>@mode.CardCount thẻ</small>
                    </span>
                </a>
            }
            else
            {
                <span class="@itemClass"
                      aria-disabled="true"
                      title="@mode.UnavailableReason">
                    <i class="ph @mode.IconClass" aria-hidden="true"></i>
                    <span>
                        <strong>@mode.Name</strong>
                        <small>@(mode.UnavailableReason ?? "Chưa khả dụng")</small>
                    </span>
                </span>
            }
        }
    </nav>
</aside>
```

- [ ] **Step 3: Create Variant C — compact launcher**

Create `Views/Study/Prototypes/_StudyModeVariantC.cshtml`:

```cshtml
@model FlashcardStudyViewModel
@{
    var modeOrder = new[]
    {
        StudyMode.Flashcard,
        StudyMode.Dictation,
        StudyMode.Quiz,
        StudyMode.EnglishMission
    };
}

<section class="prototype-variant prototype-variant-c"
         aria-label="Prototype C: bộ chọn chế độ gọn">
    <div class="prototype-launcher-copy">
        <span class="prototype-section-label">C · Chế độ gọn</span>
        <strong>Flashcard</strong>
        <small>Đổi cách học khi bạn cần.</small>
    </div>

    <div class="prototype-launcher">
        <button type="button"
                class="prototype-launcher-button"
                data-mode-launcher
                aria-expanded="false"
                aria-controls="prototype-mode-menu">
            <i class="ph ph-squares-four" aria-hidden="true"></i>
            <span>Chế độ học</span>
            <i class="ph ph-caret-down" aria-hidden="true"></i>
        </button>

        <div id="prototype-mode-menu"
             class="prototype-mode-menu"
             data-mode-menu
             hidden>
            @foreach (var modeKey in modeOrder)
            {
                var mode = Model.Modes.FirstOrDefault(item => item.Mode == modeKey);
                if (mode is null)
                {
                    continue;
                }

                var isActive = mode.Mode == StudyMode.Flashcard;
                var itemClass = $"prototype-menu-item{(isActive ? " is-active" : "")}{(!mode.IsAvailable ? " is-disabled" : "")}";

                if (isActive)
                {
                    <span class="@itemClass" aria-current="page">
                        <i class="ph @mode.IconClass" aria-hidden="true"></i>
                        <span>
                            <strong>@mode.Name</strong>
                            <small>Đang học</small>
                        </span>
                        <i class="ph ph-check" aria-hidden="true"></i>
                    </span>
                }
                else if (mode.IsAvailable)
                {
                    <a class="@itemClass" href="@mode.ActionUrl">
                        <i class="ph @mode.IconClass" aria-hidden="true"></i>
                        <span>
                            <strong>@mode.Name</strong>
                            <small>@mode.Description</small>
                        </span>
                        <i class="ph ph-arrow-up-right" aria-hidden="true"></i>
                    </a>
                }
                else
                {
                    <span class="@itemClass"
                          aria-disabled="true"
                          title="@mode.UnavailableReason">
                        <i class="ph @mode.IconClass" aria-hidden="true"></i>
                        <span>
                            <strong>@mode.Name</strong>
                            <small>@(mode.UnavailableReason ?? "Chưa khả dụng")</small>
                        </span>
                        <i class="ph ph-lock" aria-hidden="true"></i>
                    </span>
                }
            }
        </div>
    </div>
</section>
```

- [ ] **Step 4: Add isolated prototype styling**

Create `wwwroot/css/flashcard-study-mode-prototype.css` with these exact layout rules, then refine only spacing and breakpoints during browser verification:

```css
.study-mode-prototype {
    --prototype-ink: var(--fc-ink);
    --prototype-paper: var(--fc-paper);
    --prototype-line: var(--fc-line);
    --prototype-muted: var(--fc-muted);
    position: relative;
    z-index: 20;
}

.prototype-variant {
    color: var(--prototype-ink);
}

.prototype-section-label {
    color: var(--fc-accent);
    font-size: 0.68rem;
    font-weight: 800;
    letter-spacing: 0.12em;
    text-transform: uppercase;
}

.prototype-variant-a {
    display: grid;
    gap: 0.65rem;
}

.prototype-mode-bar {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 0.55rem;
    padding: 0.45rem;
    border: 1px solid var(--prototype-line);
    border-radius: 18px;
    background: color-mix(in srgb, var(--prototype-paper) 90%, transparent);
}

.prototype-mode-chip {
    display: inline-flex;
    min-height: 48px;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    padding: 0.7rem;
    border-radius: 13px;
    color: var(--prototype-muted);
    font-size: 0.82rem;
    font-weight: 800;
    text-decoration: none;
}

.prototype-mode-chip:hover:not(.is-disabled),
.prototype-mode-chip.is-active {
    background: var(--prototype-ink);
    color: var(--prototype-paper);
}

.prototype-mode-chip.is-disabled,
.prototype-rail-item.is-disabled,
.prototype-menu-item.is-disabled {
    cursor: not-allowed;
    opacity: 0.48;
}

.prototype-variant-b {
    position: fixed;
    top: clamp(11rem, 25vh, 16rem);
    left: max(1rem, calc(50vw - 590px));
    display: grid;
    width: 172px;
    gap: 0.65rem;
}

.prototype-mode-rail {
    display: grid;
    gap: 0.4rem;
    padding: 0.55rem;
    border: 1px solid var(--prototype-line);
    border-radius: 20px;
    background: var(--prototype-paper);
    box-shadow: var(--shadow-card);
}

.prototype-rail-item {
    display: grid;
    grid-template-columns: 28px minmax(0, 1fr);
    align-items: center;
    gap: 0.6rem;
    min-height: 58px;
    padding: 0.6rem;
    border-radius: 14px;
    color: var(--prototype-muted);
    text-decoration: none;
}

.prototype-rail-item i {
    font-size: 1.15rem;
}

.prototype-rail-item span {
    display: grid;
    min-width: 0;
}

.prototype-rail-item strong {
    color: inherit;
    font-size: 0.8rem;
}

.prototype-rail-item small {
    overflow: hidden;
    color: var(--prototype-muted);
    font-size: 0.66rem;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.prototype-rail-item:hover:not(.is-disabled),
.prototype-rail-item.is-active {
    background: var(--prototype-ink);
    color: var(--prototype-paper);
}

.prototype-rail-item.is-active small {
    color: color-mix(in srgb, var(--prototype-paper) 68%, transparent);
}

.prototype-variant-c {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    padding: 0.75rem 0;
}

.prototype-launcher-copy {
    display: grid;
    gap: 0.15rem;
}

.prototype-launcher-copy strong {
    font-size: 0.95rem;
}

.prototype-launcher-copy small {
    color: var(--prototype-muted);
    font-size: 0.75rem;
}

.prototype-launcher {
    position: relative;
}

.prototype-launcher-button {
    display: inline-flex;
    min-height: 46px;
    align-items: center;
    gap: 0.5rem;
    padding: 0.7rem 0.9rem;
    border: 1px solid var(--prototype-line);
    border-radius: 14px;
    background: var(--prototype-paper);
    color: var(--prototype-ink);
    font-weight: 800;
}

.prototype-mode-menu {
    position: absolute;
    top: calc(100% + 0.55rem);
    right: 0;
    z-index: 50;
    display: grid;
    width: min(360px, calc(100vw - 2rem));
    gap: 0.35rem;
    padding: 0.55rem;
    border: 1px solid var(--prototype-line);
    border-radius: 20px;
    background: var(--prototype-paper);
    box-shadow: var(--shadow-lift);
}

.prototype-mode-menu[hidden] {
    display: none;
}

.prototype-menu-item {
    display: grid;
    grid-template-columns: 32px minmax(0, 1fr) 20px;
    align-items: center;
    gap: 0.65rem;
    min-height: 64px;
    padding: 0.7rem;
    border-radius: 14px;
    color: var(--prototype-ink);
    text-decoration: none;
}

.prototype-menu-item:hover:not(.is-disabled),
.prototype-menu-item.is-active {
    background: var(--fc-soft);
}

.prototype-menu-item > span {
    display: grid;
}

.prototype-menu-item small {
    color: var(--prototype-muted);
    font-size: 0.7rem;
}

@media (max-width: 1120px) {
    .prototype-variant-b {
        position: static;
        width: auto;
    }

    .prototype-mode-rail {
        grid-template-columns: repeat(4, minmax(0, 1fr));
    }
}

@media (max-width: 767px) {
    .prototype-mode-bar,
    .prototype-mode-rail {
        grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .prototype-variant-c {
        align-items: stretch;
        flex-direction: column;
    }

    .prototype-launcher-button {
        width: 100%;
        justify-content: center;
    }

    .prototype-mode-menu {
        right: auto;
        left: 0;
    }
}
```

- [ ] **Step 5: Build to verify all partials compile**

Run:

```powershell
dotnet build --no-restore
```

Expected: build succeeds with `0 Error(s)` and each partial resolves.

- [ ] **Step 6: Commit the three variants**

```powershell
git add Views/Study/Prototypes wwwroot/css/flashcard-study-mode-prototype.css
git commit -m "feat: add flashcard study mode prototype variants"
```

---

### Task 3: Add the shared variant switcher and interaction script

**Files:**
- Create: `Views/Study/Prototypes/_StudyModePrototypeSwitcher.cshtml`
- Create: `wwwroot/js/flashcard-study-mode-prototype.js`
- Modify: `wwwroot/css/flashcard-study-mode-prototype.css`

**Interfaces:**
- Consumes: the current variant string, `[data-study-mode-prototype]`, `[data-prototype-switcher]`, `[data-mode-launcher]`, and `[data-mode-menu]`.
- Produces: reload-stable `?variant=`, cyclic A/B/C navigation, focus-scoped arrow-key cycling, and an accessible Variant C popover.

- [ ] **Step 1: Create the shared switcher partial**

Create `Views/Study/Prototypes/_StudyModePrototypeSwitcher.cshtml`:

```cshtml
@model string
@{
    var names = new Dictionary<string, string>
    {
        ["A"] = "Thanh ngang",
        ["B"] = "Mode rail",
        ["C"] = "Chế độ gọn"
    };
}

<nav class="prototype-switcher"
     data-prototype-switcher
     aria-label="Chuyển phương án prototype">
    <button type="button"
            data-prototype-direction="-1"
            aria-label="Phương án trước">
        <i class="ph ph-arrow-left" aria-hidden="true"></i>
    </button>
    <span class="prototype-switcher-label">
        <small>PROTOTYPE</small>
        <strong>@Model · @names[Model]</strong>
    </span>
    <button type="button"
            data-prototype-direction="1"
            aria-label="Phương án tiếp theo">
        <i class="ph ph-arrow-right" aria-hidden="true"></i>
    </button>
</nav>
```

- [ ] **Step 2: Create the prototype JavaScript**

Create `wwwroot/js/flashcard-study-mode-prototype.js`:

```javascript
(() => {
    const variants = ["A", "B", "C"];
    const host = document.querySelector("[data-study-mode-prototype]");
    const switcher = document.querySelector("[data-prototype-switcher]");

    if (!host || !switcher) {
        return;
    }

    const current = variants.includes(host.dataset.variant)
        ? host.dataset.variant
        : "A";

    const navigate = (offset) => {
        const currentIndex = variants.indexOf(current);
        const nextIndex = (currentIndex + offset + variants.length) % variants.length;
        const url = new URL(window.location.href);
        url.searchParams.set("variant", variants[nextIndex]);
        window.location.assign(url.toString());
    };

    switcher.querySelectorAll("[data-prototype-direction]").forEach((button) => {
        button.addEventListener("click", () => {
            navigate(Number(button.dataset.prototypeDirection));
        });
    });

    switcher.addEventListener("keydown", (event) => {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        navigate(event.key === "ArrowLeft" ? -1 : 1);
    });

    const launcher = document.querySelector("[data-mode-launcher]");
    const menu = document.querySelector("[data-mode-menu]");

    if (!launcher || !menu) {
        return;
    }

    const setMenuOpen = (open) => {
        launcher.setAttribute("aria-expanded", String(open));
        menu.hidden = !open;
    };

    launcher.addEventListener("click", () => {
        setMenuOpen(launcher.getAttribute("aria-expanded") !== "true");
    });

    document.addEventListener("click", (event) => {
        if (!launcher.contains(event.target) && !menu.contains(event.target)) {
            setMenuOpen(false);
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !menu.hidden) {
            setMenuOpen(false);
            launcher.focus();
        }
    });
})();
```

- [ ] **Step 3: Style the switcher without covering Flashcard controls**

Append to `wwwroot/css/flashcard-study-mode-prototype.css`:

```css
.prototype-switcher {
    position: fixed;
    right: 50%;
    bottom: max(1rem, env(safe-area-inset-bottom));
    z-index: 1000;
    display: grid;
    grid-template-columns: 42px minmax(150px, auto) 42px;
    align-items: center;
    gap: 0.35rem;
    padding: 0.4rem;
    border: 1px solid color-mix(in srgb, white 20%, transparent);
    border-radius: 18px;
    background: #172117;
    box-shadow: 0 16px 48px rgba(11, 20, 12, 0.3);
    color: white;
    transform: translateX(50%);
}

.prototype-switcher button {
    display: inline-grid;
    width: 42px;
    height: 42px;
    place-items: center;
    border: 0;
    border-radius: 12px;
    background: rgba(255, 255, 255, 0.1);
    color: white;
}

.prototype-switcher button:hover,
.prototype-switcher button:focus-visible {
    background: #f3c969;
    color: #172117;
    outline: 2px solid white;
    outline-offset: 2px;
}

.prototype-switcher-label {
    display: grid;
    justify-items: center;
    line-height: 1.15;
}

.prototype-switcher-label small {
    color: #f3c969;
    font-size: 0.58rem;
    font-weight: 900;
    letter-spacing: 0.12em;
}

.prototype-switcher-label strong {
    font-size: 0.78rem;
}

@media (max-width: 420px) {
    .prototype-switcher {
        grid-template-columns: 40px minmax(118px, auto) 40px;
        max-width: calc(100vw - 1rem);
    }
}
```

- [ ] **Step 4: Build and inspect static asset references**

Run:

```powershell
dotnet build --no-restore
rg -n "flashcard-study-mode-prototype|StudyModeVariant|prototype-switcher" Views/Study wwwroot
```

Expected: build succeeds; references point only to the explicit prototype view and assets.

- [ ] **Step 5: Commit switcher behavior**

```powershell
git add Views/Study/Prototypes/_StudyModePrototypeSwitcher.cshtml `
        wwwroot/js/flashcard-study-mode-prototype.js `
        wwwroot/css/flashcard-study-mode-prototype.css
git commit -m "feat: add flashcard prototype switcher"
```

---

### Task 4: Run and visually verify the prototype

**Files:**
- Modify only if browser verification reveals a concrete layout defect:
  - `Views/Study/Prototypes/_StudyModeVariantA.cshtml`
  - `Views/Study/Prototypes/_StudyModeVariantB.cshtml`
  - `Views/Study/Prototypes/_StudyModeVariantC.cshtml`
  - `wwwroot/css/flashcard-study-mode-prototype.css`
  - `wwwroot/js/flashcard-study-mode-prototype.js`

**Interfaces:**
- Consumes: a local authenticated or seed-data Flashcard route.
- Produces: a runnable prototype URL and verified A/B/C layouts ready for user selection.

- [ ] **Step 1: Start the app with one command**

Run:

```powershell
dotnet run --no-build --urls http://localhost:5010
```

Expected: the app listens on `http://localhost:5010`.

- [ ] **Step 2: Open a real Set directly in Flashcard mode**

Open:

```text
http://localhost:5010/Study/{setId}/Flashcard?variant=A
```

Use an owned Set that contains enough cards for every available mode. If authentication redirects to Login, sign in with local development credentials before continuing.

- [ ] **Step 3: Verify each variant at desktop and mobile sizes**

Check A, B, and C at:

```text
1440×900
1024×768
390×844
640×384
```

For every variant verify:

- all four modes are visible or reachable;
- Flashcard is marked active;
- unavailable modes show their reason and cannot navigate;
- no horizontal overflow exists;
- the flashcard remains the primary focus;
- the bottom switcher does not cover the card controls;
- refreshing preserves `?variant=`;
- clicking the switcher arrows wraps A → B → C → A;
- with focus on a switcher button, `ArrowLeft` and `ArrowRight` change variants;
- with focus outside the switcher, `ArrowLeft` and `ArrowRight` still change Flashcards;
- Variant C opens, closes on outside click, closes on Escape, and returns focus to its launcher.

- [ ] **Step 4: Verify production gating**

Run with Production environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --no-build --urls http://localhost:5011
```

Open `/Study/{setId}/Flashcard?variant=C`.

Expected:

- the current production mode tabs render;
- no prototype switcher renders;
- no prototype CSS or JavaScript tag appears in the page source.

- [ ] **Step 5: Run final compile and repository checks**

Run:

```powershell
dotnet build --no-restore
git diff --check
git status --short
```

Expected:

- build succeeds with `0 Error(s)`;
- `git diff --check` prints nothing;
- only intentional prototype files are modified or untracked.

- [ ] **Step 6: Commit any visual-verification fixes**

If browser verification required adjustments:

```powershell
git add Views/Study/Prototypes `
        Views/Study/Flashcard.cshtml `
        wwwroot/css/flashcard-study-mode-prototype.css `
        wwwroot/js/flashcard-study-mode-prototype.js
git commit -m "fix: refine flashcard study mode prototypes"
```

If no adjustment was required, do not create an empty commit.

## Handoff

Provide:

- the local prototype URL with a real Set id;
- direct links for `?variant=A`, `?variant=B`, and `?variant=C`;
- a concise reminder that the code is throwaway and Development-only;
- a request for the winning variant or a combination of specific elements.
