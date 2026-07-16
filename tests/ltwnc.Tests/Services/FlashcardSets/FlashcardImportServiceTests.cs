using System.Text;
using ClosedXML.Excel;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

public class FlashcardImportServiceTests : IDisposable
{
    private const string Headers = "Thuật ngữ,ĐỊNH NGHĨA,IPA,LOẠI TỪ,VÍ DỤ TIẾNG ANH,NGHĨA VÍ DỤ TIẾNG VIỆT,TỪ ĐỒNG NGHĨA,URL ẢNH";
    private readonly AppDbContext _context;
    private readonly FlashcardImportService _service;
    private readonly int _setId;

    public FlashcardImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new AppDbContext(options);
        _service = new FlashcardImportService(_context, new FlashcardFileParserResolver(new CsvFlashcardFileParser(), new XlsxFlashcardFileParser()));
        var set = new FlashcardSet { Title = "Set", UserId = "owner" };
        _context.FlashcardSets.Add(set);
        _context.SaveChanges();
        _setId = set.Id;
        _context.Flashcards.Add(new Flashcard { FlashcardSetId = _setId, FrontText = "existing", BackText = "x", Pronunciation = "/x/", PartOfSpeech = "noun", ExampleSentence = "x", ExampleMeaning = "x", OrderIndex = 7 });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Valid_csv_imports_cards_after_current_max_order()
    {
        var result = await _service.ImportAsync(_setId, "owner", FormFile(Headers + "\nrun,cháº¡y,/r/,verb,Run!,Cháº¡y!\njump,nháº£y,/dÊ’/,verb,Jump!,Nháº£y!\n", "cards.csv"));
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(new[] { 7, 8, 9 }, await _context.Flashcards.Where(c => c.FlashcardSetId == _setId).OrderBy(c => c.OrderIndex).Select(c => c.OrderIndex).ToArrayAsync());
    }

    [Fact]
    public async Task Mixed_rows_import_valid_cards_and_report_original_row()
    {
        var csv = Headers + "\nrun,cháº¡y,/r/,verb,Run!,Cháº¡y!\ninvalid,,/i/,noun,Example,Meaning\n";
        var result = await _service.ImportAsync(_setId, "owner", FormFile(csv, "cards.csv"));
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, result.Errors.Single().RowNumber);
        Assert.Equal(new[] { 7, 8 }, await _context.Flashcards.Where(c => c.FlashcardSetId == _setId).OrderBy(c => c.OrderIndex).Select(c => c.OrderIndex).ToArrayAsync());
    }

    [Fact]
    public async Task Non_owner_imports_zero_cards()
    {
        var result = await _service.ImportAsync(_setId, "other", FormFile(Headers + "\nrun,x,/r/,verb,Run!,X!\n", "cards.csv"));
        Assert.Equal(0, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
    }

    [Fact]
    public async Task Missing_header_imports_zero_and_reports_file_error()
    {
        var result = await _service.ImportAsync(_setId, "owner", FormFile("wrong,header\nvalue,value\n", "cards.csv"));
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors.Single().RowNumber);
        Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
    }

    [Fact]
    public async Task Xlsx_imports_first_sheet()
    {
        await using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("First");
            var headers = Headers.Split(',');
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, 1).Value = "xlsx"; sheet.Cell(2, 2).Value = "excel"; sheet.Cell(2, 3).Value = "/x/"; sheet.Cell(2, 4).Value = "noun"; sheet.Cell(2, 5).Value = "Example"; sheet.Cell(2, 6).Value = "Meaning";
            workbook.AddWorksheet("Second").Cell(2, 1).Value = "ignored";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        var result = await _service.ImportAsync(_setId, "owner", new FormFile(stream, 0, stream.Length, "file", "cards.xlsx"));
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("xlsx", (await _context.Flashcards.SingleAsync(c => c.FrontText == "xlsx")).FrontText);
    }

    [Fact]
    public async Task Corrupt_xlsx_is_reported_as_typed_import_exception()
    {
        var bytes = Encoding.UTF8.GetBytes("this is not an xlsx file");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "corrupt.xlsx");

        var exception = await Assert.ThrowsAsync<FlashcardImportException>(
            () => _service.ImportAsync(_setId, "owner", file));

        Assert.Contains("Không thể đọc tệp nhập", exception.Message);
        Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected_before_parsing()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var file = new FormFile(stream, 0, stream.Length, "file", "cards.txt");

        await Assert.ThrowsAsync<FlashcardImportException>(
            () => _service.ImportAsync(_setId, "owner", file));
        Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
    }

    [Fact]
    public async Task Empty_file_is_rejected_without_mutation()
    {
        await using var stream = new MemoryStream();
        var file = new FormFile(stream, 0, 0, "file", "cards.csv");

        await Assert.ThrowsAsync<FlashcardImportException>(
            () => _service.ImportAsync(_setId, "owner", file));
        Assert.Equal(1, await _context.Flashcards.CountAsync(c => c.FlashcardSetId == _setId));
    }

    private static IFormFile FormFile(string content, string name) => new FormFile(new MemoryStream(Encoding.UTF8.GetBytes(content)), 0, Encoding.UTF8.GetByteCount(content), "file", name);
}
