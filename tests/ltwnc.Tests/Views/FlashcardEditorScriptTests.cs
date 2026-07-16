using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public class FlashcardEditorScriptTests
{
    private static readonly string Script = ReadFile("wwwroot", "js", "flashcard-editor.js");
    private static readonly string View = ReadFile("Views", "FlashcardSet", "Edit.cshtml");

    [Fact]
    public void Script_posts_toggle_requests_with_antiforgery_and_reads_response_state()
    {
        Assert.Contains("fetch", Script);
        Assert.Contains("RequestVerificationToken", Script);
        Assert.Contains("response.json()", Script);
        Assert.Contains("isStarred", Script);
        Assert.Contains("input.dataset.toggleStarUrl", Script);
    }

    [Fact]
    public void Script_rolls_back_failed_toggles_and_guards_pending_requests()
    {
        Assert.Matches(new Regex("rollback|restore|previous", RegexOptions.IgnoreCase), Script);
        Assert.Contains("const previousChecked = !input.checked", Script);
        Assert.Contains("setStarState(cardId, previousChecked)", Script);
        Assert.Contains("data-star-pending", Script);
        Assert.Contains("preventDefault()", Script);
        Assert.DoesNotContain("input.disabled", Script);
        Assert.Contains("error", Script);
    }

    [Fact]
    public void Script_supports_navigation_and_textarea_auto_grow()
    {
        Assert.Contains("scrollIntoView", Script);
        Assert.Contains("scrollHeight", Script);
        Assert.Contains("document.querySelectorAll('textarea[data-auto-grow]')", Script);
    }

    [Fact]
    public void Selecting_a_card_recalculates_auto_grow_textareas_after_activating_its_panel()
    {
        Assert.Matches(
            new Regex(
                "panel\\.classList\\.toggle\\('is-active', active\\);[\\s\\S]*?" +
                "if \\(active\\) \\{[\\s\\S]*?" +
                "panel\\.querySelectorAll\\('textarea\\[data-auto-grow\\]'\\)\\.forEach\\(growTextarea\\)",
                RegexOptions.Singleline),
            Script);
        Assert.Contains("if (firstCard) selectCard(firstCard.dataset.cardId)", Script);
    }

    [Fact]
    public void Auto_grow_listener_binding_is_idempotent()
    {
        Assert.Contains("textarea.dataset.autoGrowBound === 'true'", Script);
        Assert.Contains("textarea.dataset.autoGrowBound = 'true'", Script);
    }

    [Fact]
    public void Script_keeps_the_vocabulary_list_equal_to_the_visible_detail_height()
    {
        Assert.Contains("function syncEditorPanelHeights()", Script);
        Assert.Contains("--vocab-detail-height", Script);
        Assert.Contains("detail.getBoundingClientRect().height", Script);
        Assert.Contains("new ResizeObserver(syncEditorPanelHeights)", Script);
        Assert.Contains("window.addEventListener('resize', syncEditorPanelHeights)", Script);
        Assert.Matches(
            new Regex(
                "panel\\.querySelectorAll\\('textarea\\[data-auto-grow\\]'\\)\\.forEach\\(growTextarea\\);[\\s\\S]*?" +
                "syncEditorPanelHeights\\(\\)",
                RegexOptions.Singleline),
            Script);
    }

    [Fact]
    public void Edit_view_marks_back_text_and_example_meaning_textareas_for_auto_grow()
    {
        Assert.Equal(2, AutoGrowTextareas("backText").Count);
        Assert.Equal(2, AutoGrowTextareas("exampleMeaning").Count);
        Assert.DoesNotMatch(new Regex("<input[^>]+name=\"backText\"", RegexOptions.Singleline), View);
    }

    [Fact]
    public void Edit_view_references_external_editor_script_with_cache_busting()
    {
        Assert.Matches(
            new Regex("<script[^>]+src=\"~/js/flashcard-editor\\.js\"[^>]+asp-append-version=\"true\"", RegexOptions.Singleline),
            View);
    }

    [Fact]
    public void Batch_toolbar_stays_hidden_until_a_card_is_selected()
    {
        Assert.Contains("function syncBatchToolbar(form)", Script);
        Assert.Contains("input[name=\"selectedCardIds\"]:checked", Script);
        Assert.Contains("toolbar.hidden = !hasSelection", Script);
        Assert.Contains("input[name=\"selectedCardIds\"]", Script);
        Assert.Matches(
            new Regex("class=\"batch-toolbar[^\"]*\"[^>]*hidden", RegexOptions.Singleline),
            View);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        return string.Empty;
    }

    private static MatchCollection AutoGrowTextareas(string name) =>
        Regex.Matches(
            View,
            $"<textarea(?=[^>]*name=\"{name}\")(?=[^>]*class=\"form-input-custom\")(?=[^>]*rows=\"2\")(?=[^>]*data-auto-grow)(?=[^>]*required)[^>]*>",
            RegexOptions.Singleline);
}
