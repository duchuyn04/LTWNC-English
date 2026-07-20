using System.Text;

namespace ltwnc.Services.AdminExports;

public static class SafeCsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly char[] DangerousFormulaPrefixes = ['=', '+', '-', '@'];

    // Tạo nội dung CSV UTF-8 có BOM, header rõ ràng và mọi ô đều được quote để parser đọc ổn định.
    public static byte[] Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, headers);
        foreach (IReadOnlyList<string?> row in rows)
        {
            AppendRow(builder, row);
        }

        byte[] preamble = Utf8WithBom.GetPreamble();
        byte[] body = Utf8WithBom.GetBytes(builder.ToString());
        byte[] content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);
        return content;
    }

    // Ghép một dòng CSV theo RFC 4180: quote toàn bộ field và escape dấu quote bằng cặp quote.
    private static void AppendRow(StringBuilder builder, IReadOnlyList<string?> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(EscapeCell(NeutralizeFormula(values[index])));
            builder.Append('"');
        }

        builder.Append("\r\n");
    }

    // Vô hiệu hóa công thức spreadsheet trước khi escape để dữ liệu không tự chạy khi mở bằng Excel.
    private static string NeutralizeFormula(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Bỏ qua whitespace đầu chuỗi để công thức không lọt qua bằng cách thêm khoảng trắng hoặc xuống dòng.
        int firstMeaningfulIndex = FindFirstMeaningfulCharacterIndex(value);
        if (firstMeaningfulIndex >= value.Length)
        {
            return value;
        }

        if (DangerousFormulaPrefixes.Contains(value[firstMeaningfulIndex]))
        {
            return "'" + value;
        }

        return value;
    }

    // Tìm ký tự đầu tiên có ý nghĩa trong ô CSV để kiểm tra prefix công thức spreadsheet.
    private static int FindFirstMeaningfulCharacterIndex(string value)
    {
        int index = 0;
        while (index < value.Length)
        {
            char current = value[index];
            if (!char.IsWhiteSpace(current))
            {
                break;
            }

            index++;
        }

        return index;
    }

    // Escape quote trong từng ô CSV bằng cách nhân đôi dấu quote.
    private static string EscapeCell(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}
