# LTWNC English

Ứng dụng học từ vựng tiếng Anh bằng flashcard. Ngườidùng tạo bộ thẻ, thêm từ vựng kèm IPA và ví dụ, chia sẻ bộ công khai, rồi học theo tiến độ cá nhân qua các chế độ Flashcard và Nghe chép.

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

Project áp dụng một số mẫu từ sách *Design Patterns: Elements of Reusable Object-Oriented Software* để code dễ mở rộng và dễ test hơn.

### 🧬 Prototype

Dùng khi ngườidùng sao chép một bộ thẻ công khai vào thư viện riêng.

`FlashcardSet` và `Flashcard` đều implement `IPrototype<T>`. Khi sao chép, `FlashcardSet.Clone()` tạo một bộ mới với cùng tiêu đề, mô tả và danh sách thẻ đã clone. Mỗi `Flashcard.Clone()` tạo thẻ mới, giữ nội dung học thuật nhưng reset trạng thái cá nhân như `IsStarred` và `UploadedImagePath`.

Vì clone tạo object mới hoàn toàn, bộ sao có thể chỉnh sửa riêng mà không ảnh hưởng bộ nguồn.

### 🎯 Strategy

Dùng cho các chế độ học trong Study Hub.

Mỗi chế độ học là một class implement `IStudyModeStrategy`:

- `FlashcardModeStrategy` lấy tất cả thẻ đã qua bộ lọc.
- `DictationModeStrategy` lấy thẻ phù hợp với `DictationContentMode`, ví dụ loại thẻ thiếu câu ví dụ khi chọn ExampleSentence mode.

`StudyService` không còn hard-code từng mode. Nó iterate các strategy đã đăng ký trong DI, mỗi strategy tự lấy thẻ và tự xây option hiển thị. Muốn thêm mode mới thì tạo class mới implement interface, đăng ký trong `Program.cs`, không cần mở `StudyService`.

### 🎮 Command

Dùng cho các thao tác hàng loạt trên thẻ trong trang sửa bộ thẻ.

Các command implement `ICardActionCommand`:

- `DeleteCardsCommand`
- `StarCardsCommand`
- `UnstarCardsCommand`

Mỗi command gói một thao tác kèm dữ liệu cần thiết (setId, userId, danh sách cardId) và khả năng undo. `CardActionService.ExecuteAsync` chạy command, lưu snapshot vào `CardActionLog` để hoàn tác sau này. Controller chỉ việc gọi factory tạo command rồi đưa cho service thực thi.

### 🏭 Factory Method

Dùng để tạo command đúng loại từ action mà controller gửi lên.

`CardActionCommandFactory` nhận một chuỗi action type như `"Delete"`, `"Star"`, `"Unstar"` và trả về command tương ứng. Controller không cần biết command cụ thể là class nào, cũng không cần tự viết switch để khởi tạo.

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
├── Services/                 # Logic nghiệp vụ và các implementation GoF
│   ├── CardActions/          # Command pattern
│   ├── StudyModes/           # Strategy pattern
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

Mở URL được in trong terminal, thường là `https://localhost:5001` hoặc `http://localhost:5000`.

## License

Dự án học tập cho môn LTWNC.
