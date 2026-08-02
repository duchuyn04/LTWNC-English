# Thiết kế chuyển AI sang appsettings và thêm Chain of Responsibility

## Mục tiêu

Chuyển nguồn cấu hình AI từ bảng `AiProviders`/Admin UI sang `appsettings.json`, đồng thời biểu diễn fallback giữa các provider bằng mẫu GoF Chain of Responsibility.

## Phạm vi

- Đọc danh sách provider từ `AiProviders:Providers` bằng options strongly typed.
- Giữ adapter OpenAI-compatible và fallback theo `IsPrimary`, `Priority` rồi đến thứ tự trong mảng cấu hình.
- Xóa `AiProvider` entity, `DbSet`, service quản trị, controller, view model, Razor views, menu và asset UI liên quan.
- Thêm migration mới để xóa bảng `AiProviders`; không sửa hoặc xóa migration cũ đã chạy.
- Giữ `AiOperationLogs`; log dùng tên/model từ cấu hình và để `ProviderId` null.
- Cập nhật Admin Dashboard để chỉ hiển thị trạng thái đã cấu hình, không kiểm tra health từ DB.
- Cập nhật `appsettings.example.json`, local `appsettings.json`, README và tài liệu GoF lên 10 mẫu.

## Cấu hình

`AiProviders:Providers` chứa `Name`, `AdapterType`, `BaseUrl`, `ModelId`, `ApiKey`, `IsEnabled`, `IsPrimary`, `Priority` và `TimeoutSeconds`. API key chỉ đi vào runtime adapter; file mẫu không chứa secret.

## Chain of Responsibility

`AiCompletionRouter` tạo chuỗi handler theo thứ tự provider. Mỗi handler thử một provider, ghi attempt, trả kết quả khi thành công hoặc chuyển lỗi fallback an toàn cho handler kế tiếp. Lỗi hủy request của client và lỗi ngoài danh sách fallback vẫn được giữ nguyên.

## Tiêu chí hoàn thành

- Ứng dụng không còn phụ thuộc runtime vào `AiProviders` hoặc Admin UI AI.
- Migration mới có thể drop bảng `AiProviders` mà không ảnh hưởng `AiOperationLogs`.
- Provider cấu hình trong `appsettings.json` được thử theo thứ tự và fallback đúng khi provider trước thất bại.
- README và `explain/` mô tả cấu hình mới và Chain of Responsibility.
- `dotnet build`, toàn bộ test suite và `git diff --check` đều đạt.
