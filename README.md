# 📚 LTWNC English

Ứng dụng học từ vựng tiếng Anh bằng flashcard. Tạo bộ thẻ, thêm từ vựng kèm IPA và ví dụ, chia sẻ công khai, rồi học theo tiến độ cá nhân qua Flashcard, Nghe chép, hoặc hội thoại với AI.

## ✨ Tính năng chính

- 🔐 Đăng ký, đăng nhập, đăng xuất (ASP.NET Core Identity)
- 👤 Profile cá nhân/công khai tại `/{username}`, thống kê học tập, timeline, quyền riêng tư
- 🖼️ Avatar upload JPG/PNG/WebP tối đa 5 MB, crop theo khung tròn
- 🔀 Trang 404 tương tác concept Wrong Turn với vocabulary card
- 📝 Tạo, sửa, xóa bộ thẻ công khai hoặc riêng tư
- 🃏 Thẻ có thuật ngữ, định nghĩa, IPA, loại từ, ví dụ tiếng Anh, nghĩa tiếng Việt, từ đồng nghĩa
- 📸 Upload ảnh JPG/PNG/WebP tối đa 2 MB hoặc dùng URL
- ⭐ Đánh dấu sao thẻ để học riêng
- 🎯 Study Hub: chọn chế độ học, xem tiến độ, gợi ý mode phù hợp, lọc nhanh đã sao/chưa thuộc
- 🔄 Học flashcard với lật thẻ, trộn thẻ, lọc thẻ đã sao hoặc chưa thuộc
- 🎧 Nghe chép chính tả: học theo từ vựng hoặc câu ví dụ, chấm đáp án từng từ, phát âm qua Web Speech API
- 🤖 English Mission: chọn chủ đề, dùng từ trong bộ thẻ để hội thoại thích ứng với gia sư AI
- 💾 Lưu tiến trình học qua `UserProgress`
- 🔊 Text-to-speech, phím tắt, cài đặt hiển thị mặt trước/mặt sau

## 📥 Nhập thẻ từ tệp

Từ trang chỉnh sửa bộ thẻ tại `/Set/{id}/Edit` (chỉ chủ sở hữu), chọn tệp `.csv` hoặc `.xlsx`. XLSX chỉ đọc worksheet đầu tiên. Kích thước tối đa **10 MB**, định dạng khác bị từ chối.

Hàng đầu tiên phải có đúng các cột bắt buộc (không phân biệt hoa thường): `Thuật ngữ`, `Định nghĩa`, `IPA`, `Loại từ`, `Ví dụ tiếng Anh`, `Nghĩa ví dụ tiếng Việt`. Hai cột tùy chọn: `Từ đồng nghĩa` và `URL ẢNH`.

Mẫu CSV tối thiểu:

```csv
Thuật ngữ,Định nghĩa,IPA,Loại từ,Ví dụ tiếng Anh,Nghĩa ví dụ tiếng Việt,Từ đồng nghĩa,URL ẢNH
```

Dòng trống bị bỏ qua. Dòng hợp lệ vẫn được nhập nếu tệp có dòng lỗi. Thiếu cột bắt buộc hoặc sai định dạng thì toàn bộ tệp bị từ chối.

## 🏗️ Các mẫu thiết kế GoF

Project dùng một số mẫu GoF, không phải "có đủ cho đẹp báo cáo" mà vì chỗ cụ thể trong code cần chúng. Không gom code theo thư mục `Patterns/` hay `GoF/`, mỗi pattern nằm trong domain dùng nó.

### 🔄 Prototype

**Vấn đề:** Copy bộ thẻ công khai vào thư viện riêng. Bản sao phải giữ nội dung học nhưng là bản ghi mới, khác id, khác owner, reset trạng thái cá nhân.

**Cách làm:** `FlashcardSet` và `Flashcard` implement `IPrototype<T>`. Logic "cái gì giữ, cái gì reset" nằm trên entity, không rải trong service.

- `FlashcardSet.Clone()` giữ tiêu đề, mô tả, deep-clone danh sách thẻ. Reset `Id`, `UserId`, `SourceSetId`, đặt `IsPublic = false`.
- `Flashcard.Clone()` giữ nội dung học, reset `IsStarred = false`, `UploadedImagePath = null`.
- `FlashcardSetService.CopyPublicSetAsync` load bộ nguồn, gọi `Clone()`, gán owner mới.

### 🎯 Strategy

**Vấn đề:** Study Hub có nhiều chế độ học (Flashcard, Nghe chép, English Mission). Mỗi mode lấy thẻ khác nhau, build option khác nhau. Nếu `StudyService` tự switch theo mode, mỗi mode mới buộc mở service lõi.

**Cách làm:** Mỗi chế độ là một class implement `IStudyModeStrategy`. `StudyService` iterate các strategy đã đăng ký trong DI, không chứa chi tiết mode.

- `FlashcardModeStrategy` lấy tất cả thẻ đã qua bộ lọc
- `DictationModeStrategy` lấy thẻ phù hợp với `DictationContentMode`
- `EnglishMissionModeStrategy` lấy thẻ cho mission

### ⚡ Command

**Vấn đề:** Thao tác hàng loạt (xóa nhiều, gắn sao, bỏ sao) cần undo. Nếu service gọi thẳng EF theo từng action, logic thực thi, hoàn tác và log dính vào nhau.

**Cách làm:** Gói thao tác thành object có `Execute` / `Undo`. `CardActionService` chạy command, lưu snapshot vào `CardActionLog`.

- `DeleteCardsCommand`, `StarCardsCommand`, `UnstarCardsCommand`
- Mỗi command mang setId, userId, danh sách cardId và biết undo

### 🏭 Factory Method

**Vấn đề:** Controller nhận chuỗi action type từ form. Nếu tự `new` từng command, thêm action mới là sửa controller.

**Cách làm:** `CardActionCommandFactory.Create(...)` map "tên action → object command". Switch khởi tạo nằm trong factory, không ở controller.

### 👀 Observer

**Vấn đề:** Sau khi đánh dấu thẻ đã thuộc hoặc xong buổi học, hệ thống còn việc phụ (mở huy hiệu, ghi log). Nếu service học gọi thẳng các service phụ, nó bị phụ thuộc; thêm phản ứng mới lại sửa service học.

**Cách làm:** Service học chỉ phát sự kiện qua `StudyEventPublisher`. Ai cần phản ứng thì đăng ký. Subject không biết concrete observer nào đang có.

- `AchievementStudyObserver` gọi `AchievementUnlockService` mở huy hiệu
- `LoggingStudyObserver` ghi log hệ thống
- Sự kiện: `CardProgressChangedEvent`, `StudySessionCompletedEvent`, `DictationAnswerCheckedEvent`
- Observer lỗi thì bắt exception, log, observer khác vẫn nhận tin

### 🔌 Adapter

**Vấn đề:** Gọi AI provider bên ngoài qua HTTP. Nếu service học/service mission biết chi tiết HTTP, đổi provider là sửa khắp nơi.

**Cách làm:** `IAiProviderAdapter` là interface chung. `OpenAiCompatibleAdapter` chuyển call domain thành HTTP cụ thể qua `OpenAiCompatibleClient`. Router (`AiCompletionRouter`) chỉ biết interface, không biết chi tiết HTTP. Fallback theo `Priority`, thử provider tiếp theo khi timeout hoặc 5xx.

### 📦 Application service interfaces

Các application service (`FlashcardSetService`, `StudyService`, `DictationService`...) đều có contract `I*` tương ứng. Controllers inject interface, `Program.cs` đăng ký `AddScoped<IService, Service>()`. Mục đích: thay implementation hoặc bọc decorator mà không sửa call site.

## 🛠️ Công nghệ

| Thành phần | Công nghệ |
|------------|-----------|
| 🖥️ Framework | ASP.NET Core MVC (.NET 10.0) |
| 🗄️ Database | SQL Server |
| ⚙️ ORM | Entity Framework Core |
| 🔑 Xác thực | ASP.NET Core Identity (cookie) |
| 🎨 UI | Razor Views, Bootstrap, CSS riêng |
| 🔷 Icons | Phosphor Icons |
| 🔊 TTS | Web Speech API |
| 🤖 AI | Custom OpenAI-compatible providers |

## 🤖 AI providers và English Mission

English Mission gọi AI qua backend. Provider quản lý tại `/Admin/AiProviders`, hỗ trợ API key tùy chọn, discovery model qua `/models`, kiểm tra kết nối, fallback theo `Priority`. API key mã hóa bằng ASP.NET Core Data Protection.

Migration mặc định tạo provider local:

```text
Name: 9Router Local
Base URL: http://localhost:20128/v1
Model: cx/gpt-5.6-luna
API key: không có
```

User cấu hình tại `AdminBootstrap:UserId` được gán role `Admin` khi database áp đủ migration. Có thể đặt bằng biến môi trường `AdminBootstrap__UserId`. Sau khi gán role, cần đăng xuất đăng nhập lại để cookie nhận role claim.

Provider từ xa bắt buộc HTTPS. HTTP chỉ được phép cho localhost/loopback. Timeout, lỗi mạng, 429/5xx hoặc output sai schema thì thử provider tiếp theo. 400/401/403 là lỗi cấu hình, không fallback.

## 📁 Cấu trúc thư mục

```text
ltwnc/
├── Areas/
│   └── Admin/                        # Khu vực quản trị tách biệt
│       ├── Controllers/              # Dashboard, Users, Content, AiProviders, AuditLogs...
│       ├── Models/                   # ViewModels riêng cho admin
│       └── Views/                    # Razor views admin
├── Controllers/                      # MVC chính: Home, Account, Study, FlashcardSet, Profile...
├── Services/                         # Nghiệp vụ, tổ chức theo domain
│   ├── Achievements/                 # Catalog, progress, unlock + observer thành tích
│   ├── AdminAchievements/            # Admin quản lý huy hiệu
│   ├── AdminAuditRetention/          # Tác vụ nền dọn audit log quá hạn
│   ├── AdminDashboard/               # KPI dashboard
│   ├── AdminEnglishMissions/         # Admin quản lý mission + dọn transcript
│   ├── AdminExports/                 # Xuất dữ liệu CSV/Excel
│   ├── AdminSearch/                  # Tìm kiếm toàn cục admin
│   ├── AdminStudyRecords/            # Admin xem bản ghi học
│   ├── AdminUsers/                   # Admin quản lý tài khoản + khóa
│   ├── Ai/                           # AI completion router, adapter, provider
│   ├── Audit/                        # Ghi audit log cho hành động admin
│   ├── Auth/                         # Identity auth, CurrentUser, AdminRoleBootstrapper
│   ├── CardActions/                  # Command: batch delete/star/unstar + undo
│   ├── ContentModeration/            # Kiểm duyệt nội dung (quarantine/restrict/allow)
│   ├── ContentReports/               # Xử lý báo cáo nội dung từ user
│   ├── EnglishMission/               # Mission service, contracts
│   ├── FlashcardSets/                # CRUD bộ thẻ / thẻ / copy / import CSV-XLSX
│   ├── Leaderboard/                  # Bảng xếp hạng
│   ├── Profiles/                     # Profile, avatar, thống kê, timeline
│   ├── Study/                        # Study hub, flashcard session, dictation
│   ├── StudyEvents/                  # Observer: publisher + sự kiện học
│   └── StudyModes/                   # Strategy: lọc thẻ theo chế độ học
├── Data/                             # AppDbContext (EF Core)
├── Models/
│   ├── Entities/                     # FlashcardSet, Flashcard, UserProgress, StudySession...
│   ├── Enums/                        # BatchActionType...
│   └── ViewModels/                   # Group theo feature (Account, Study, Profile, FlashcardSet...)
├── Views/                            # Razor views chính
│   ├── Account/                      # Login, Register
│   ├── Achievements/                 # Trang huy hiệu
│   ├── EnglishMission/               # Giao diện mission
│   ├── FlashcardSet/                 # CRUD bộ thẻ
│   ├── Home/                         # Trang chủ
│   ├── Leaderboard/                  # Bảng xếp hạng
│   ├── Profile/                      # Profile công khai / chỉnh sửa
│   ├── Study/                        # Flashcard, Dictation, Study Hub
│   └── Shared/                       # Layout, partial view chung
├── wwwroot/
│   ├── css/                          # Stylesheet tùy chỉnh
│   ├── js/                           # JavaScript tùy chỉnh
│   ├── images/                       # Ảnh tĩnh
│   ├── lib/                          # Bootstrap, jQuery, jQuery Validation
│   └── uploads/                      # File user upload (avatar...)
├── Migrations/                       # EF Core migrations
├── docs/                             # ADR, quyết định thiết kế
├── Properties/                       # launchSettings.json
├── tests/
│   └── ltwnc.Tests/                  # Unit + integration tests
│       ├── Controllers/
│       ├── Data/
│       ├── Infrastructure/
│       ├── Integration/
│       ├── Services/                 # Mirror Services/ theo domain
│       ├── Views/
│       └── e2e/                      # Playwright end-to-end tests
├── Program.cs                        # Entry point, DI, middleware
└── ltwnc.csproj
```

Folder theo **domain**, không theo tên pattern. Pattern nằm trong domain liên quan.

## ⚙️ Cài đặt

Yêu cầu:
- .NET 10 SDK
- SQL Server hoặc SQL Server Express
- `dotnet-ef` nếu cần chạy migration

Clone repo:

```bash
git clone https://github.com/duchuyn04/LTWNC-English.git
cd LTWNC-English
```

Cài EF tool nếu chưa có:

```bash
dotnet tool install --global dotnet-ef
```

Restore package:

```bash
dotnet restore
```

## 🗄️ Cấu hình database

Mở `appsettings.json` chỉnh connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=LTWNC-English;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Một số giá trị `Server` thường dùng:

| SQL Server | Server |
|------------|--------|
| SQL Server Express | `localhost\\SQLEXPRESS` hoặc `.\\SQLEXPRESS` |
| LocalDB | `(localdb)\\mssqllocaldb` |
| Default instance | `localhost` hoặc `.` |

Tạo/cập nhật database:

```bash
dotnet ef database update
```

## ▶️ Chạy ứng dụng

```bash
dotnet run
```

Mở URL trong terminal, thường là `https://localhost:5001` hoặc `http://localhost:5000`.

## 📄 License

Dự án học tập cho môn LTWNC.
