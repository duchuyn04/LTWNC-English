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
        Assert.Matches(new Regex("pending|inFlight|disabled", RegexOptions.IgnoreCase), Script);
        Assert.Contains("error", Script);
    }

    [Fact]
    public void Script_supports_navigation_and_textarea_auto_grow()
    {
        Assert.Contains("scrollIntoView", Script);
        Assert.Contains("scrollHeight", Script);
        Assert.Contains("backText", Script);
        Assert.Contains("exampleMeaning", Script);
    }

    [Fact]
    public void Edit_view_references_external_editor_script_with_cache_busting()
    {
        Assert.Matches(
            new Regex("<script[^>]+src=\"~/js/flashcard-editor\\.js\"[^>]+asp-append-version=\"true\"", RegexOptions.Singleline),
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
}
