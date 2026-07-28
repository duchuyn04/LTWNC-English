namespace ltwnc.Services.Auth;

// Tên các claim riêng được lưu trong cookie xác thực của ứng dụng.
public static class AppClaimTypes
{
    // Đánh dấu tài khoản có quyền truy cập khu vực Admin.
    public const string IsAdmin = "IsAdmin";

    // Dùng để vô hiệu hóa cookie cũ khi thông tin bảo mật thay đổi.
    public const string SecurityStamp = "SecurityStamp";
}
