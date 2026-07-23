using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.FlashcardSets;

public sealed class FlashcardImportService : IFlashcardImportService
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly AppDbContext _context;
    private readonly FlashcardFileParserResolver _resolver;

    public FlashcardImportService(
        AppDbContext context,
        FlashcardFileParserResolver resolver)
    {
        _context = context;
        _resolver = resolver;
    }

    public async Task<FlashcardImportPreview> PreviewAsync(
        int setId,
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        bool ownsSet = await _context.FlashcardSets
            .AnyAsync(
                set => set.Id == setId && set.UserId == userId,
                cancellationToken);
        if (!ownsSet)
        {
            throw new UnauthorizedAccessException(
                "Không có quyền nhập thẻ vào bộ này.");
        }

        FlashcardFileParseResult parsed =
            await ValidateAndParseAsync(file, cancellationToken);

        return new FlashcardImportPreview
        {
            Rows = parsed.Rows,
            Errors = BuildErrors(parsed)
        };
    }

    public async Task<FlashcardImportResult> ImportAsync(
        int setId,
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .Include(item => item.Flashcards)
            .FirstOrDefaultAsync(item => item.Id == setId, cancellationToken);
        if (set is null || set.UserId != userId)
        {
            return new FlashcardImportResult();
        }

        FlashcardFileParseResult parsed =
            await ValidateAndParseAsync(file, cancellationToken);
        IReadOnlyList<FlashcardImportError> errors = BuildErrors(parsed);
        if (!string.IsNullOrWhiteSpace(parsed.FileError))
        {
            return new FlashcardImportResult
            {
                Errors = errors,
                SkippedCount = parsed.Errors.Count
            };
        }

        int nextOrder = set.Flashcards.Count == 0
            ? 0
            : set.Flashcards.Max(card => card.OrderIndex) + 1;
        foreach (FlashcardImportRow row in parsed.Rows)
        {
            set.Flashcards.Add(new Flashcard
            {
                FlashcardSetId = set.Id,
                FrontText = row.FrontText,
                BackText = row.BackText,
                Pronunciation = row.Pronunciation,
                PartOfSpeech = row.PartOfSpeech,
                ExampleSentence = row.ExampleSentence,
                ExampleMeaning = row.ExampleMeaning,
                Synonyms = row.Synonyms,
                ImageUrl = row.ImageUrl,
                UploadedImagePath = null,
                IsStarred = false,
                OrderIndex = nextOrder++
            });
        }

        if (parsed.Rows.Count > 0)
        {
            set.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new FlashcardImportResult
        {
            ImportedCount = parsed.Rows.Count,
            SkippedCount = parsed.Errors.Count,
            Errors = errors
        };
    }

    private async Task<FlashcardFileParseResult> ValidateAndParseAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new FlashcardImportException("Vui lòng chọn tệp cần nhập.");
        }

        if (file.Length > MaxUploadBytes)
        {
            throw new FlashcardImportException(
                "Tệp nhập không được vượt quá 10 MB.");
        }

        IFlashcardFileParser parser =
            _resolver.Resolve(Path.GetExtension(file.FileName));
        try
        {
            await using Stream stream = file.OpenReadStream();
            return await parser.ParseAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FlashcardImportException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FlashcardImportException(
                "Không thể đọc tệp nhập. Vui lòng kiểm tra định dạng tệp.",
                exception);
        }
    }

    private static IReadOnlyList<FlashcardImportError> BuildErrors(
        FlashcardFileParseResult parsed)
    {
        var errors = parsed.Errors.ToList();
        if (!string.IsNullOrWhiteSpace(parsed.FileError))
        {
            errors.Insert(
                0,
                new FlashcardImportError
                {
                    RowNumber = 0,
                    Reason = parsed.FileError
                });
        }

        return errors;
    }
}
