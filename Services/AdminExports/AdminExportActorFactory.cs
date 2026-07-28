using ltwnc.Services.Auth;

namespace ltwnc.Services.AdminExports;

public static class AdminExportActorFactory
{
    // Dựng actor audit từ current user đã đi qua policy Admin, có fallback để audit không bị thiếu định danh.
    public static AdminExportActor FromCurrentUser(ICurrentUser currentUser)
    {
        // 1. Tính giá trị và lưu vào `actorUserId` để dùng ở bước tiếp theo.
        string actorUserId = "unknown-admin";
        // 2. Kiểm tra `currentUser.UserId != null` để chọn nhánh xử lý phù hợp.
        if (currentUser.UserId != null)
        {
            // 3. Cập nhật `actorUserId` bằng giá trị mới.
            actorUserId = currentUser.UserId;
        }

        // 4. Tính giá trị và lưu vào `actorDisplay` để dùng ở bước tiếp theo.
        string actorDisplay = "Admin";
        // 5. Kiểm tra `currentUser.UserName != null` để chọn nhánh xử lý phù hợp.
        if (currentUser.UserName != null)
        {
            // 6. Cập nhật `actorDisplay` bằng giá trị mới.
            actorDisplay = currentUser.UserName;
        }

        // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminExportActor(actorUserId, actorDisplay);
    }
}
