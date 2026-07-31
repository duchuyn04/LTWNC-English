namespace ltwnc.Tests.Views;

public sealed class FlashcardSetDetailsViewTests
{
    [Fact]
    public void Details_UsesOwnerOnlyPostFormForDuplication()
    {
        string view = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Views",
            "FlashcardSet",
            "Details.cshtml"));

        Assert.Contains("@if (!Model.IsQuarantined)", view);
        Assert.Contains("asp-action=\"Duplicate\"", view);
        Assert.Contains("method=\"post\"", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("Nhân bản", view);
        Assert.Contains("TempData[\"DuplicateError\"]", view);
    }

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
