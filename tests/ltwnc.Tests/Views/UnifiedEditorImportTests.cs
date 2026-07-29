namespace ltwnc.Tests.Views;

public class UnifiedEditorImportTests
{
    private static readonly string View = ReadFile("Views", "FlashcardSet", "Editor.cshtml");
    private static readonly string Script = ReadFile("wwwroot", "js", "unified-editor.js");
    private static readonly string Styles = ReadFile("wwwroot", "css", "unified-editor.css");

    [Fact]
    public void Editor_UsesWorkbenchSidebarAndScrollableCardWorkspace()
    {
        Assert.Contains("class=\"editor-sidebar\"", View);
        Assert.Contains("class=\"editor-workspace\"", View);
        Assert.Contains("class=\"editor-scroll-region\"", View);
        Assert.Contains("id=\"card-search\"", View);
        Assert.Contains("grid-template-columns: minmax(17rem, 20rem) minmax(0, 1fr)", Styles);
        Assert.Contains("overflow-y: auto", Styles);
        Assert.Contains("@media (min-width: 64rem)", Styles);
    }

    [Fact]
    public void Editor_SearchAndStarFilterOperateOnRenderedCards()
    {
        Assert.Contains("function applyCardFilters()", Script);
        Assert.Contains("cardSearch.addEventListener('input', applyCardFilters)", Script);
        Assert.Contains("cardFilter.addEventListener('change', applyCardFilters)", Script);
        Assert.Contains("card.hidden = !matchesQuery || !matchesFilter", Script);
    }

    [Fact]
    public void Editor_UsesCsvAndXlsxFilePickerInsteadOfPasteTextarea()
    {
        Assert.Contains("id=\"import-file\"", View);
        Assert.Contains("type=\"file\"", View);
        Assert.Contains("accept=\".csv,.xlsx", View);
        Assert.Contains("Chọn file hoặc kéo thả vào đây", View);
        Assert.DoesNotContain("id=\"import-text\"", View);
        Assert.DoesNotContain("id=\"import-delimiter\"", View);
    }

    [Fact]
    public void Editor_ExplainsFormatReplaceImpactAndExposesAccessibleFeedback()
    {
        Assert.Contains("aria-modal=\"true\"", View);
        Assert.Contains("aria-labelledby=\"import-modal-title\"", View);
        Assert.Contains("role=\"status\"", View);
        Assert.Contains("tiến độ học liên quan sẽ bị xóa", View);
        Assert.Contains("@Html.AntiForgeryToken()", View);
    }

    [Fact]
    public void ImportScript_UploadsMultipartFileAndReplaceFlag()
    {
        Assert.Contains("new FormData()", Script);
        Assert.Contains("formData.append('file'", Script);
        Assert.Contains("formData.append('replaceAll'", Script);
        Assert.Contains("`/Set/${currentSetId}/ImportFile`", Script);
        Assert.Contains("'X-Requested-With': 'XMLHttpRequest'", Script);
    }

    [Fact]
    public void ImportScript_ValidatesFileAndRendersServerErrorsSafely()
    {
        Assert.Contains("allowedImportExtensions = ['.csv', '.xlsx']", Script);
        Assert.Contains("maxImportBytes = 10 * 1024 * 1024", Script);
        Assert.Contains("importDropzone.addEventListener('drop'", Script);
        Assert.Contains("item.textContent =", Script);
        Assert.Contains("result.omittedErrorCount", Script);
    }

    [Fact]
    public void ImportScript_PopulatesAllParsedCardFieldsAndPreservesImageUrl()
    {
        Assert.Contains("const importedCard = appendImportedCard(data)", Script);
        Assert.Contains("firstImportedCard ??= importedCard", Script);
        Assert.DoesNotContain("firstImportedCard ??= appendImportedCard(data)", Script);
        Assert.Contains("card.dataset.imageUrl = data.imageUrl || ''", Script);
        Assert.Contains("'.input-pronunciation').value = data.pronunciation", Script);
        Assert.Contains("'.input-part-of-speech').value = data.partOfSpeech", Script);
        Assert.Contains("'.input-example-sentence').value = data.exampleSentence", Script);
        Assert.Contains("'.input-example-meaning').value = data.exampleMeaning", Script);
        Assert.Contains("'.input-synonyms').value = data.synonyms", Script);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        return string.Empty;
    }
}
