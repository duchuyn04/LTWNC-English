namespace ltwnc.Tests.Views;

public sealed class CardActionEditorViewTests
{
    [Fact]
    public void Editor_exposes_command_selection_and_actions()
    {
        string view = Read("Views/FlashcardSet/Editor.cshtml");

        Assert.Contains("data-card-selection", view);
        Assert.Contains("data-batch-toolbar", view);
        Assert.Contains("data-batch-action=\"Star\"", view);
        Assert.Contains("data-batch-action=\"Unstar\"", view);
        Assert.Contains("data-batch-action=\"Delete\"", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
    }

    [Fact]
    public void Editor_script_posts_batch_commands_and_offers_undo()
    {
        string script = Read("wwwroot/js/unified-editor.js");

        Assert.Contains("function submitBatchAction(action)", script);
        Assert.Contains("new FormData()", script);
        Assert.Contains("selectedCardIds", script);
        Assert.Contains("/BatchAction", script);
        Assert.Contains("CardActions/Undo/", script);
        Assert.Contains("function applyBatchResult(result)", script);
    }

    [Fact]
    public void Editor_script_does_not_select_unsaved_cards_for_commands()
    {
        string script = Read("wwwroot/js/unified-editor.js");

        Assert.Contains("input.disabled = !persisted", script);
        Assert.Contains("Number.isInteger(id) && id > 0", script);
        Assert.Contains("input:not([data-card-selection])", script);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
