using ltwnc.Models.ViewModels.FlashcardSet;
using Xunit;

namespace ltwnc.Tests.Services;

public class FlashcardImportContractTests
{
    [Fact]
    public void Import_result_preserves_counts_and_row_errors()
    {
        var result = new FlashcardImportResult
        {
            ImportedCount = 2,
            SkippedCount = 1,
            Errors = new[]
            {
                new FlashcardImportError
                {
                    RowNumber = 4,
                    Reason = "IPA không được để trống."
                }
            }
        };

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(4, result.Errors.Single().RowNumber);
    }
}
