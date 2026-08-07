namespace ltwnc.Tests.Views;

public sealed class ReviewFocusDeckViewTests
{
    private static string Root
    {
        get
        {
            string? configuredRoot = Environment.GetEnvironmentVariable("REVIEW_REPO_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot)
                && File.Exists(Path.Combine(configuredRoot, "ltwnc.csproj")))
            {
                return configuredRoot;
            }

            foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                DirectoryInfo? directory = new(startPath);
                while (directory is not null)
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

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void ReviewFlow_UsesOneFocusDeckShell()
    {
        string index = Read("Views/Review/Index.cshtml");
        string session = Read("Views/Review/Session.cshtml");
        string result = Read("Views/Review/Result.cshtml");

        foreach (string view in new[] { index, session, result })
        {
            Assert.Contains("~/css/review.css", view);
            Assert.Contains("ViewData[\"HideLayoutChrome\"] = true", view);
            Assert.Contains("review-focus", view);
        }

        Assert.Contains("action=\"/Review/Set/@Model.SetId/Start\"", index);
        Assert.Contains("@Html.AntiForgeryToken()", index);
        Assert.Contains("TempData[\"Message\"]", index);
        Assert.Contains("role=\"status\"", index);
        Assert.Contains("style=\"width: @(progressPercent)%\"", session);
        Assert.Contains("style=\"width: @(progressPercent)%\"", result);
    }

    [Fact]
    public void Session_PreservesReviewPostsAndKeyboardControls()
    {
        string view = Read("Views/Review/Session.cshtml");

        Assert.Contains("action=\"/Review/@Model.SessionId/End\"", view);
        Assert.Contains("data-confirm=\"Kết thúc sớm lượt ôn?\"", view);
        Assert.Contains("action=\"/Review/@Model.SessionId/Rate\"", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("name=\"flashcardId\"", view);
        Assert.Contains("name=\"answerRevealed\"", view);
        Assert.Contains("name=\"rating\" value=\"Again\"", view);
        Assert.Contains("name=\"rating\" value=\"Hard\"", view);
        Assert.Contains("name=\"rating\" value=\"Good\"", view);
        Assert.Contains("name=\"rating\" value=\"Easy\"", view);
        Assert.Contains("event.code === 'Space'", view);
        Assert.Contains("['1', '2', '3', '4']", view);
        Assert.Contains("requestSubmit(button)", view);
        Assert.Contains("input, textarea, select, [contenteditable=\"true\"]", view);
        Assert.Contains("ShowFrontTerm", view);
        Assert.Contains("ShowBackExample", view);
    }

    [Fact]
    public void Session_PreservesAllStudySettingBranchesAndAnswerImageScope()
    {
        string view = Read("Views/Review/Session.cshtml");

        Assert.Contains("ShowFrontTerm", view);
        Assert.Contains("ShowFrontDefinition", view);
        Assert.Contains("ShowFrontIpa", view);
        Assert.Contains("ShowFrontImage", view);
        Assert.Contains("ShowBackTerm", view);
        Assert.Contains("ShowBackDefinition", view);
        Assert.Contains("ShowBackIpa", view);
        Assert.Contains("ShowBackExample", view);
        Assert.Contains("ShowBackImage", view);
        Assert.Contains("HideImage", view);
        Assert.Contains("BlurImage", view);

        int answerPanelIndex = view.IndexOf("id=\"review-answer\"", StringComparison.Ordinal);
        int backImageBranchIndex = view.IndexOf("Model.Settings.ShowBackImage", StringComparison.Ordinal);

        Assert.True(answerPanelIndex >= 0);
        Assert.True(backImageBranchIndex > answerPanelIndex);
    }

    [Fact]
    public void Session_SelectsUnratedCardAndGuardsZeroProgress()
    {
        string view = Read("Views/Review/Session.cshtml");

        Assert.Contains("Model.Cards.FirstOrDefault(value => !value.IsRated)", view);
        Assert.Contains("?? Model.Cards.FirstOrDefault()", view);
        Assert.Contains("int currentNumber = Model.TotalCards == 0", view);
        Assert.Contains("int progressPercent = Model.TotalCards == 0", view);
        Assert.Contains("Model.RatedCards * 100d / Model.TotalCards", view);
    }

    [Fact]
    public void Session_RatingButtonsUsePreviewDelaysAndStartDisabled()
    {
        string view = Read("Views/Review/Session.cshtml");

        Assert.Contains("@againPreview?.DelayLabel", view);
        Assert.Contains("@hardPreview?.DelayLabel", view);
        Assert.Contains("@goodPreview?.DelayLabel", view);
        Assert.Contains("@easyPreview?.DelayLabel", view);
        Assert.Contains("@againPreview?.NextReviewLabel", view);
        Assert.Contains("@hardPreview?.NextReviewLabel", view);
        Assert.Contains("@goodPreview?.NextReviewLabel", view);
        Assert.Contains("@easyPreview?.NextReviewLabel", view);
        Assert.Contains("data-shortcut=\"1\" disabled", view);
        Assert.Contains("data-shortcut=\"2\" disabled", view);
        Assert.Contains("data-shortcut=\"3\" disabled", view);
        Assert.Contains("data-shortcut=\"4\" disabled", view);
        Assert.Contains("id=\"review-rating-form\"", view);
        Assert.Contains("if (rateForm) rateForm.hidden = false", view);
        Assert.Contains("root.dataset.state = 'answer'", view);
    }

    [Fact]
    public void Result_RendersTerminalStateCountsCardsAndNextReviewCta()
    {
        string view = Read("Views/Review/Result.cshtml");

        Assert.Contains("Model.IsEnded ?", view);
        Assert.Contains("Model.Cards.Count(card => card.Rating == ReviewRating.Again)", view);
        Assert.Contains("Model.Cards.Count(card => card.Rating == ReviewRating.Hard)", view);
        Assert.Contains("Model.Cards.Count(card => card.Rating == ReviewRating.Good)", view);
        Assert.Contains("Model.Cards.Count(card => card.Rating == ReviewRating.Easy)", view);
        Assert.Contains("@StageLabel(card.Stage)", view);
        Assert.Contains("@RatingLabel(card.Rating)", view);
        Assert.Contains("href=\"/Review\"", view);
    }

    [Fact]
    public void FocusDeckStyles_AreResponsiveAndAccessible()
    {
        string css = Read("wwwroot/css/review.css");

        Assert.Contains("width: min(100%, 56.25rem)", css);
        Assert.Contains("grid-template-columns: repeat(4", css);
        Assert.Contains("@media (max-width: 40rem)", css);
        Assert.Contains("grid-template-columns: repeat(2", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("prefers-reduced-motion: reduce", css);
        Assert.DoesNotContain(".review-prototype", css);
    }

    [Fact]
    public void PrototypeRouteAndAssetsAreRemoved()
    {
        string controller = Read("Controllers/ReviewController.cs");

        Assert.DoesNotContain("/Review/Prototype", controller);
        Assert.DoesNotContain("BuildPrototypeSession", controller);
        Assert.False(File.Exists(Path.Combine(Root, "Views/Review/Prototype.cshtml")));
        Assert.False(File.Exists(Path.Combine(Root, "wwwroot/css/review-prototype.css")));
    }

    [Fact]
    public void Typography_UsesOnlyBeVietnamProAcrossSharedLayouts()
    {
        string[] assets =
        {
            Read("wwwroot/css/site.css"),
            Read("wwwroot/css/auth.css"),
            Read("wwwroot/css/not-found.css"),
            Read("wwwroot/css/admin/tokens.css"),
            Read("Views/Shared/_Layout.cshtml"),
            Read("Views/Shared/_AuthLayout.cshtml"),
            Read("Views/Shared/NotFound.cshtml"),
            Read("Areas/Admin/Views/Shared/_AdminLayout.cshtml")
        };

        foreach (string asset in assets[..4])
        {
            Assert.DoesNotContain("Newsreader", asset, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Georgia", asset, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Be Vietnam Pro", assets[0]);
        Assert.Contains("Be Vietnam Pro", assets[3]);

        foreach (string asset in assets[4..])
        {
            Assert.Contains("Be+Vietnam+Pro", asset);
            Assert.DoesNotContain("Newsreader", asset, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Georgia", asset, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("--font-display: var(--font-body)", assets[0]);
        Assert.Contains("--auth-display: var(--font-display)", assets[1]);
    }
}
