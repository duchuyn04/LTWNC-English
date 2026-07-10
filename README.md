# LTWNC English

Ứng dụng học từ vựng tiếng Anh bằng flashcard. Xây bằng ASP.NET Core MVC. Ngườ dùng tạo bộ thẻ, thêm từ vựng có IPA, ví dụ và ảnh, chia sẻ công khai, rồi học theo tiến độ cá nhân.

## Tính năng chính

- Đăng ký, đăng nhập, đăng xuất bằng ASP.NET Identity.
- Tạo, sửa, xóa bộ thẻ công khai hoặc riêng tư.
- Thêm, sửa, xóa thẻ từ vựng với thuật ngữ, định nghĩa, IPA, loại từ, ví dụ tiếng Anh, nghĩa ví dụ tiếng Việt, từ đồng nghĩa.
- Ảnh minh họa cho thẻ bằng URL hoặc upload nội bộ JPG/PNG/WebP, tối đa 2 MB.
- Đánh dấu sao thẻ để học riêng.
- Study Hub: trang chọn chế độ học, hiển thị tiến độ bộ thẻ, gợi ý mode phù hợp, lọc nhanh đã sao/chưa thuộc, liệt kê mode sắp ra mắt.
- Học flashcard với lật thẻ, trộn thẻ, lọc thẻ đã sao hoặc chưa thuộc.
- Nghe chép chính tả: học theo từ vựng hoặc câu ví dụ, so sánh đáp án từng từ, phát âm qua Web Speech API.
- Chế độ tiến độ: đổi nút điều hướng thành `Chưa biết` / `Đã biết` và lưu tiến trình học.
- Cài đặt hiển thị mặt trước/mặt sau: thuật ngữ, định nghĩa, IPA, ví dụ, ảnh, ẩn ảnh, làm mờ ảnh, ảnh lớn.
- Text-to-speech bằng Web Speech API: chọn giọng, tốc độ, tự đọc khi lật sang mặt sau, nút loa luôn đọc từ tiếng Anh.
- Phím tắt: `Space` lật thẻ, `←/→` chuyển thẻ, `1/2` đánh dấu chưa biết/đã biết, `Ctrl` đọc từ tiếng Anh, `Backspace` thoát.
- Màn hình hoàn thành phiên học với thống kê đã biết/cần ôn.

## Kiến trúc hiện tại

Dự án dùng kiến trúc MVC đơn giản:

```text
Razor View
   ↓
Controller
   ↓
Service
   ↓
AppDbContext / EF Core
   ↓
SQL Server
```

Các service thao tác trực tiếp với `AppDbContext` để giữ code gọn:

- `AccountController` dùng `UserManager` và `SignInManager` của ASP.NET Identity.
- `FlashcardSetController` nhận request quản lý bộ thẻ/thẻ và gọi `FlashcardSetService`.
- `StudyController` xử lý luồng học flashcard, nghe chép, tiến trình, đánh sao, settings và gọi `StudyService`, `DictationService` hoặc `FlashcardSetService`.
- `FlashcardSetService` quản lý bộ thẻ, thẻ, quyền sở hữu, upload ảnh và đánh dấu sao.
- `StudyService` quản lý danh sách thẻ để học, tiến trình học, phiên học, cài đặt học và dữ liệu Study Hub.
- `DictationService` quản lý phiên nghe chép, chấm đáp án, so sánh từng từ và cập nhật tiến trình.
- `AppDbContext` kế thừa `IdentityDbContext`, chứa bảng domain và bảng Identity.

## Công nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Framework | ASP.NET Core MVC (.NET 10.0) |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Xác thực | ASP.NET Identity |
| UI | Razor Views, Bootstrap, CSS riêng |
| Icons | Phosphor Icons |
| TTS | Web Speech API |

## Cấu trúc thư mục

```text
ltwnc/
├── Controllers/              # Nhận request, kiểm tra quyền, trả View/Redirect/JSON
│   ├── AccountController.cs
│   ├── HomeController.cs
│   ├── FlashcardSetController.cs
│   └── StudyController.cs
├── Services/                 # Logic nghiệp vụ, thao tác trực tiếp với AppDbContext
│   ├── FlashcardSetService.cs
│   ├── StudyService.cs
│   └── DictationService.cs
├── Data/                     # EF Core DbContext và cấu hình quan hệ/index
│   └── AppDbContext.cs
├── Models/                   # Dữ liệu domain và dữ liệu truyền ra View
│   ├── Entities/             # Entity map với bảng database
│   │   ├── Flashcard.cs
│   │   ├── FlashcardSet.cs
│   │   ├── StudySession.cs
│   │   ├── UserProgress.cs
│   │   └── UserStudySettings.cs
│   └── ViewModels/           # Model chỉ phục vụ UI/Razor View
├── Views/                    # Razor Views hiển thị giao diện
│   ├── Account/
│   ├── FlashcardSet/
│   ├── Home/
│   ├── Shared/
│   └── Study/
├── wwwroot/                  # Static files public: CSS, JS, ảnh upload
│   ├── css/
│   │   ├── site.css
│   │   ├── edit.css
│   │   ├── flashcard.css
│   │   ├── set-management.css
│   │   ├── dictation-redesign.css
│   │   └── study-mode-selector.css
│   ├── js/
│   └── uploads/flashcards/
├── Migrations/               # EF Core migrations tạo/cập nhật database
├── docs/                     # Specs, ADR, glossary
│   ├── specs/
│   ├── adr/
│   └── CONTEXT.md
├── Program.cs
├── appsettings.json
└── ltwnc.csproj
```

## Database

`AppDbContext` quản lý các bảng chính:

- `AspNetUsers`, `AspNetRoles`, ...: bảng của ASP.NET Identity.
- `FlashcardSets`: bộ thẻ.
- `Flashcards`: thẻ từ vựng, gồm IPA, loại từ, ví dụ, ảnh, trạng thái sao.
- `StudySessions`: phiên học đã hoàn thành, gồm mode học và chế độ nghe chép snapshot.
- `DictationSessionDetails`: chi tiết từng câu trả lờ trong phiên nghe chép.
- `UserProgresses`: tiến trình từng user theo từng thẻ, unique theo `(UserId, FlashcardId)`.
- `UserStudySettings`: cài đặt học của từng user, unique theo `UserId`.

Quan hệ quan trọng:

- Một `FlashcardSet` có nhiều `Flashcard`, xóa bộ thẻ sẽ xóa thẻ.
- `StudySession` và `UserProgress` dùng delete restrict, service xóa dữ liệu liên quan trước khi xóa bộ thẻ/thẻ.
- `Flashcard` có index theo `FlashcardSetId` và `(FlashcardSetId, IsStarred)` để lọc học nhanh hơn.

## Cài đặt

Yêu cầu:

- .NET 10 SDK
- SQL Server hoặc SQL Server Express
- `dotnet-ef` nếu cần chạy migration

Clone repo:

```bash
git clone https://github.com/duchuyn04/LTWNC-English.git
cd LTWNC-English
```

Cài EF tool nếu máy chưa có:

```bash
dotnet tool install --global dotnet-ef
```

Restore package:

```bash
dotnet restore
```

## Cấu hình database

Mở `appsettings.json` và chỉnh connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=LTWNC-English;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Một số giá trị `Server` thường dùng:

| SQL Server | Server |
| --- | --- |
| SQL Server Express | `localhost\\SQLEXPRESS` hoặc `.\\SQLEXPRESS` |
| LocalDB | `(localdb)\\mssqllocaldb` |
| Default instance | `localhost` hoặc `.` |

Tạo/cập nhật database:

```bash
dotnet ef database update
```

## Chạy ứng dụng

```bash
dotnet run
```

Mở URL được in ra trong terminal, thường là:

- `https://localhost:5001`
- `http://localhost:5000`

## Luồng sử dụng

1. Đăng ký hoặc đăng nhập.
2. Tạo bộ thẻ mới ở `/Set/Create`.
3. Vào trang sửa bộ thẻ để thêm từ vựng, ảnh và đánh dấu sao nếu cần.
4. Vào `/Study/{setId}` để xem Study Hub, chọn Flashcard hoặc Nghe chép.
5. Dùng thanh điều hướng để chuyển thẻ, trộn thẻ hoặc bật `Tiến độ`.
6. Bật panel cài đặt trên thẻ để chọn mặt trước/mặt sau hiển thị gì.
7. Hoàn thành phiên học để ghi nhận thống kê.

## Routes chính

| Route | Method | Mô tả |
| --- | --- | --- |
| `/` | GET | Trang chủ |
| `/Account/Register` | GET/POST | Đăng ký |
| `/Account/Login` | GET/POST | Đăng nhập |
| `/Account/Logout` | POST | Đăng xuất |
| `/Set` | GET | Bộ thẻ của tôi |
| `/Set/Create` | GET/POST | Tạo bộ thẻ |
| `/Set/{id}` | GET | Chi tiết bộ thẻ |
| `/Set/{id}/Edit` | GET/POST | Sửa bộ thẻ |
| `/Set/{id}/Delete` | POST | Xóa bộ thẻ |
| `/Set/{setId}/Cards/Create` | POST | Thêm thẻ |
| `/Cards/{id}/Edit` | POST | Sửa thẻ |
| `/Cards/{id}/Delete` | POST | Xóa thẻ |
| `/Study/{setId}` | GET | Study Hub: chọn chế độ học |
| `/Study/{setId}/Flashcard` | GET | Học flashcard |
| `/Study/{setId}/Flashcard/Mark` | POST | Đánh dấu đã biết/chưa biết |
| `/Study/{setId}/Flashcard/{cardId}/ToggleStar` | POST | Đổi trạng thái sao |
| `/Study/{setId}/Dictation` | GET | Nghe chép chính tả |
| `/Study/{setId}/Dictation/Check` | POST | Kiểm tra đáp án nghe chép |
| `/Study/{setId}/Dictation/Complete` | POST | Hoàn thành phiên nghe chép |
| `/Study/{setId}/Dictation/Result/{sessionId}` | GET | Kết quả phiên nghe chép |
| `/Study/{setId}/Complete` | POST | Hoàn thành phiên học flashcard |
| `/Study/Settings` | POST | Lưu cài đặt học |
| `/Study/{setId}/ClearFilters` | GET | Xóa bộ lọc Study Hub |

Query của `/Study/{setId}` và `/Study/{setId}/Flashcard`:

- `starredOnly`: chỉ học thẻ đã sao.
- `unlearnedOnly`: chỉ học thẻ chưa thuộc.

## Lưu ý kỹ thuật

- File upload được lưu trong `wwwroot/uploads/flashcards/`.
- Validation ảnh hiện kiểm tra dung lượng, extension và MIME type.
- Cài đặt học được lưu server-side theo user; ngườ chưa đăng nhập vẫn xem/học bộ công khai nhưng không lưu được settings/progress.
- Text-to-speech chạy ở trình duyệt, phụ thuộc voice mà hệ điều hành/trình duyệt cung cấp.
- Nút loa trong flashcard luôn đọc thuật ngữ tiếng Anh (`FrontText`), kể cả khi đang ở mặt sau.

## Lỗi thường gặp

### Lỗi certificate SQL Server

Nếu gặp lỗi SSL/certificate khi kết nối SQL Server, thêm vào connection string:

```text
TrustServerCertificate=True
```

### Port đang được dùng

Nếu `dotnet run` báo port đang được dùng, đổi port trong `Properties/launchSettings.json` hoặc tắt process cũ.

### File build bị lock

Nếu build báo `ltwnc.dll` hoặc `ltwnc.exe` đang bị process `ltwnc` khóa, hãy dừng server đang chạy rồi build lại. Khi chỉ cần kiểm compile mà không dừng server, có thể build ra thư mục tạm:

```bash
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\tmp\ltwnc-build\
```

## License

Dự án học tập cho môn LTWNC.
