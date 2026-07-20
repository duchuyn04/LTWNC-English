# Modern Quiz Setup and Untimed Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Nâng cấp màn hình thiết lập trắc nghiệm theo bố cục split-card hiện đại và hỗ trợ lựa chọn làm bài không giới hạn thời gian xuyên suốt start, continue, restart và retry.

**Architecture:** Dùng QuizTimingMode trong request view model để server nhận một lựa chọn duy nhất (preset, custom hoặc untimed). QuizService.StartNewAsync nhận int?; giá trị null tạo phiên untimed bằng hai trường thời gian nullable đang có, còn retry/restart sao chép trực tiếp chế độ thời gian từ phiên nguồn. View setup được tách thành vùng form chính, thẻ active-session và một script nhỏ đồng bộ radio/card state.

**Tech Stack:** ASP.NET Core MVC, Razor views, Entity Framework Core 10, C#/xUnit/Moq, CSS thuần và JavaScript trình duyệt.

## Global Constraints

- Không thêm migration hoặc cột cơ sở dữ liệu mới.
- Preset hợp lệ là 5, 10, 15 và 20 phút; custom hợp lệ từ 1 đến 120 phút.
- Hai trường QuizStartedAtUtc = NULL và QuizTimeLimitSeconds = NULL biểu diễn phiên không giới hạn, kể cả phiên cũ đang có hai trường này là NULL.
- Các lựa chọn preset, custom và untimed phải loại trừ lẫn nhau; server là nguồn validation cuối cùng.
- Giữ nguyên thay đổi hover độc lập trong wwwroot/css/study-mode-selector.css và tests/ltwnc.Tests/Views/StudyModeSelectorStyleTests.cs.
- Mọi task đều phải chạy test liên quan trước khi commit; không dùng giá trị 0 làm sentinel cho untimed.

---

## File map

- Modify Models/ViewModels/Study/QuizSetupViewModel.cs: thêm enum và hợp đồng request cho chế độ thời gian.
- Modify Services/Study/IQuizService.cs: đổi chữ ký StartNewAsync sang int?.
- Modify Services/Study/QuizService.cs: tạo phiên timed/untimed và kế thừa chế độ thời gian khi retry/restart.
- Modify Controllers/StudyController.cs: bind, validate và chuyển QuizTimingMode thành số phút nullable.
- Modify Views/Study/QuizSetup.cshtml: markup split-card, preset cards, custom card, untimed card và active-session card.
- Modify Views/Study/Quiz.cshtml: chỉ render đồng hồ khi phiên có deadline.
- Create wwwroot/js/quiz-setup.js: đồng bộ lựa chọn card, custom input, nhãn CTA và trạng thái disabled.
- Modify wwwroot/css/quiz.css: token, layout, card state, focus, responsive và reduced-motion cho setup mới; không phá style đang làm bài/kết quả.
- Modify tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs: validation mode, custom/untimed dispatch và GET model defaults.
- Modify tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs: nullable timing, deadline và kế thừa retry/restart.
- Modify tests/ltwnc.Tests/Views/QuizViewTests.cs: static contract của Razor, CSS và setup script.
- Modify tests/ltwnc.Tests/Integration/QuizSetupRenderingTests.cs: kiểm tra HTML form route và token sau khi markup đổi.

## Task 1: Chốt hợp đồng chế độ thời gian và service semantics

**Files:**
- Modify: Models/ViewModels/Study/QuizSetupViewModel.cs
- Modify: Services/Study/IQuizService.cs
- Modify: Services/Study/QuizService.cs
- Test: tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs

**Interfaces:**
- Produces QuizTimingMode with values Preset, Custom, Untimed.
- Produces IQuizService.StartNewAsync(int setId, string userId, UserStudySettings settings, int? timeLimitMinutes).
- Existing callers passing an int remain source-compatible because C# widens to nullable int?.

- [ ] **Step 1: Viết test fail cho phiên untimed và kế thừa null timing**

Thêm các test vào QuizServiceTests cạnh test StartNewAsync hiện có:

~~~csharp
[Fact]
public async Task StartNewAsync_with_null_duration_creates_untimed_session()
{
    await using var database = await QuizTestDatabase.CreateAsync();
    FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
    QuizService service = CreateService(
        database.Context,
        new RecordingStudyEventPublisher());

    StudySession session = await service.StartNewAsync(
        set.Id,
        set.UserId,
        new UserStudySettings(),
        null);

    Assert.Null(session.QuizStartedAtUtc);
    Assert.Null(session.QuizTimeLimitSeconds);

    QuizQuestionState state = await service.GetCurrentQuestionAsync(
        set.Id,
        session.Id,
        set.UserId);
    Assert.Null(state.DeadlineUtc);
    Assert.Null(state.RemainingSeconds);
}

[Fact]
public async Task RetryAll_from_untimed_source_creates_untimed_session()
{
    await using var database = await QuizTestDatabase.CreateAsync();
    FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
    QuizService service = CreateService(
        database.Context,
        new RecordingStudyEventPublisher());
    StudySession source = await service.StartNewAsync(
        set.Id,
        set.UserId,
        new UserStudySettings(),
        null);
    await CompleteQuizAsync(database.Context, service, set, source);

    StudySession retry = await service.RetryAllAsync(
        set.Id,
        source.Id,
        set.UserId);

    Assert.Null(retry.QuizStartedAtUtc);
    Assert.Null(retry.QuizTimeLimitSeconds);
}
~~~

Reuse QuizTestDatabase, SeedQuestionPoolAsync, CreateService and CompleteQuizAsync already defined in this test class; do not add a second database fixture.

Also update the existing Restart_abandons_active_attempt_reuses_scope_and_starts_fresh_timer theory so its second InlineData expects null, its expectedTimeLimitSeconds parameter is int?, and it asserts QuizStartedAtUtc is null when the expected duration is null. Add Assert.Null(retrySession.QuizStartedAtUtc) and Assert.Null(retrySession.QuizTimeLimitSeconds) to the existing RetryWrong_contains_only_wrong_cards_and_preserves_directions test, and the same two assertions to RetryAll_preserves_original_card_scope_and_redistributes_directions. These existing tests use StartOrResumeAsync sources with null timing, so together they cover all three replacement actions.

- [ ] **Step 2: Chạy test để xác nhận đang fail**

Run:

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~StartNewAsync_with_null_duration|FullyQualifiedName~RetryAll_from_untimed_source"
~~~

Expected: compile/test failure because StartNewAsync only accepts int and retry currently substitutes DefaultQuizMinutes for a null source duration.

- [ ] **Step 3: Cập nhật hợp đồng và implementation tối thiểu**

Trong QuizSetupViewModel.cs, thêm enum:

~~~csharp
public enum QuizTimingMode
{
    Preset,
    Custom,
    Untimed
}
~~~

Thêm property:

~~~csharp
public QuizTimingMode TimingMode { get; set; } = QuizTimingMode.Preset;
~~~

Đổi tham số interface/service thành int? timeLimitMinutes. Trong QuizService.StartNewAsync, chỉ validate giới hạn khi timeLimitMinutes.HasValue và tạo entity bằng:

~~~csharp
QuizStartedAtUtc = timeLimitMinutes.HasValue ? now : null,
QuizTimeLimitSeconds = timeLimitMinutes.HasValue
    ? timeLimitMinutes.Value * 60
    : null
~~~

Trong flow tạo replacement session, thay fallback sourceSession.QuizTimeLimitSeconds ?? DefaultQuizMinutes * 60 bằng:

~~~csharp
QuizStartedAtUtc = sourceSession.QuizTimeLimitSeconds.HasValue ? now : null,
QuizTimeLimitSeconds = sourceSession.QuizTimeLimitSeconds,
~~~

Trong MatchesRequestedRetryAsync, so sánh activeSession.QuizTimeLimitSeconds != sourceSession.QuizTimeLimitSeconds để active retry của timed và untimed không bị trộn.

- [ ] **Step 4: Chạy lại test service**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests"
~~~

Expected: PASS cho test mới và toàn bộ test service hiện có; các test cũ kiểm tra timed session vẫn phải nhận QuizStartedAtUtc và số giây như trước.

- [ ] **Step 5: Commit task**

~~~powershell
git add Models/ViewModels/Study/QuizSetupViewModel.cs Services/Study/IQuizService.cs Services/Study/QuizService.cs tests/ltwnc.Tests/Services/Study/QuizServiceTests.cs
git commit -m "feat: preserve quiz timing mode across sessions"
~~~

## Task 2: Validation controller cho preset, custom và untimed

**Files:**
- Modify: Controllers/StudyController.cs
- Test: tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs

**Interfaces:**
- Consumes QuizTimingMode from QuizSetupViewModel.
- Produces a call to _quizService.StartNewAsync(..., int? timeLimitMinutes) where null is only emitted for Untimed.

- [ ] **Step 1: Viết test fail cho ba mode và payload mâu thuẫn**

Thêm test theo mẫu Moq hiện có:

~~~csharp
[Fact]
public async Task QuizStart_post_untimed_starts_session_without_duration()
{
    UserStudySettings settings = new();
    _studyService.Setup(service => service.GetSettingsAsync("user-1"))
        .ReturnsAsync(settings);
    _quizService.Setup(service => service.StartNewAsync(7, "user-1", settings, null))
        .ReturnsAsync(new StudySession { Id = 42 });

    StudyController controller = CreateController("user-1");
    IActionResult result = await controller.QuizStart(7, new QuizSetupViewModel
    {
        TimingMode = QuizTimingMode.Untimed,
        SelectedPresetMinutes = null,
        CustomMinutes = null
    });

    AssertQuizSessionRedirect(result, 7, 42);
}
~~~

Add the custom dispatch test:

~~~csharp
[Fact]
public async Task QuizStart_post_custom_uses_custom_duration()
{
    UserStudySettings settings = new();
    _studyService.Setup(service => service.GetSettingsAsync("user-1"))
        .ReturnsAsync(settings);
    _quizService.Setup(service => service.StartNewAsync(7, "user-1", settings, 37))
        .ReturnsAsync(new StudySession { Id = 42 });

    StudyController controller = CreateController("user-1");
    IActionResult result = await controller.QuizStart(7, new QuizSetupViewModel
    {
        TimingMode = QuizTimingMode.Custom,
        CustomMinutes = 37
    });

    AssertQuizSessionRedirect(result, 7, 42);
}
~~~

Use one theory for invalid and conflicting payloads:

~~~csharp
[Theory]
[InlineData((int)QuizTimingMode.Preset, 121, null)]
[InlineData((int)QuizTimingMode.Custom, null, 0)]
[InlineData((int)QuizTimingMode.Custom, 10, 37)]
[InlineData((int)QuizTimingMode.Untimed, 10, null)]
[InlineData(99, null, null)]
public async Task QuizStart_post_rejects_invalid_or_conflicting_timing_payload(
    int rawMode,
    int? presetMinutes,
    int? customMinutes)
{
    _quizService.Setup(service => service.GetSetupAsync(7, "user-1"))
        .ReturnsAsync(new QuizSetupState { SetId = 7, SetTitle = "Core English" });
    StudyController controller = CreateController("user-1");

    IActionResult result = await controller.QuizStart(7, new QuizSetupViewModel
    {
        TimingMode = (QuizTimingMode)rawMode,
        SelectedPresetMinutes = presetMinutes,
        CustomMinutes = customMinutes
    });

    Assert.IsType<ViewResult>(result);
    Assert.False(controller.ModelState.IsValid);
    _quizService.Verify(service => service.StartNewAsync(
        It.IsAny<int>(),
        It.IsAny<string>(),
        It.IsAny<UserStudySettings>(),
        It.IsAny<int?>()), Times.Never);
}
~~~

- [ ] **Step 2: Chạy test để xác nhận fail**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~StudyControllerQuizTests.QuizStart_post"
~~~

Expected: compile failure for missing TimingMode and Moq setup mismatch, followed by validation failures until the controller is updated.

- [ ] **Step 3: Implement validation and dispatch**

In QuizStart POST, replace duration inference with:

~~~csharp
bool hasValidTimingSelection = input.TimingMode switch
{
    QuizTimingMode.Preset => input.SelectedPresetMinutes is 5 or 10 or 15 or 20
        && input.CustomMinutes is null,
    QuizTimingMode.Custom => input.CustomMinutes is >= QuizService.MinimumQuizMinutes
        and <= QuizService.MaximumQuizMinutes
        && input.SelectedPresetMinutes is null,
    QuizTimingMode.Untimed => input.SelectedPresetMinutes is null
        && input.CustomMinutes is null,
    _ => false
};

int? timeLimitMinutes = input.TimingMode switch
{
    QuizTimingMode.Preset => input.SelectedPresetMinutes,
    QuizTimingMode.Custom => input.CustomMinutes,
    QuizTimingMode.Untimed => null,
    _ => null
};
~~~

Nếu hasValidTimingSelection là false, thêm một lỗi rõ ràng vào TimingMode hoặc CustomMinutes và render lại setup. On GET, set TimingMode = QuizTimingMode.Preset and SelectedPresetMinutes = QuizService.DefaultQuizMinutes.

- [ ] **Step 4: Chạy controller tests**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~StudyControllerQuizTests"
~~~

Expected: PASS, including existing timed-start, invalid duration, unavailable pool, redirect, route and antiforgery tests.

- [ ] **Step 5: Commit task**

~~~powershell
git add Controllers/StudyController.cs tests/ltwnc.Tests/Controllers/StudyControllerQuizTests.cs
git commit -m "feat: validate quiz timing mode selection"
~~~

## Task 3: Razor markup cho split-card setup

**Files:**
- Modify: Views/Study/QuizSetup.cshtml
- Modify: Views/Study/Quiz.cshtml
- Modify: tests/ltwnc.Tests/Views/QuizViewTests.cs
- Modify: tests/ltwnc.Tests/Integration/QuizSetupRenderingTests.cs

**Interfaces:**
- Consumes QuizSetupViewModel.TimingMode, SelectedPresetMinutes, CustomMinutes, SetTitle, SetId and ActiveSessionId.
- Produces named POST form /Study/{setId}/Quiz/Start, antiforgery token, radio controls and hooks consumed by quiz-setup.js.

- [ ] **Step 1: Viết static view tests fail**

Extend QuizViewTests:

~~~csharp
Assert.Contains("quiz-setup-layout", QuizSetupView);
Assert.Contains("quiz-active-session", QuizSetupView);
Assert.Contains("Không giới hạn thời gian", QuizSetupView);
Assert.Contains("asp-for=\"TimingMode\"", QuizSetupView);
Assert.Contains("data-quiz-timing=\"untimed\"", QuizSetupView);
Assert.Contains("asp-validation-summary=\"All\"", QuizSetupView);
Assert.Contains("asp-validation-for=\"TimingMode\"", QuizSetupView);
Assert.Contains("if (Model.DeadlineUtc.HasValue)", QuizView);
~~~

Add assertions for the setup script reference and classes used by the JS: data-quiz-preset, data-quiz-custom and data-quiz-submit-label.

- [ ] **Step 2: Chạy view/integration tests để xác nhận fail**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests.Quiz_setup|FullyQualifiedName~QuizSetupRenderingTests"
~~~

Expected: FAIL because the current generic fieldset does not contain the new split-card hooks.

- [ ] **Step 3: Thay markup setup bằng cấu trúc đã chốt**

Thay nội dung card setup hiện tại bằng markup sau; giữ header/exit hiện có và thêm class quiz-stage-setup cho main:

~~~cshtml
<div class="quiz-setup-layout" data-quiz-setup>
    <section class="quiz-setup-main" aria-labelledby="quiz-setup-title">
        <span class="quiz-eyebrow">Trắc nghiệm</span>
        <h2 id="quiz-setup-title">Thiết lập bài trắc nghiệm</h2>
        <p class="quiz-setup-intro">Chọn nhịp độ phù hợp trước khi bắt đầu.</p>

        <form asp-route="QuizStartPost" asp-route-setId="@Model.SetId" method="post">
            @Html.AntiForgeryToken()
            <input asp-for="TimingMode" type="hidden" data-quiz-mode-input />
            <input asp-for="SelectedPresetMinutes" type="hidden" data-quiz-preset-input />
            <div asp-validation-summary="All" role="alert"></div>
            <span asp-validation-for="TimingMode" class="quiz-validation-message"></span>

            <fieldset class="quiz-timing-fieldset">
                <legend>Thời lượng</legend>
                <div class="quiz-timing-grid">
                    @foreach (int minutes in new[] { 5, 10, 15, 20 })
                    {
                        <label class="quiz-timing-card" data-quiz-preset="@minutes">
                            <input type="radio"
                                   name="TimingChoice"
                                   value="preset-@minutes"
                                   data-quiz-option
                                   data-quiz-mode="Preset"
                                   data-quiz-minutes="@minutes" />
                            <span>
                                <strong>@minutes phút</strong>
                                <small>Nhịp độ gợi ý</small>
                            </span>
                        </label>
                    }
                </div>

                <label class="quiz-timing-card quiz-timing-card-untimed"
                       data-quiz-timing="untimed">
                    <input type="radio"
                           name="TimingChoice"
                           value="untimed"
                           data-quiz-option
                           data-quiz-mode="Untimed" />
                    <span class="quiz-timing-icon" aria-hidden="true">∞</span>
                    <span>
                        <strong>Không giới hạn thời gian</strong>
                        <small>Làm bài theo tốc độ của bạn</small>
                    </span>
                </label>

                <div class="quiz-timing-card quiz-timing-card-custom" data-quiz-custom>
                    <input type="radio"
                           id="quiz-timing-custom"
                           name="TimingChoice"
                           value="custom"
                           data-quiz-option
                           data-quiz-mode="Custom" />
                    <label for="quiz-timing-custom">
                        <strong>Thời lượng tùy chỉnh</strong>
                        <small>Chọn từ 1 đến 120 phút</small>
                    </label>
                    <span class="quiz-custom-input">
                        <label asp-for="CustomMinutes" class="visually-hidden">Số phút tùy chỉnh</label>
                        <input asp-for="CustomMinutes"
                               type="number"
                               min="1"
                               max="120"
                               step="1"
                               data-quiz-custom-input />
                        <span>phút</span>
                    </span>
                </div>
                <span asp-validation-for="CustomMinutes"></span>
            </fieldset>

            <button type="submit"
                    class="quiz-action quiz-action-primary quiz-setup-submit"
                    data-quiz-submit-label>Bắt đầu bài kiểm tra</button>
        </form>
    </section>

    @if (Model.ActiveSessionId.HasValue)
    {
        <aside class="quiz-active-session" aria-labelledby="quiz-active-title">
            <span class="quiz-eyebrow">Đang dở</span>
            <h2 id="quiz-active-title">Bạn còn một bài chưa hoàn thành</h2>
            <p>Tiến độ hiện tại đã được lưu lại.</p>
            <a class="quiz-action quiz-action-light"
               asp-action="Quiz"
               asp-route-setId="@Model.SetId"
               asp-route-sessionId="@Model.ActiveSessionId">Tiếp tục làm bài</a>
        </aside>
    }
</div>
~~~

TimingChoice là radio group phục vụ accessibility/UI và không bind vào model. Hai hidden field là payload duy nhất cho TimingMode và SelectedPresetMinutes, tránh POST nhiều giá trị cùng tên.

Trong Quiz.cshtml, bọc timer hiện có bằng điều kiện để untimed không render ký hiệu --:--:

~~~cshtml
@if (Model.DeadlineUtc.HasValue)
{
    <div class="quiz-timer"
         data-quiz-timer
         role="timer"
         aria-live="polite"
         aria-label="Thời gian còn lại">--:--</div>
}
~~~

- [ ] **Step 4: Chạy view and integration tests**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests.Quiz_setup|FullyQualifiedName~QuizSetupRenderingTests"
~~~

Expected: PASS for markup contract and named route/antiforgery rendering.

- [ ] **Step 5: Commit task**

~~~powershell
git add Views/Study/QuizSetup.cshtml Views/Study/Quiz.cshtml tests/ltwnc.Tests/Views/QuizViewTests.cs tests/ltwnc.Tests/Integration/QuizSetupRenderingTests.cs
git commit -m "feat: add split card quiz setup markup"
~~~

## Task 4: CSS và client state cho các thẻ lựa chọn

**Files:**
- Create: wwwroot/js/quiz-setup.js
- Modify: wwwroot/css/quiz.css
- Modify: Views/Study/QuizSetup.cshtml
- Modify: tests/ltwnc.Tests/Views/QuizViewTests.cs

**Interfaces:**
- Consumes data-quiz-setup, data-quiz-preset, data-quiz-timing="untimed", data-quiz-custom, data-quiz-mode-input, data-quiz-preset-input, data-quiz-custom-input and data-quiz-submit-label.
- Produces mutually-exclusive checked/disabled state and CTA labels without changing server payload semantics.

- [ ] **Step 1: Viết contract tests fail cho CSS/JS**

~~~csharp
private static readonly string QuizSetupScript = ReadFile("wwwroot", "js", "quiz-setup.js");

[Fact]
public void Quiz_setup_state_contract_supports_untimed_and_reduced_motion()
{
    Assert.Contains("data-quiz-setup", QuizSetupView);
    Assert.Contains("data-quiz-setup", QuizSetupScript);
    Assert.Contains("data-quiz-preset-input", QuizSetupScript);
    Assert.Contains("Bắt đầu không giới hạn", QuizSetupScript);
    Assert.Contains("submitLabel.disabled = true", QuizSetupScript);
    Assert.Contains("prefers-reduced-motion", QuizStyles);
    Assert.Contains("quiz-setup-layout", QuizStyles);
    Assert.Contains("quiz-timing-card.is-selected", QuizStyles);
}
~~~

- [ ] **Step 2: Chạy tests để xác nhận fail**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests.Quiz_setup_state_contract"
~~~

Expected: FAIL because the setup script and new CSS selectors do not exist.

- [ ] **Step 3: Implement script và CSS**

Create quiz-setup.js as a dependency-free IIFE:

~~~javascript
(() => {
    const root = document.querySelector('[data-quiz-setup]');
    if (!root) return;

    const options = [...root.querySelectorAll('[data-quiz-option]')];
    const modeInput = root.querySelector('[data-quiz-mode-input]');
    const presetInput = root.querySelector('[data-quiz-preset-input]');
    const customInput = root.querySelector('[data-quiz-custom-input]');
    const submitLabel = root.querySelector('[data-quiz-submit-label]');
    const form = root.querySelector('form');

    const applyOption = option => {
        const mode = option?.dataset.quizMode ?? 'Preset';
        options.forEach(item => {
            item.checked = item === option;
            item.closest('.quiz-timing-card')?.classList.toggle(
                'is-selected',
                item === option);
        });

        if (modeInput) modeInput.value = mode;
        if (mode === 'Preset' && presetInput) {
            presetInput.value = option?.dataset.quizMinutes ?? '10';
        }
        if (presetInput) presetInput.disabled = mode !== 'Preset';
        if (customInput) customInput.disabled = mode !== 'Custom';
        if (submitLabel) {
            submitLabel.textContent = mode === 'Untimed'
                ? 'Bắt đầu không giới hạn'
                : 'Bắt đầu bài kiểm tra';
        }
    };

    const initialMode = modeInput?.value || 'Preset';
    const initialPreset = presetInput?.value || '10';
    const initialOption = options.find(option =>
        option.dataset.quizMode === initialMode
        && (initialMode !== 'Preset'
            || option.dataset.quizMinutes === initialPreset))
        ?? options.find(option => option.dataset.quizMinutes === '10')
        ?? options[0];

    options.forEach(option => option.addEventListener('change', () => {
        if (option.checked) applyOption(option);
    }));

    root.querySelector('[data-quiz-custom]')?.addEventListener('click', () => {
        const option = options.find(item => item.dataset.quizMode === 'Custom');
        if (option) applyOption(option);
    });

    form?.addEventListener('submit', () => {
        if (!form.checkValidity() || !submitLabel) return;
        submitLabel.disabled = true;
        submitLabel.setAttribute('aria-busy', 'true');
    });

    applyOption(initialOption);
})();
~~~

Append these setup-only rules to quiz.css, merging duplicate media queries instead of creating conflicting copies:

~~~css
.quiz-stage-setup { width: min(100%, 1120px); }

.quiz-setup-layout {
    display: grid;
    grid-template-columns: minmax(0, 1.65fr) minmax(280px, 0.85fr);
    gap: 1.25rem;
    align-items: stretch;
}

.quiz-setup-layout:not(:has(.quiz-active-session)) {
    grid-template-columns: minmax(0, 760px);
    justify-content: center;
}

.quiz-setup-main,
.quiz-active-session {
    border-radius: 28px;
    padding: clamp(1.5rem, 4vw, 2.75rem);
}

.quiz-setup-main {
    border: 1px solid var(--quiz-line);
    background: #fffdf9;
    box-shadow: 0 24px 64px rgba(28, 25, 23, 0.08);
}

.quiz-setup-main h2,
.quiz-active-session h2 {
    margin: 0.45rem 0 0.6rem;
    font-size: clamp(1.75rem, 4vw, 2.65rem);
    line-height: 1.05;
    letter-spacing: -0.04em;
}

.quiz-setup-intro { margin: 0 0 1.75rem; color: var(--quiz-muted); }
.quiz-timing-fieldset { margin: 0; padding: 0; border: 0; }
.quiz-timing-fieldset legend { margin-bottom: 0.8rem; font-weight: 800; }

.quiz-timing-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.75rem;
}

.quiz-timing-card {
    position: relative;
    display: flex;
    align-items: center;
    gap: 0.85rem;
    min-height: 78px;
    margin-top: 0.75rem;
    padding: 1rem;
    border: 1.5px solid var(--quiz-line);
    border-radius: 18px;
    background: #ffffff;
    cursor: pointer;
    transition: transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease;
}

.quiz-timing-card:hover { transform: translateY(-2px); border-color: #c7b9a8; }
.quiz-timing-card:focus-within { outline: 3px solid #92400e; outline-offset: 3px; }
.quiz-timing-card.is-selected { border-color: #b45309; box-shadow: inset 0 0 0 1px #b45309; }
.quiz-timing-card input[type="radio"] { accent-color: #b45309; }
.quiz-timing-card strong,
.quiz-timing-card small { display: block; }
.quiz-timing-card small { margin-top: 0.2rem; color: var(--quiz-muted); }

.quiz-timing-card-untimed { background: #fff7ed; }
.quiz-timing-icon { font-size: 2rem; font-weight: 800; line-height: 1; }
.quiz-timing-card-custom { flex-wrap: wrap; }
.quiz-custom-input { display: inline-flex; align-items: center; gap: 0.4rem; margin-left: auto; }
.quiz-custom-input input { width: 78px; min-height: 44px; border: 1px solid var(--quiz-line); border-radius: 12px; padding: 0.5rem; }
.quiz-setup-submit { width: 100%; margin-top: 1.25rem; background: #b45309; }

.quiz-active-session {
    display: flex;
    flex-direction: column;
    justify-content: center;
    background: #292524;
    color: #ffffff;
}

.quiz-active-session p { color: #d6d3d1; }
.quiz-active-session .quiz-eyebrow { color: #fdba74; }
.quiz-action-light { margin-top: 1rem; background: #ffffff; color: #292524; }

@media (max-width: 760px) {
    .quiz-setup-layout { grid-template-columns: 1fr; }
    .quiz-active-session { grid-row: 1; }
    .quiz-setup-main { grid-row: 2; }
    .quiz-timing-grid { grid-template-columns: 1fr; }
    .quiz-custom-input { width: 100%; margin-left: 0; }
}

@media (prefers-reduced-motion: reduce) {
    .quiz-timing-card,
    .quiz-setup-submit { transition: none; }
}
~~~

Include the script in QuizSetup.cshtml under @section Scripts with asp-append-version="true".

- [ ] **Step 4: Chạy static tests và build**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizViewTests"
dotnet build ltwnc.csproj --no-restore
~~~

Expected: PASS for all view contract tests and a successful build.

- [ ] **Step 5: Commit task**

~~~powershell
git add wwwroot/js/quiz-setup.js wwwroot/css/quiz.css Views/Study/QuizSetup.cshtml tests/ltwnc.Tests/Views/QuizViewTests.cs
git commit -m "feat: style and wire quiz timing cards"
~~~

## Task 5: Hồi quy, browser smoke test và hoàn tất

**Files:**
- Modify only if verification exposes a concrete defect: files from Tasks 1–4.
- Test: all existing test projects and the running local Study quiz page.

**Interfaces:**
- Consumes the completed setup page and quiz service behavior.
- Produces verified timed/untimed user flow with no database schema change.

- [ ] **Step 1: Chạy toàn bộ test suite**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
~~~

Expected: all tests pass; record the final passed/failed count before claiming completion.

- [ ] **Step 2: Chạy app và kiểm tra browser smoke test**

Open /Study/{setId}/Quiz with a set containing cards and verify:

1. Desktop with active session shows right dark continuation card; without active session the form is centered.
2. Clicking each preset selects only that card and CTA remains Bắt đầu bài kiểm tra.
3. Clicking Không giới hạn thời gian selects only the untimed card, disables custom input and changes CTA to Bắt đầu không giới hạn.
4. Focusing custom input selects custom mode and re-enables its input.
5. Submitting untimed opens quiz without a countdown; submitting 10 minutes shows countdown.
6. Complete an untimed quiz, use Làm lại câu sai, then Làm lại toàn bộ; both new sessions remain untimed and are not locked to the prior subset.
7. Resize to mobile width and verify active card appears above the form with no horizontal scroll.
8. Tab through controls and verify visible focus; enable reduced motion and verify no setup transition is required for use.

- [ ] **Step 3: Re-run targeted service/controller/view tests after smoke test**

~~~powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizServiceTests|FullyQualifiedName~StudyControllerQuizTests|FullyQualifiedName~QuizViewTests|FullyQualifiedName~QuizSetupRenderingTests"
~~~

Expected: PASS with no new failures.

- [ ] **Step 4: Review diff and ensure unrelated changes are preserved**

~~~powershell
git diff origin/feature/quiz-study-mode...HEAD --stat
git status --short
~~~

Expected: feature files and tests are committed; the pre-existing hover files remain untouched by these tasks and are not removed.

- [ ] **Step 5: Kết thúc verification**

Nếu không có defect, không tạo commit rỗng. Nếu có defect, quay lại task sở hữu file đó, bổ sung một test fail cụ thể, thực hiện lại chu kỳ red/green và dùng chính lệnh git add với danh sách file của task đó trước khi commit.
