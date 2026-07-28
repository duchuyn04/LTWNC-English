using System.Text;

namespace ltwnc.Services.AdminExports;

public static class SafeCsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly char[] DangerousFormulaPrefixes = ['=', '+', '-', '@'];

    // Tạo nội dung CSV UTF-8 có BOM, header rõ ràng và mọi ô đều được quote để parser đọc ổn định.
    public static byte[] Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        // 1. Khởi tạo `builder` với dữ liệu ban đầu cần thiết.
        var builder = new StringBuilder();
        // 2. Gọi `AppendRow` để thực hiện bước nghiệp vụ này.
        AppendRow(builder, headers);
        // 3. Duyệt từng `row` trong `rows` để xử lý lần lượt.
        foreach (IReadOnlyList<string?> row in rows)
        {
            // 4. Gọi `AppendRow` để thực hiện bước nghiệp vụ này.
            AppendRow(builder, row);
        }

        // 5. Gọi `GetPreamble` và lưu kết quả vào `preamble`.
        byte[] preamble = Utf8WithBom.GetPreamble();
        // 6. Gọi `GetBytes` và lưu kết quả vào `body`.
        byte[] body = Utf8WithBom.GetBytes(builder.ToString());
        // 7. Khởi tạo `content` với dữ liệu ban đầu cần thiết.
        byte[] content = new byte[preamble.Length + body.Length];
        // 8. Gọi `BlockCopy` để thực hiện bước nghiệp vụ này.
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        // 9. Gọi `BlockCopy` để thực hiện bước nghiệp vụ này.
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);
        // 10. Trả `content` cho nơi gọi.
        return content;
    }

    // Ghép một dòng CSV theo RFC 4180: quote toàn bộ field và escape dấu quote bằng cặp quote.
    private static void AppendRow(StringBuilder builder, IReadOnlyList<string?> values)
    {
        // 1. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int index = 0; index < values.Count; index++)
        {
            // 2. Kiểm tra `index > 0` để chọn nhánh xử lý phù hợp.
            if (index > 0)
            {
                // 3. Gọi `Append` để thực hiện bước nghiệp vụ này.
                builder.Append(',');
            }

            // 4. Gọi `Append` để thực hiện bước nghiệp vụ này.
            builder.Append('"');
            // 5. Gọi `Append` để thực hiện bước nghiệp vụ này.
            builder.Append(EscapeCell(NeutralizeFormula(values[index])));
            // 6. Gọi `Append` để thực hiện bước nghiệp vụ này.
            builder.Append('"');
        }

        // 7. Gọi `Append` để thực hiện bước nghiệp vụ này.
        builder.Append("\r\n");
    }

    // Vô hiệu hóa công thức spreadsheet trước khi escape để dữ liệu không tự chạy khi mở bằng Excel.
    private static string NeutralizeFormula(string? value)
    {
        // 1. Kiểm tra `string.IsNullOrEmpty(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrEmpty(value))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // Bỏ qua whitespace đầu chuỗi để công thức không lọt qua bằng cách thêm khoảng trắng hoặc xuống dòng.
        // 3. Gọi `FindFirstMeaningfulCharacterIndex` và lưu kết quả vào `firstMeaningfulIndex`.
        int firstMeaningfulIndex = FindFirstMeaningfulCharacterIndex(value);
        // 4. Kiểm tra `firstMeaningfulIndex >= value.Length` để chọn nhánh xử lý phù hợp.
        if (firstMeaningfulIndex >= value.Length)
        {
            // 5. Trả `value` cho nơi gọi.
            return value;
        }

        // 6. Kiểm tra `DangerousFormulaPrefixes.Contains(value[firstMeaningfulIndex])` để chọn nhánh xử lý phù hợp.
        if (DangerousFormulaPrefixes.Contains(value[firstMeaningfulIndex]))
        {
            // 7. Trả `"'" + value` cho nơi gọi.
            return "'" + value;
        }

        // 8. Trả `value` cho nơi gọi.
        return value;
    }

    // Tìm ký tự đầu tiên có ý nghĩa trong ô CSV để kiểm tra prefix công thức spreadsheet.
    private static int FindFirstMeaningfulCharacterIndex(string value)
    {
        // 1. Tính giá trị và lưu vào `index` để dùng ở bước tiếp theo.
        int index = 0;
        // 2. Tiếp tục lặp khi `index < value.Length` còn đúng.
        while (index < value.Length)
        {
            // 3. Tính giá trị và lưu vào `current` để dùng ở bước tiếp theo.
            char current = value[index];
            // 4. Kiểm tra `!char.IsWhiteSpace(current)` để chọn nhánh xử lý phù hợp.
            if (!char.IsWhiteSpace(current))
            {
                // 5. Thoát khỏi vòng lặp hoặc nhánh xử lý hiện tại.
                break;
            }

            // 6. Cập nhật bộ đếm hoặc trạng thái `index`.
            index++;
        }

        // 7. Trả `index` cho nơi gọi.
        return index;
    }

    // Escape quote trong từng ô CSV bằng cách nhân đôi dấu quote.
    private static string EscapeCell(string value)
    {
        // 1. Trả kết quả từ `Replace` cho nơi gọi.
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}
