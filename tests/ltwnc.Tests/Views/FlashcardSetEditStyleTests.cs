using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public class FlashcardSetEditStyleTests
{
    private static readonly string Source = File.ReadAllText(FindEditStylesheet());

    [Fact]
    public void Vocabulary_list_scrolls_but_detail_expands_to_fit_its_form()
    {
        Assert.Matches(
            Rule("\\.vocab-editor", "align-items:\\s*start"),
            Source);
        Assert.Matches(
            Rule(
                "\\.vocab-list",
                "height:\\s*var\\(--vocab-detail-height,\\s*min\\(70vh,\\s*720px\\)\\)[^}]*" +
                "max-height:\\s*var\\(--vocab-detail-height,\\s*min\\(70vh,\\s*720px\\)\\)[^}]*" +
                "overflow-y:\\s*auto[^}]*min-height:\\s*0[^}]*box-sizing:\\s*border-box"),
            Source);
        Assert.Matches(
            Rule(
                "\\.vocab-detail",
                "max-height:\\s*none[^}]*overflow-y:\\s*visible"),
            Source);
    }

    [Fact]
    public void Editor_sidebar_is_sticky_and_has_visible_link_focus()
    {
        Assert.Contains(".set-editor-sidebar", Source);
        Assert.Matches(
            Rule("\\.set-editor-sidebar", "position:\\s*sticky"),
            Source);
        Assert.Matches(
            Rule("\\.set-editor-sidebar\\s+a:focus-visible", "outline:\\s*[^;]+"),
            Source);
    }

    [Fact]
    public void Star_checkbox_has_distinct_unchecked_checked_and_focus_states()
    {
        var starRule = Regex.Match(Source, "\\.star-checkbox\\s*\\{(?<declarations>[^}]*)\\}");

        Assert.True(starRule.Success);
        Assert.Matches(
            new Regex("(?m)^\\s*appearance:\\s*none;"),
            starRule.Groups["declarations"].Value);
        Assert.DoesNotMatch(
            new Regex("display:\\s*none|visibility:\\s*hidden"),
            starRule.Groups["declarations"].Value);
        Assert.Matches(
            Rule(
                "\\.star-checkbox\\s*\\+\\s*\\.star-toggle::before",
                "content:\\s*['\"]\\\\2606['\"]"),
            Source);
        Assert.Matches(
            Rule(
                "\\.star-checkbox:checked\\s*\\+\\s*\\.star-toggle::before",
                "content:\\s*['\"]\\\\2605['\"]"),
            Source);
        Assert.Matches(
            Rule(
                "\\.star-checkbox:focus-visible\\s*\\+\\s*\\.star-toggle::before",
                "outline:\\s*[^;]+"),
            Source);
    }

    [Fact]
    public void Editor_content_uses_the_available_layout_width()
    {
        Assert.Matches(
            Rule(
                "\\.set-editor-content\\s*>\\s*\\.row\\s*>\\s*\\.col-md-10",
                "width:\\s*100%"),
            Source);
    }

    [Fact]
    public void Editor_inputs_fit_their_grid_columns()
    {
        Assert.Matches(
            Rule("\\.set-editor-content\\s+\\.vocab-grid\\s*>\\s*label", "min-width:\\s*0"),
            Source);
        Assert.Matches(
            Rule(
                "\\.set-editor-content\\s+\\.form-input-custom",
                "width:\\s*100%[^}]*box-sizing:\\s*border-box"),
            Source);
    }

    [Fact]
    public void Auto_grow_textareas_have_sensible_height_bounds_and_overflow()
    {
        Assert.Matches(
            Rule(
                "textarea\\[data-auto-grow\\]",
                "min-height:\\s*[^;]+;[^}]*max-height:\\s*220px;[^}]*overflow-y:\\s*auto"),
            Source);
    }

    [Fact]
    public void Add_card_form_is_visually_separated_from_the_split_editor()
    {
        Assert.Matches(
            Rule("\\.vocab-editor", "padding-bottom:\\s*3rem"),
            Source);
        Assert.Matches(
            Rule("\\.add-card-form", "margin-top:\\s*3rem"),
            Source);
    }

    [Fact]
    public void Sidebar_becomes_horizontally_scrollable_on_mobile()
    {
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{[\\s\\S]*?" +
                RulePattern(
                    "\\.set-editor-sidebar",
                    "position:\\s*static[^}]*overflow-x:\\s*auto") +
                "[\\s\\S]*?" +
                RulePattern(
                    "\\.set-editor-sidebar\\s+nav",
                    "display:\\s*flex[^}]*flex-wrap:\\s*nowrap"),
                RegexOptions.Singleline),
            Source);
    }

    private static Regex Rule(string selector, string declarations) =>
        new(RulePattern(selector, declarations), RegexOptions.Singleline);

    private static string RulePattern(string selector, string declarations) =>
        $"{selector}\\s*\\{{[^}}]*{declarations}[^}}]*\\}}";

    private static string FindEditStylesheet()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "wwwroot", "css", "edit.css");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate wwwroot/css/edit.css.");
    }
}
