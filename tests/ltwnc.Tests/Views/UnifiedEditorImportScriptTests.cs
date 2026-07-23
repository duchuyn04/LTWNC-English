using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public sealed class UnifiedEditorImportScriptTests
{
    private static readonly string Script =
        ReadFile("wwwroot", "js", "unified-editor.js");

    [Fact]
    public void Import_click_confirms_and_saves_a_new_set_before_opening_modal()
    {
        Assert.Contains("window.confirm", Script);
        Assert.Contains("await ensureSetCreated()", Script);
        Assert.Contains("function openImportModal()", Script);
        Assert.Matches(
            new Regex(
                "btnImport\\.addEventListener\\('click',[\\s\\S]*?openImportModal\\(\\)",
                RegexOptions.Singleline),
            Script);
    }

    [Fact]
    public void File_preview_posts_formdata_and_renders_untrusted_values_as_text()
    {
        Assert.Contains("new FormData(fileImportForm)", Script);
        Assert.Contains("/Import/Preview", Script);
        Assert.Contains("RequestVerificationToken", Script);
        Assert.Contains("cell.textContent", Script);
        Assert.DoesNotContain("previewRow.innerHTML", Script);
    }

    [Fact]
    public void Replace_commit_requires_confirmation()
    {
        Assert.Contains("Toàn bộ thẻ và tiến độ học", Script);
        Assert.Contains("fileImportForm.submit()", Script);
        Assert.Contains("replaceAll", Script);
    }

    [Fact]
    public void File_or_mode_changes_invalidate_the_previous_preview()
    {
        Assert.Contains("function resetFilePreview()", Script);
        Assert.Matches(
            new Regex(
                "importFile\\.addEventListener\\('change',[\\s\\S]*?resetFilePreview\\(\\)",
                RegexOptions.Singleline),
            Script);
        Assert.Matches(
            new Regex(
                "importModeInputs\\.forEach[\\s\\S]*?resetFilePreview",
                RegexOptions.Singleline),
            Script);
    }

    [Fact]
    public void Modal_supports_tabs_escape_and_focus_return()
    {
        Assert.Contains("function activateImportTab", Script);
        Assert.Contains("event.key === 'Escape'", Script);
        Assert.Contains("lastImportTrigger?.focus()", Script);
        Assert.Contains("aria-selected", Script);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }
}
