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

    public FlashcardImportService(AppDbContext context, FlashcardFileParserResolver resolver)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_resolver` để các phương thức khác sử dụng.
        _resolver = resolver;
    }

    public Task<FlashcardFileParseResult> ParseAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateFile` để thực hiện bước nghiệp vụ này.
        ValidateFile(file);
        // 2. Trả kết quả từ `ParseFileAsync` cho nơi gọi.
        return ParseFileAsync(file, cancellationToken);
    }

    public async Task<FlashcardImportResult> ImportAsync(int setId, string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateFile` để thực hiện bước nghiệp vụ này.
        ValidateFile(file);

        // 2. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .Include(item => item.Flashcards)
            .FirstOrDefaultAsync(item => item.Id == setId, cancellationToken);
        // 3. Kiểm tra `set is null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set is null || set.UserId != userId)
            // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new FlashcardImportResult();

        // 5. Gọi `ParseFileAsync` và lưu kết quả vào `parsed`.
        FlashcardFileParseResult parsed = await ParseFileAsync(file, cancellationToken);

        // 6. Gọi `ToList` và lưu kết quả vào `errors`.
        var errors = parsed.Errors.ToList();
        // 7. Kiểm tra `!string.IsNullOrWhiteSpace(parsed.FileError)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(parsed.FileError))
        {
            // 8. Gọi `Insert` để thực hiện bước nghiệp vụ này.
            errors.Insert(0, new FlashcardImportError { RowNumber = 0, Reason = parsed.FileError });
            // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new FlashcardImportResult { Errors = errors, SkippedCount = parsed.Errors.Count };
        }

        // 10. Tính giá trị và lưu vào `nextOrder` để dùng ở bước tiếp theo.
        int nextOrder = set.Flashcards.Count == 0 ? 0 : set.Flashcards.Max(card => card.OrderIndex) + 1;
        // 11. Duyệt từng `row` trong `parsed.Rows` để xử lý lần lượt.
        foreach (FlashcardImportRow row in parsed.Rows)
        {
            // 12. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 13. Kiểm tra `parsed.Rows.Count > 0` để chọn nhánh xử lý phù hợp.
        if (parsed.Rows.Count > 0)
        {
            // 14. Cập nhật `set.UpdatedAt` bằng giá trị mới.
            set.UpdatedAt = DateTime.UtcNow;
            // 15. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 16. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new FlashcardImportResult
        {
            ImportedCount = parsed.Rows.Count,
            SkippedCount = parsed.Errors.Count,
            Errors = errors
        };
    }

    private static void ValidateFile(IFormFile file)
    {
        // 1. Kiểm tra `file is null || file.Length == 0` để chọn nhánh xử lý phù hợp.
        if (file is null || file.Length == 0)
            // 2. Dừng xử lý và phát sinh lỗi `new FlashcardImportException("Vui lòng chọn tệp cần nhập.")`.
            throw new FlashcardImportException("Vui lòng chọn tệp cần nhập.");
        // 3. Kiểm tra `file.Length > MaxUploadBytes` để chọn nhánh xử lý phù hợp.
        if (file.Length > MaxUploadBytes)
            // 4. Dừng xử lý và phát sinh lỗi `new FlashcardImportException("Tệp nhập không được vượt quá 10 MB.")`.
            throw new FlashcardImportException("Tệp nhập không được vượt quá 10 MB.");
    }

    private async Task<FlashcardFileParseResult> ParseFileAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `Resolve` và lưu kết quả vào `parser`.
        IFlashcardFileParser parser = _resolver.Resolve(Path.GetExtension(file.FileName));
        // 2. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 3. Gọi `OpenReadStream` và lưu kết quả vào `stream`.
            await using Stream stream = file.OpenReadStream();
            // 4. Trả kết quả từ `ParseAsync` cho nơi gọi.
            return await parser.ParseAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 5. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch (FlashcardImportException)
        {
            // 6. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch (Exception exception)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new FlashcardImportException( "Không thể đọc tệp nhập. Vui lòng kiể...`.
            throw new FlashcardImportException(
                "Không thể đọc tệp nhập. Vui lòng kiểm tra định dạng tệp.",
                exception);
        }
    }
}
