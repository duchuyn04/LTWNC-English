# LTWNC English - Website Học Tiếng Anh Flashcard

Website học tiếng Anh cộng đồng giống Quizlet, cho phép người dùng tạo bộ thẻ flashcard, chia sẻ và học từ bộ thẻ của nhau.

## Tính năng

### Đã hoàn thành (MVP)

- **Đăng ký / Đăng nhập / Đăng xuất** — xác thực bằng email và mật khẩu
- **Tạo bộ thẻ** — đặt tiêu đề, mô tả, chọn công khai hoặc riêng tư
- **Quản lý bộ thẻ** — sửa, xóa bộ thẻ của mình
- **Thêm / sửa / xóa thẻ** — quản lý từ vựng trong mỗi bộ thẻ, hỗ trợ IPA, loại từ, câu ví dụ, từ đồng nghĩa
- **Ảnh minh họa cho thẻ** — upload ảnh từ máy (JPG/PNG/WebP, tối đa 2MB) hoặc nhập URL; validation cả extension lẫn MIME type
- **Tìm kiếm** — tìm bộ thẻ công khai theo tiêu đề
- **Học flashcard** — lật thẻ, đánh dấu đã biết / chưa biết, theo dõi tiến trình
- **Đánh dấu sao thẻ** — đánh dấu từ quan trọng ngay trong lúc học (AJAX)
- **Bộ lọc học tập** — lọc chỉ học thẻ đã sao hoặc thẻ chưa thuộc; URL filter ghi đè setting đã lưu
- **Cài đặt học flashcard** — tùy chỉnh mặt trước/sau (thuật ngữ, định nghĩa, IPA, ví dụ, ảnh), ẩn/làm mờ ảnh, tự phát âm; lưu server-side theo tài khoản
- **Text-to-speech** — tự phát âm tiếng Anh mặt trước / tiếng Việt mặt sau, chọn giọng và tốc độ
- **Phím tắt học** — Space lật thẻ, ←/→ chuyển thẻ, 1/2 đánh dấu, R đọc lại, Backspace thoát
- **Màn hình hoàn thành** — thống kê đã biết / cần ôn sau mỗi phiên học

### Sắp ra mắt

- Trắc nghiệm (Quiz)
- Viết chính tả (Write)
- Ghép đôi (Match)
- Hồ sơ người dùng với thống kê học tập

## Công nghệ

| Thành phần | Công nghệ |
|------------|-----------|
| Framework | ASP.NET MVC (.NET 10.0) |
| Database | SQL Server |
| ORM | Entity Framework Core 10.0.9 |
| Xác thực | ASP.NET Identity |
| Frontend | Razor Views + jQuery + Bootstrap 5 |
| Icons | Phosphor Icons |
| Kiến trúc | 3 tầng: Controller → Service → Repository |

## Cấu trúc thư mục

```
ltwnc/
├── Controllers/              # Điều phối request/response
│   ├── AccountController.cs
│   ├── HomeController.cs
│   ├── FlashcardSetController.cs
│   └── StudyController.cs
├── Services/                 # Logic nghiệp vụ
│   ├── IAccountService.cs
│   ├── AccountService.cs
│   ├── IFlashcardSetService.cs
│   ├── FlashcardSetService.cs
│   ├── IStudyService.cs
│   └── StudyService.cs
├── Repositories/             # Truy xuất database
│   ├── IFlashcardSetRepository.cs
│   ├── FlashcardSetRepository.cs
│   ├── IFlashcardRepository.cs
│   ├── FlashcardRepository.cs
│   ├── IStudySessionRepository.cs
│   └── StudySessionRepository.cs
├── Models/
│   ├── Entities/             # Entity models (database)
│   └── ViewModels/           # View models (UI)
├── Data/
│   └── AppDbContext.cs       # EF Core DbContext
├── Views/                    # Razor views
│   ├── Shared/
│   ├── Account/
│   ├── Home/
│   ├── FlashcardSet/
│   └── Study/
├── wwwroot/                  # Static files (CSS, JS)
├── Program.cs                # Entry point
└── appsettings.json          # Cấu hình
```

## Yêu cầu hệ thống

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express, Developer, hoặc Standard)
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms) (tùy chọn, để quản lý database)

## Cài đặt

### 1. Clone repository

```bash
git clone https://github.com/duchuyn04/LTWNC-English.git
cd LTWNC-English
```

### 2. Cài đặt .NET EF tools (nếu chưa có)

```bash
dotnet tool install --global dotnet-ef
```

### 3. Restore packages

```bash
dotnet restore
```

## Cấu hình Database

### Bước 1: Xác định tên SQL Server

Mở SQL Server Management Studio (SSMS) hoặc Command Prompt, kiểm tra tên server:

**Cách 1: Dùng SSMS**
- Mở SSMS → Server name hiển thị ở ô "Server name"
- Thường là: `localhost\SQLEXPRESS` hoặc `.\SQLEXPRESS`

**Cách 2: Dùng Command Prompt**
```bash
sqlcmd -L
```

**Cách 3: Dùng PowerShell**
```powershell
Get-Service | Where-Object {$_.Name -like "*SQL*"}
```

### Bước 2: Cấu hình connection string

Mở file `appsettings.json`, tìm mục `ConnectionStrings`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=TÊN_SERVER;Database=LTWNC-English;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Thay `TÊN_SERVER` bằng tên SQL Server của bạn:

| Loại SQL Server | Giá trị |
|-----------------|---------|
| SQL Server Express (mặc định) | `localhost\SQLEXPRESS` hoặc `.\SQLEXPRESS` |
| SQL Server LocalDB | `(localdb)\mssqllocaldb` |
| SQL Server mặc định | `localhost` hoặc `.` |
| SQL Server đặt tên | `localhost\TÊN_INSTANCE` |

**Ví dụ:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=LTWNC-English;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

> **Lưu ý:** Trong JSON, dấu `\` phải được escape thành `\\`.

### Bước 3: Chạy Migration

Tạo database và các bảng:

```bash
dotnet ef database update
```

Nếu muốn tạo migration mới (sau khi thay đổi model):

```bash
dotnet ef migrations add TênMigration
dotnet ef database update
```

### Bước 4: Kiểm tra database

Mở SSMS, kết nối đến server của bạn. Bạn sẽ thấy database `LTWNC-English` với các bảng:

- `AspNetUsers` — người dùng
- `AspNetRoles` — vai trò
- `FlashcardSets` — bộ thẻ
- `Flashcards` — thẻ
- `StudySessions` — phiên học
- `UserProgresses` — tiến trình học

## Chạy ứng dụng

```bash
dotnet run
```

Mở trình duyệt truy cập: **http://localhost:5000**

## Sử dụng

1. **Đăng ký** — tạo tài khoản mới
2. **Đăng nhập** — đăng nhập bằng email và mật khẩu
3. **Tạo bộ thẻ** — nhấn "Tạo bộ thẻ mới" trên trang chủ
4. **Thêm thẻ** — thêm từ vựng tiếng Anh và nghĩa tiếng Việt
5. **Học** — chọn bộ thẻ → chọn chế độ Flashcard → lật thẻ và đánh dấu đã biết/chưa biết

## API Routes

| Route | Method | Mô tả |
|-------|--------|-------|
| `/` | GET | Trang chủ |
| `/Account/Register` | GET/POST | Đăng ký |
| `/Account/Login` | GET/POST | Đăng nhập |
| `/Account/Logout` | POST | Đăng xuất |
| `/Set` | GET | Bộ thẻ của tôi |
| `/Set/Create` | GET/POST | Tạo bộ thẻ |
| `/Set/{id}` | GET | Chi tiết bộ thẻ |
| `/Set/{id}/Edit` | GET/POST | Sửa bộ thẻ |
| `/Set/{id}/Delete` | POST | Xóa bộ thẻ |
| `/Set/{id}/AddCard` | POST | Thêm thẻ vào bộ |
| `/Set/{id}/EditCard/{cardId}` | POST | Sửa thẻ |
| `/Set/{id}/DeleteCard/{cardId}` | POST | Xóa thẻ |
| `/Study/{setId}` | GET | Chọn chế độ học |
| `/Study/{setId}/Flashcard` | GET | Học flashcard (query: `starredOnly`, `unlearnedOnly`, `index`) |
| `/Study/{setId}/Flashcard/Mark` | POST | Đánh dấu đã biết / chưa biết |
| `/Study/{setId}/Flashcard/{cardId}/ToggleStar` | POST | Đánh dấu sao thẻ (AJAX) |
| `/Study/{setId}/Complete` | POST | Hoàn thành phiên học |
| `/Study/Settings` | POST | Lưu cài đặt học (AJAX) |

## Xử lý lỗi thường gặp

### Lỗi kết nối database

```
A connection was successfully established with the server, but then an error occurred during the login process.
```

**Giải pháp:** Thêm `TrustServerCertificate=True` vào connection string.

### Lỗi migration

```
Introducing FOREIGN KEY constraint may cause cycles or multiple cascade paths.
```

**Giải pháp:** Dự án đã xử lý lỗi này. Nếu gặp lại, chạy lại migration:

```bash
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Lỗi port đã sử dụng

```
Failed to bind to address https://127.0.0.1:5000: address already in use.
```

**Giải pháp:** Đổi port trong `Properties/launchSettings.json` hoặc tắt ứng dụng đang sử dụng port 5000.

## License

Dự án học tập — LTWNC.
