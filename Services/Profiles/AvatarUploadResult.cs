namespace ltwnc.Services.Profiles;

// Kết quả thay ảnh đại diện sau khi kiểm tra định dạng và lưu tệp.
public sealed class AvatarUploadResult
{
    // Cho biết thao tác có hoàn tất thành công hay không.
    public bool Succeeded { get; init; }
    // Đường dẫn ảnh mới khi thao tác thành công.
    public string? AvatarPath { get; init; }
    // Thông báo an toàn để hiển thị khi thao tác thất bại.
    public string? Error { get; init; }
}
