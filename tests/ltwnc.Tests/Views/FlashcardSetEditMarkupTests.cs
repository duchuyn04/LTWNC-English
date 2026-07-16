using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public class FlashcardSetEditMarkupTests
{
    private static readonly string Source = File.ReadAllText(FindEditView());

    [Fact]
    public void Sidebar_links_target_all_editor_sections()
    {
        Assert.Contains("class=\"set-editor-sidebar\"", Source);
        Assert.Contains("href=\"#set-info\"", Source);
        Assert.Contains("href=\"#file-import\"", Source);
        Assert.Contains("href=\"#card-list\"", Source);
        Assert.Contains("href=\"#add-card-form\"", Source);
        Assert.Contains("id=\"set-info\"", Source);
        Assert.Contains("id=\"file-import\"", Source);
        Assert.Contains("id=\"card-list\"", Source);
    }

    [Fact]
    public void Editor_content_does_not_add_a_nested_main_landmark()
    {
        Assert.DoesNotContain("<main class=\"set-editor-content\">", Source);
        Assert.Contains("<div class=\"set-editor-content\">", Source);
    }

    [Fact]
    public void Add_card_form_is_outside_editor_and_redundant_header_link_is_removed()
    {
        Assert.DoesNotMatch(
            new Regex("vocab-list-header[\\s\\S]*?<a[^>]+href=\\\"#add-card-form\\\""),
            Source);
        Assert.Matches(
            new Regex("</section>\\s*</div>\\s*<section[^>]*>\\s*<form[^>]+id=\\\"add-card-form\\\"", RegexOptions.Singleline),
            Source);
    }

    [Fact]
    public void Existing_card_panels_and_batch_selection_remain_in_editor()
    {
        Assert.Matches(
            new Regex("<section class=\\\"vocab-detail\\\">[\\s\\S]*?class=\\\"vocab-card-panel\\\"[\\s\\S]*?</section>"),
            Source);
        Assert.Contains("name=\"selectedCardIds\"", Source);
    }

    [Fact]
    public void Existing_card_star_checkbox_exposes_ajax_and_accessibility_attributes()
    {
        Assert.Matches(
            new Regex(
                "<input[^>]+type=\\\"checkbox\\\"[^>]+name=\\\"isStarred\\\"[^>]+class=\\\"visually-hidden\\\"[^>]+data-toggle-star-url=[^>]+data-card-id=[^>]+data-star-target=[^>]+aria-label=[^>]+checked",
                RegexOptions.Singleline),
            Source);
        Assert.Contains("@Html.AntiForgeryToken()", Source);
        Assert.Matches(
            new Regex(
                "class=\\\"vocab-card-form\\\">[\\s\\S]*?@Html\\.AntiForgeryToken\\(\\)[\\s\\S]*?id=\\\"star-card-@card\\.Id\\\"[\\s\\S]*?<label[^>]+for=\\\"star-card-@card\\.Id\\\"",
                RegexOptions.Singleline),
            Source);
        Assert.Contains("<input type=\"hidden\" name=\"isStarred\" value=\"false\" />", Source);
    }

    [Fact]
    public void Card_navigation_selector_excludes_star_controls()
    {
        Assert.DoesNotContain("document.querySelectorAll('[data-card-id]')", Source);
        Assert.Contains("document.querySelectorAll('.vocab-list-item[data-card-id]')", Source);
        Assert.Contains("document.querySelector('.vocab-list-item[data-card-id]')", Source);
    }

    private static string FindEditView()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Views", "FlashcardSet", "Edit.cshtml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Views/FlashcardSet/Edit.cshtml.");
    }
}
