# LTWNC English

Ứng dụng học từ vựng tiếng Anh bằng flashcard. Người dùng tạo bộ thẻ, thêm từ vựng kèm IPA và ví dụ, chia sẻ bộ công khai, rồi học theo tiến độ cá nhân qua các chế độ Flashcard và Nghe chép.

## Tính năng chính

- Đăng ký, đăng nhập, đăng xuất với ASP.NET Identity.
- Tạo, sửa, xóa bộ thẻ công khai hoặc riêng tư.
- Thêm thẻ với thuật ngữ, định nghĩa, IPA, loại từ, ví dụ tiếng Anh, nghĩa ví dụ tiếng Việt, từ đồng nghĩa.
- Upload ảnh JPG/PNG/WebP tối đa 2 MB hoặc dùng URL ảnh.
- Đánh dấu sao thẻ để học riêng.
- Study Hub: chọn chế độ học, xem tiến độ, gợi ý mode phù hợp, lọc nhanh đã sao/chưa thuộc.
- Học flashcard với lật thẻ, trộn thẻ, lọc thẻ đã sao hoặc chưa thuộc.
- Nghe chép chính tả: học theo từ vựng hoặc câu ví dụ, chấm đáp án từng từ, phát âm qua Web Speech API.
- Lưu tiến trình học qua `UserProgress`.
- Text-to-speech, phím tắt, cài đặt hiển thị mặt trước/mặt sau.

## Các mẫu thiết kế GoF

Project dùng một số mẫu từ sách *Design Patterns: Elements of Reusable Object-Oriented Software*. Mục tiêu không phải “có đủ pattern cho đẹp báo cáo”, mà là gom chỗ dễ phình if/switch và chỗ một hành động kéo theo nhiều hệ quả phụ vào các class riêng, để thêm tính năng sau không phải sửa lan sang service lõi.

### Prototype

**Vấn đề trong project:** User có thể copy một bộ thẻ công khai vào thư viện riêng. Bộ sao phải giữ nguyên nội dung học (từ, nghĩa, IPA, ví dụ, URL ảnh…) nhưng là bản ghi mới: id khác, owner khác, private, không mang sao hay đường dẫn ảnh upload của người khác. Nếu copy bằng gán field thủ công ở service, mỗi lần entity thêm cột là phải nhớ chỉnh chỗ copy; dễ sót hoặc copy nhầm trạng thái cá nhân.

**Vì sao dùng Prototype:** Object tự biết cách nhân bản chính nó. Logic “cái gì giữ, cái gì reset” nằm trên `FlashcardSet` / `Flashcard`, sát model, thay vì rải trong service.

**Cách làm trong code:**

- `FlashcardSet` và `Flashcard` implement `IPrototype<T>`.
- `FlashcardSet.Clone()` giữ tiêu đề, mô tả và deep-clone danh sách thẻ. Reset `Id`, `UserId`, `SourceSetId`, và đặt `IsPublic = false` (bản clone không kế thừa chính sách công khai của nguồn). Caller phải load đủ `Flashcards` trước khi clone.
- `Flashcard.Clone()` giữ nội dung học nhưng reset trạng thái cá nhân: `IsStarred = false`, `UploadedImagePath = null`.
- `FlashcardSetService.CopyPublicSetAsync` load bộ nguồn kèm thẻ, kiểm tra số thẻ trên object khớp database, gọi `Clone()`, rồi gán `UserId`, `SourceSetId`, và khẳng định bản sao là private.

Vì clone tạo object mới hoàn toàn, bộ sao chỉnh sửa riêng được mà không đụng bộ nguồn.

### Strategy

**Vấn đề trong project:** Study Hub có nhiều chế độ học (Flashcard, Nghe chép, sau này có thể thêm Quiz…). Mỗi mode lấy thẻ khác nhau và build option hiển thị khác nhau. Ví dụ Dictation có thể loại thẻ thiếu câu ví dụ khi chọn học theo example sentence. Nếu `StudyService` tự `if (mode == Flashcard) … else if (mode == Dictation) …`, mỗi mode mới buộc mở service lõi, test lại cả class, và logic lọc thẻ trộn với điều phối hub.

**Vì sao dùng Strategy:** Gom “cách lấy thẻ + cách hiện option” của từng mode vào một class. Service chỉ chọn strategy theo mode đã chọn, không chứa chi tiết mode.

**Cách làm trong code:**

Mỗi chế độ học là một class implement `IStudyModeStrategy`:

- `FlashcardModeStrategy` lấy tất cả thẻ đã qua bộ lọc.
- `DictationModeStrategy` lấy thẻ phù hợp với `DictationContentMode` (ví dụ loại thẻ thiếu câu ví dụ khi chọn ExampleSentence).

`StudyService` iterate các strategy đã đăng ký trong DI; mỗi strategy tự lấy thẻ và tự xây option hiển thị. Thêm mode mới: class mới implement interface, đăng ký trong `Program.cs`, không cần mở `StudyService`.

### Command

**Vấn đề trong project:** Trang sửa bộ thẻ có thao tác hàng loạt: xóa nhiều thẻ, gắn sao, bỏ sao. Các thao tác này cần undo (snapshot trước khi chạy). Nếu controller hoặc service gọi thẳng EF theo từng action, logic thực thi, hoàn tác và log dính vào nhau; mỗi action mới lại copy boilerplate snapshot/undo.

**Vì sao dùng Command:** Gói một thao tác thành object có `Execute` / `Undo` và dữ liệu cần thiết. Service chỉ chạy command và lưu log; controller không chứa bước EF của từng action.

**Cách làm trong code:**

Các command implement `ICardActionCommand`:

- `DeleteCardsCommand`
- `StarCardsCommand`
- `UnstarCardsCommand`

Mỗi command mang setId, userId, danh sách cardId và biết undo. `CardActionService.ExecuteAsync` chạy command, lưu snapshot vào `CardActionLog` để hoàn tác sau. Controller gọi factory tạo command rồi đưa cho service thực thi.

### Factory Method

**Vấn đề trong project:** Controller nhận chuỗi action type từ form/API (`"Delete"`, `"Star"`, `"Unstar"`). Nếu controller tự `new` từng command hoặc tự switch khởi tạo, nó phải biết constructor, dependency (`AppDbContext`) và danh sách class concrete. Thêm action mới là sửa controller.

**Vì sao dùng Factory Method:** Một chỗ duy nhất map “tên action → object command”. Controller chỉ truyền action type + tham số; không import từng class command.

**Cách làm trong code:**

`CardActionCommandFactory.Create(...)` nhận action type và trả về `ICardActionCommand` tương ứng. Switch khởi tạo nằm trong factory, không nằm ở controller.

### Observer

**Vấn đề trong project:** Sau khi user đánh dấu thẻ đã thuộc, xong buổi học, hoặc trả lời nghe chép, hệ thống còn việc phụ: mở huy hiệu đủ điều kiện, ghi log. Nếu `StudyService` / `DictationService` gọi thẳng `AchievementUnlockService` và logger, service học bị phụ thuộc thành tích; thêm phản ứng mới (thống kê, notification…) lại sửa service học. Lỗi ở mở huy hiệu cũng dễ kéo hỏng luồng học chính nếu gọi nối tiếp không tách.

**Vì sao dùng Observer:** Service học chỉ phát sự kiện sau khi đã lưu database. Ai cần phản ứng thì đăng ký lắng nghe. Subject không biết concrete observer nào đang có.

**Cách làm trong code:**

- Subject: `StudyEventPublisher` nhận sự kiện và gọi lần lượt các observer.
- Observer: `IStudyEventObserver`.
- Concrete observers:
  - `AchievementStudyObserver` gọi `AchievementUnlockService` để mở khóa huy hiệu (lưu `UserAchievements`).
  - `LoggingStudyObserver` ghi log hệ thống (minh họa một sự kiện, nhiều listener).
- Sự kiện: `CardProgressChangedEvent`, `StudySessionCompletedEvent`, `DictationAnswerCheckedEvent`.
- Tiến độ (live): `AchievementProgressService` đếm thẻ đã thuộc, buổi học, câu nghe chép đúng… theo user; catalog trong code gắn metric + target.
- Mở khóa: `AchievementUnlockService.SyncEligibleAsync` dùng khi học (qua Observer) và khi mở trang `/Achievements` (rescan, bù huy hiệu mới hoặc sự kiện bỏ lỡ).
- UI: thanh tiến độ, `current/target`, CTA sang `/Set`; banner TempData khi rescan vừa mở huy hiệu mới.
- `StudyService` / `DictationService` chỉ gọi `PublishAsync` sau khi lưu database; không biết chi tiết thành tích.
- Thêm observer mới: class implement `IStudyEventObserver`, một dòng đăng ký DI trong `Program.cs`.
- Trang xem: `/Achievements` (cần đăng nhập).

Trong ASP.NET, danh sách observer lấy từ DI (tương đương `Attach` trong sách GoF). Nếu một observer lỗi, publisher bắt exception và log; observer khác vẫn nhận tin, buổi học không bị hỏng vì lỗi phụ.

## Công nghệ

| Thành phần | Công nghệ                         |
| ---------- | --------------------------------- |
| Framework  | ASP.NET Core MVC (.NET 10.0)      |
| Database   | SQL Server                        |
| ORM        | Entity Framework Core             |
| Xác thực   | ASP.NET Identity                  |
| UI         | Razor Views, Bootstrap, CSS riêng |
| Icons      | Phosphor Icons                    |
| TTS        | Web Speech API                    |

## Cấu trúc thư mục

```text
ltwnc/
├── Controllers/              # Nhận request, kiểm tra quyền, trả View/Redirect/JSON
├── Services/                 # Logic nghiệp vụ và các implementation GoF
│   ├── CardActions/          # Command pattern
│   ├── StudyModes/           # Strategy pattern
│   ├── StudyEvents/          # Observer pattern (sự kiện học + thành tích)
│   └── FlashcardSetService.cs
├── Data/                     # EF Core DbContext
│   └── AppDbContext.cs
├── Models/                   # Entities và ViewModels
│   ├── Entities/
│   └── ViewModels/
├── Views/                    # Razor Views
├── wwwroot/                  # Static files
├── Migrations/               # EF Core migrations
├── Program.cs
├── appsettings.json
└── ltwnc.csproj
```

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

Cài EF tool nếu chưa có:

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

| SQL Server         | Server                                       |
| ------------------ | -------------------------------------------- |
| SQL Server Express | `localhost\\SQLEXPRESS` hoặc `.\\SQLEXPRESS` |
| LocalDB            | `(localdb)\\mssqllocaldb`                    |
| Default instance   | `localhost` hoặc `.`                         |

Tạo/cập nhật database:

```bash
dotnet ef database update
```

## Chạy ứng dụng

```bash
dotnet run
```

Mở URL được in trong terminal, thường là `https://localhost:5001` hoặc `http://localhost:5000`.

## License

Dự án học tập cho môn LTWNC.
