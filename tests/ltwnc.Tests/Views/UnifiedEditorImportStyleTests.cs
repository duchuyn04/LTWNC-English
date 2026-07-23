using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public sealed class UnifiedEditorImportStyleTests
{
    private static readonly string Css =
        ReadFile("wwwroot", "css", "unified-editor.css");

    [Fact]
    public void File_import_styles_cover_tabs_dropzone_preview_and_mobile_layout()
    {
        Assert.Contains(".import-tabs", Css);
        Assert.Contains(".import-dropzone", Css);
        Assert.Contains(".import-mode-options", Css);
        Assert.Contains(".import-preview-table-wrap", Css);
        Assert.Contains("overflow-x: auto", Css);
        Assert.Matches(
            new Regex(
                "@media \\(max-width: 767px\\)[\\s\\S]*?\\.modal-actions",
                RegexOptions.Singleline),
            Css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", Css);
    }

    [Fact]
    public void Import_controls_have_visible_keyboard_focus_and_selected_states()
    {
        Assert.Contains(".import-tab.is-active", Css);
        Assert.Contains(".import-tab:focus-visible", Css);
        Assert.Contains(".import-dropzone:focus-within", Css);
        Assert.Contains(".import-mode-options label:has(input:checked)", Css);
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
