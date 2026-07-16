using System.Text;
using ClosedXML.Excel;
using ltwnc.Services.FlashcardSets;

namespace ltwnc.Tests.Services;

public class FlashcardFileParserTests
{
    private const string RequiredHeaders =
        "Thuật ngữ,Định nghĩa,IPA,Loại từ,Ví dụ tiếng Anh,Nghĩa ví dụ tiếng Việt";

    private readonly CsvFlashcardFileParser _csv = new();

    [Fact]
    public async Task Csv_parser_keeps_quoted_comma_and_newline()
    {
        const string csv = RequiredHeaders + "\n" +
                           "run,chạy,/rʌn/,verb,\"Run, Forest,\nrun!\",Hãy chạy!\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Equal("Run, Forest,\nrun!", Assert.Single(result.Rows).ExampleSentence);
    }

    [Fact]
    public async Task Csv_parser_reads_utf8_vietnamese_headers_and_data()
    {
        const string csv = RequiredHeaders + "\n" +
                           "xin chào,lời chào,/sin˧˧ caːw˨˩/,thán từ,Hello!,Xin chào!\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        FlashcardImportRow row = Assert.Single(result.Rows);
        Assert.Equal("xin chào", row.FrontText);
        Assert.Equal("lời chào", row.BackText);
        Assert.Null(result.FileError);
    }

    [Fact]
    public async Task Xlsx_parser_reads_only_first_worksheet()
    {
        await using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            WriteWorksheet(workbook.AddWorksheet("First"), "first");
            WriteWorksheet(workbook.AddWorksheet("Second"), "second");
            workbook.SaveAs(stream);
        }
        stream.Position = 0;

        FlashcardFileParseResult result = await new XlsxFlashcardFileParser().ParseAsync(stream);

        Assert.Equal("first", Assert.Single(result.Rows).FrontText);
    }

    [Fact]
    public async Task Parser_matches_trimmed_headers_case_insensitively()
    {
        const string csv =
            "  THUẬT NGỮ  , định NGHĨA , ipa , LOẠI TỪ , VÍ DỤ TIẾNG ANH , nghĩa VÍ DỤ tiếng VIỆT \n" +
            "term,definition,/tɜːm/,noun,An example.,Một ví dụ.\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Single(result.Rows);
        Assert.Null(result.FileError);
    }

    [Fact]
    public async Task Missing_required_header_is_a_file_error_and_yields_no_rows()
    {
        const string csv = "Thuật ngữ,Định nghĩa,IPA,Loại từ,Ví dụ tiếng Anh\n" +
                           "run,chạy,/rʌn/,verb,Run!\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Empty(result.Rows);
        Assert.Contains("NGHĨA VÍ DỤ TIẾNG VIỆT", result.MissingRequiredHeaders);
        Assert.NotNull(result.FileError);
    }

    [Fact]
    public async Task Empty_rows_are_ignored_and_original_row_numbers_are_retained()
    {
        const string csv = RequiredHeaders + "\n" +
                           "\n" +
                           "     , , , , , \n" +
                           "term,definition,/tɜːm/,noun,An example.,Một ví dụ.\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Equal(4, Assert.Single(result.Rows).RowNumber);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Required_blank_cell_is_reported_as_a_row_error()
    {
        const string csv = RequiredHeaders + "\n" +
                           "term,definition,   ,noun,An example.,Một ví dụ.\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Empty(result.Rows);
        Assert.Equal(2, Assert.Single(result.Errors).RowNumber);
        Assert.Contains("IPA không được để trống", result.Errors[0].Reason);
    }

    [Fact]
    public async Task Part_of_speech_over_80_characters_is_reported_as_a_row_error()
    {
        string csv = RequiredHeaders + "\n" +
                     $"term,definition,/tɜːm/,{new string('x', 81)},An example.,Một ví dụ.\n";
        await using var stream = CsvStream(csv);

        FlashcardFileParseResult result = await _csv.ParseAsync(stream);

        Assert.Empty(result.Rows);
        Assert.Equal(2, Assert.Single(result.Errors).RowNumber);
        Assert.Contains("80", result.Errors[0].Reason);
    }

    [Fact]
    public void Resolver_selects_supported_parsers_and_rejects_other_extensions()
    {
        var resolver = new FlashcardFileParserResolver(_csv, new XlsxFlashcardFileParser());

        Assert.Same(_csv, resolver.Resolve(".CSV"));
        Assert.IsType<XlsxFlashcardFileParser>(resolver.Resolve(".xlsx"));
        Assert.Throws<FlashcardImportException>(() => resolver.Resolve(".xls"));
    }

    private static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));

    private static void WriteWorksheet(IXLWorksheet worksheet, string frontText)
    {
        string[] headers = RequiredHeaders.Split(',');
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        string[] values = [frontText, "definition", "/test/", "noun", "An example.", "Một ví dụ."];
        for (var column = 0; column < values.Length; column++)
        {
            worksheet.Cell(2, column + 1).Value = values[column];
        }
    }
}
