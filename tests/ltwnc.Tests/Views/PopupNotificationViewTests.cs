namespace ltwnc.Tests.Views;

public sealed class PopupNotificationViewTests
{
    [Fact]
    public void MainAndAdminLayoutsLoadSharedPopupAssets()
    {
        string mainLayout = Read("Views/Shared/_Layout.cshtml");
        string adminLayout = Read("Areas/Admin/Views/Shared/_AdminLayout.cshtml");

        Assert.Contains("~/css/popup.css", mainLayout);
        Assert.Contains("~/js/site.js", mainLayout);
        Assert.Contains("~/css/popup.css", adminLayout);
        Assert.Contains("/js/site.js", adminLayout);
    }

    [Theory]
    [InlineData("Views/Achievements/Index.cshtml")]
    [InlineData("Views/EnglishMission/SelectTopic.cshtml")]
    [InlineData("Views/EnglishMission/Chat.cshtml")]
    [InlineData("Views/FlashcardSet/Edit.cshtml")]
    [InlineData("Views/FlashcardSet/Details.cshtml")]
    [InlineData("Views/Profile/Edit.cshtml")]
    [InlineData("Views/Review/Index.cshtml")]
    [InlineData("Views/Study/Flashcard.cshtml")]
    [InlineData("Views/Study/Index.cshtml")]
    [InlineData("Views/Study/Quiz.cshtml")]
    [InlineData("Views/Study/QuizResult.cshtml")]
    [InlineData("Areas/Admin/Views/AiProviders/Index.cshtml")]
    [InlineData("Areas/Admin/Views/ContentReports/Index.cshtml")]
    [InlineData("Areas/Admin/Views/Dashboard/Index.cshtml")]
    [InlineData("Areas/Admin/Views/Users/Details.cshtml")]
    public void TransientServerMessagesOptIntoPopupBehavior(string relativePath)
    {
        Assert.Contains("data-popup=", Read(relativePath));
    }

    [Fact]
    public void PopupScriptSupportsCloseAutoDismissAndDynamicMessages()
    {
        string script = Read("wwwroot/js/site.js");

        Assert.Contains("app-popup-stack", script);
        Assert.Contains("app-popup__close", script);
        Assert.Contains("window.showAppPopup", script);
        Assert.Contains("MutationObserver", script);
    }

    [Fact]
    public void DynamicBatchFeedbackUsesSharedPopupBehavior()
    {
        string script = Read("wwwroot/js/flashcard-editor.js");

        Assert.Contains("alert.dataset.popup", script);
        Assert.Contains("alert.dataset.popupPersist", script);
    }

    [Fact]
    public void NativeConfirmDialogsAreReplacedBySharedModal()
    {
        string siteScript = Read("wwwroot/js/site.js");
        string setIndex = Read("Views/FlashcardSet/Index.cshtml");
        string setDetails = Read("Views/FlashcardSet/Details.cshtml");
        string editorScript = Read("wwwroot/js/unified-editor.js");

        Assert.Contains("window.appConfirm", siteScript);
        Assert.Contains("app-confirm-backdrop", siteScript);
        Assert.Contains("data-confirm=", setIndex);
        Assert.Contains("data-confirm=", setDetails);
        Assert.DoesNotContain("confirm(", setIndex);
        Assert.DoesNotContain("confirm(", setDetails);
        Assert.DoesNotContain("confirm(", editorScript);
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
