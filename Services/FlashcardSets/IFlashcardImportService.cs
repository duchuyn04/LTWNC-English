using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.FlashcardSets;

public interface IFlashcardImportService
{
    Task<FlashcardImportPreview> PreviewAsync(
        int setId,
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<FlashcardImportResult> ImportAsync(
        int setId,
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
