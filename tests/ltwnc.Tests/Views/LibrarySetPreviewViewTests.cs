namespace ltwnc.Tests.Views;

public sealed class LibrarySetPreviewViewTests
{
    [Fact]
    public void LibraryLinksAuthorToPublicProfile()
    {
        string view = Read("Views/Library/Index.cshtml");

        Assert.Contains("class=\"library-author-row library-author-link\"", view);
        Assert.Contains("class=\"library-author-row library-author-fallback\"", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(item.AuthorUsername))", view);
        Assert.Contains("asp-route=\"PublicProfile\"", view);
        Assert.Contains("asp-route-username=\"@item.AuthorUsername\"", view);
    }

    [Fact]
    public void DetailsRendersAuthorAndAllOptionalCardFields()
    {
        string view = Read("Views/FlashcardSet/Details.cshtml");

        Assert.Contains("set-detail-author-link", view);
        Assert.Contains("asp-route-username=\"@Model.AuthorUsername\"", view);
        Assert.Contains("set-detail-card", view);
        Assert.Contains("card.FrontText", view);
        Assert.Contains("card.BackText", view);
        Assert.Contains("card.Pronunciation", view);
        Assert.Contains("card.PartOfSpeech", view);
        Assert.Contains("card.ExampleSentence", view);
        Assert.Contains("card.ExampleMeaning", view);
        Assert.Contains("card.Synonyms", view);
        Assert.Contains("card.UploadedImagePath", view);
        Assert.Contains("card.ImageUrl", view);
    }

    [Fact]
    public void DetailsGuardsOptionalFieldsBeforeRenderingLabels()
    {
        string view = Read("Views/FlashcardSet/Details.cshtml");

        Assert.Contains("@if (!string.IsNullOrWhiteSpace(card.Pronunciation))", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(card.PartOfSpeech))", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(card.ExampleSentence))", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(card.ExampleMeaning))", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(card.Synonyms))", view);
        Assert.Contains("@if (!string.IsNullOrWhiteSpace(imageSource))", view);
        Assert.DoesNotContain("IsStarred", view);
    }

    private static string Read(string relativePath)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
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
