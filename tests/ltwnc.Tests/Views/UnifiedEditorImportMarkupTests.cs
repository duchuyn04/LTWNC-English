namespace ltwnc.Tests.Views;

public sealed class UnifiedEditorImportMarkupTests
{
    private static readonly string Source =
        ReadFile("Views", "FlashcardSet", "Editor.cshtml");

    [Fact]
    public void Import_modal_exposes_accessible_file_and_paste_tabs()
    {
        Assert.Contains("role=\"dialog\"", Source);
        Assert.Contains("aria-modal=\"true\"", Source);
        Assert.Contains("role=\"tablist\"", Source);
        Assert.Contains("id=\"import-tab-file\"", Source);
        Assert.Contains("id=\"import-tab-paste\"", Source);
        Assert.Contains("id=\"import-panel-file\"", Source);
        Assert.Contains("id=\"import-panel-paste\"", Source);
    }

    [Fact]
    public void File_import_form_accepts_csv_xlsx_and_has_antiforgery()
    {
        Assert.Contains("id=\"file-import-form\"", Source);
        Assert.Contains("enctype=\"multipart/form-data\"", Source);
        Assert.Contains("accept=\".csv,.xlsx\"", Source);
        Assert.Contains("@Html.AntiForgeryToken()", Source);
        Assert.Contains("id=\"btn-file-preview\"", Source);
        Assert.Contains("id=\"file-import-preview\"", Source);
    }

    [Fact]
    public void File_import_offers_template_and_append_replace_modes()
    {
        Assert.Contains("~/templates/flashcard-import-template.csv", Source);
        Assert.Contains("id=\"import-mode-append\"", Source);
        Assert.Contains("id=\"import-mode-replace\"", Source);
        Assert.Contains("name=\"replaceAll\"", Source);
        Assert.Contains("value=\"true\"", Source);
    }

    [Fact]
    public void Editor_renders_import_feedback_after_redirect()
    {
        Assert.Contains("TempData[\"ImportImportedCount\"]", Source);
        Assert.Contains("TempData[\"ImportSkippedCount\"]", Source);
        Assert.Contains("TempData[\"ImportErrors\"]", Source);
        Assert.Contains("TempData[\"ImportErrorsOmittedCount\"]", Source);
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
