# Thiết kế chế độ học Nghe chép chính tả (Dictation)

## Tóm tắt

Thêm chế độ học **Nghe chép chính tả** vào trang chọn chế độ học `/Study/{setId}`. Người dùng nghe thuật ngữ tiếng Anh qua Web Speech API, sau đó nhập lại đáp án. Hệ thống kiểm tra ngay, hiển thị phản hồi sau mỗi câu, và có màn hình tổng kết cuối phiên.

## Quyết định thiết kế

| Chủ đề | Quyết định |
|--------|-----------|
| Vị trí | Chế độ học mới **Nghe chép** trên `/Study/{setId}`, cạnh Flashcard/Quiz/Viết/Ghép đôi. |
| Kiến trúc | Razor view + JS nhúng dữ liệu (Cách tiếp cận 1). API chỉ dùng cho check đáp án và hoàn thành phiên. |
| Nguồn âm thanh | Trình duyệt Web Speech API (`speechSynthesis`). |
| Yêu cầu đăng nhập | Bắt buộc đăng nhập để lưu tiến trình, câu sai, streak. |
| Chế độ trả lời | Có thể chuyển đổi trong cài đặt: trả lời thuật ngữ hoặc trả lời định nghĩa. |
| Feedback | Cả feedback ngay sau mỗi câu và màn hình tổng kết cuối phiên. |

## Thay đổi dữ liệu

### 1. `StudyMode` enum

Thêm giá trị `Dictation` vào `Models/Entities/StudySession.cs`.

```csharp
public enum StudyMode
{
    Flashcard,
    Quiz,
    Write,
    Match,
    Dictation
}
```

### 2. `UserStudySettings`

Bổ sung các cột cài đặt riêng cho nghe chép:

| Tên | Kiểu | Mặc định | Ý nghĩa |
|-----|------|----------|---------|
| `DictationAnswerMode` | `DictationAnswerMode` | `Term` | `Term`: đọc thuật ngữ, nhập thuật ngữ. `Definition`: đọc thuật ngữ, nhập nghĩa. |
| `DictationAutoAdvance` | `bool` | `false` | Tự động chuyển câu khi trả lời đúng. |
| `DictationPlaybackSpeed` | `float` | `1.0` | Tốc độ phát âm: 0.5, 0.75, 1.0, 1.25, 1.5. |
| `DictationVoiceUri` | `string?` | `null` | URI giọng đọc được chọn từ `speechSynthesis.getVoices()`. |
| `DictationShowHint` | `bool` | `true` | Hiện gợi ý (IPA, hình ảnh, nghĩa) khi trả lời sai. |
| `DictationAcceptSynonyms` | `bool` | `true` | Chấp nhận từ đồng nghĩa làm đáp án đúng. |
| `DictationShuffle` | `bool` | `false` | Xáo trộn thứ tự thẻ. |

Thêm enum mới:

```csharp
public enum DictationAnswerMode
{
    Term,
    Definition
}
```

### 3. `StudySession`

Dùng sẵn cột `Score` để lưu phần trăm đúng của phiên nghe chép.

### 4. Bảng mới `DictationSessionDetails`

Lưu chi tiết từng câu trong phiên.

```csharp
public class DictationSessionDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StudySessionId { get; set; }

    [Required]
    public int FlashcardId { get; set; }

    public bool IsCorrect { get; set; }

    public string AnsweredText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(StudySessionId))]
    public StudySession? StudySession { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
```

## Controller, Service và API

### `DictationService` (`Services/DictationService.cs`)

| Phương thức | Chức năng |
|-------------|-----------|
| `GetCardsForDictationAsync(int setId, string userId, UserStudySettings settings)` | Lấy danh sách thẻ, áp dụng lọc và xáo trộn. |
| `CreateSessionAsync(string userId, int setId)` | Tạo `StudySession` với `Mode = Dictation`, `CompletedAt` tạm thời. |
| `CheckAnswerAsync(int sessionId, int cardId, string answeredText, string userId, DictationAnswerMode mode, bool acceptSynonyms)` | Kiểm tra đáp án, cập nhật `UserProgress`, thêm `DictationSessionDetail`. Trả về kết quả. |
| `CompleteSessionAsync(int sessionId, int score)` | Đóng phiên, ghi `Score`. |
| `GetSessionResultAsync(int sessionId, string userId)` | Trả dữ liệu màn hình tổng kết. |

### `StudyController` thêm action

| Route | Phương thức | Mô tả |
|-------|-------------|-------|
| `GET /Study/{setId}/Dictation` | `Dictation` | Render view học. |
| `POST /Study/{setId}/Dictation/Check` | `DictationCheck` | Kiểm tra đáp án, trả JSON. |
| `POST /Study/{setId}/Dictation/Complete` | `DictationComplete` | Hoàn thành phiên, trả `redirectUrl`. |
| `GET /Study/{setId}/Dictation/Result/{sessionId}` | `DictationResult` | View màn hình tổng kết. |

### ViewModel

```csharp
public class DictationStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<DictationCardViewModel> Cards { get; set; } = new();
    public UserStudySettings Settings { get; set; } = new();
    public int StreakDays { get; set; }
}

public class DictationCardViewModel
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? UploadedImagePath { get; set; }
    public string? Synonyms { get; set; }
}
```

### Logic kiểm tra đáp án

1. Chuẩn hóa đáp án người dùng: loại bỏ khoảng trắng đầu/cuối, dấu câu thừa, chuyển về chữ thường.
2. Chuẩn hóa đáp án đúng tương ứng với `DictationAnswerMode`:
   - `Term`: `FrontText`
   - `Definition`: `BackText`
3. Nếu `DictationAcceptSynonyms = true` và mode là `Term`, tách `Synonyms` bằng dấu phẩy hoặc chấm phẩy, chuẩn hóa từng phần tử, so sánh với tập đáp án.
4. Trả về `IsCorrect = true` nếu khớp bất kỳ đáp án nào.

## Luồng màn hình

### 1. Màn hình học `/Study/{setId}/Dictation`

- **Header**: nút quay lại, tiến độ `current / total`, nút cài đặt.
- **Vùng bài tập**:
  - Nút phát âm lớn.
  - Dropdown chọn tốc độ.
  - Textarea nhập đáp án.
  - Nút `Kiểm tra (Enter)`.
  - Nút `Tôi không biết`.
  - Phím tắt `Ctrl + 1` để nghe lại.
- **Sidebar**:
  - Tiến độ buổi học.
  - Tỉ lệ chính xác.
  - Chuỗi học tập.
  - Link xem lại lịch sử câu sai.
  - Kết thúc phiên học.

### 2. Feedback ngay sau mỗi câu

- **Đúng**: viền xanh, dấu tick, hiển thị đáp án. Nếu `DictationAutoAdvance = true`, tự chuyển câu sau 1 giây.
- **Sai**: viền đỏ, hiển thị đáp án đúng. Nếu `DictationShowHint = true`, hiển thị IPA / hình ảnh / nghĩa. Nút `Tiếp tục`.

### 3. Màn hình tổng kết `/Study/{setId}/Dictation/Result/{sessionId}`

- Vòng tròn tỉ lệ đúng.
- Số liệu: tổng thể, đã thuộc, cần ôn.
- Danh sách các thẻ cần ôn.
- Các nút:
  - `Ôn lại thẻ cần nhớ`
  - `Chuyển sang kiểm tra`
  - `Học lại toàn bộ`
  - `Về bộ từ vựng`
- Chuỗi học tập ở dưới.

## Modal cài đặt nghe chép

Giao diện giống ảnh cài đặt đã cung cấp, gồm các nhóm:

1. **Lọc từ vựng**
   - Chỉ học từ đánh dấu sao.
   - Chỉ học từ chưa thuộc.

2. **Thứ tự học**
   - Học theo thứ tự (không xáo trộn).
   - Xáo trộn.

3. **Chế độ trả lời**
   - Trả lời bằng thuật ngữ (đọc thuật ngữ).
   - Trả lời bằng định nghĩa (đọc thuật ngữ).

4. **Tùy chọn trả lời**
   - Chấp nhận từ đồng nghĩa làm đáp án.

5. **Tùy chọn hành vi**
   - Tự động tiếp tục khi trả lời đúng.

6. **Tùy chọn âm thanh**
   - Tốc độ phát âm.
   - Chọn giọng đọc (dropdown từ `speechSynthesis.getVoices()`).

7. **Gợi ý**
   - Hiện IPA / hình ảnh / nghĩa khi trả lời sai.

Cài đặt được lưu qua API `/Study/Settings` (mở rộng form binding `UserStudySettings`) và áp dụng ngay.

## Xử lý lỗi và trường hợp biên

| Trường hợp | Hành vi |
|------------|---------|
| Trình duyệt không hỗ trợ Web Speech API | Vô hiệu hóa nút phát âm, hiện thông báo nhỏ. |
| Không có giọng đọc | Dùng giọng mặc định, ẩn dropdown chọn giọng. |
| Chưa đăng nhập | Redirect về trang đăng nhập. |
| Bộ từ không có thẻ hoặc lọc ra rỗng | Redirect về `/Study/{setId}` với `TempData["Message"]`. |
| Đáp án rỗng | Không gọi check, hiện tooltip nhắc nhập. |
| Phiên học không tồn tại | Trả 404 khi gọi check/complete/result. |
| Lỗi mạng khi check | Hiện nút thử lại, giữ nguyên input. |
| Chính sách autoplay | Chỉ phát âm sau lần tương tác đầu tiên. |
| Thiết bị di động | Phím Enter trên bàn phím ảo gọi `Kiểm tra`. |
| `Synonyms` null hoặc rỗng | Không lỗi khi tách chuỗi. |

## Kiểm thử

### Unit test `DictationService`

- `GetCardsForDictationAsync`:
  - Lọc `StarredOnly`.
  - Lọc `UnlearnedOnly`.
  - Kết hợp cả 2 bộ lọc.
  - Bộ từ rỗng trả về list rỗng.
  - Xáo trộn khi bật shuffle.
- `CheckAnswerAsync`:
  - Đáp án chính xác hoàn toàn.
  - Hoa thường khác nhau vẫn đúng.
  - Khoảng trắng đầu/cuối / dấu câu thừa vẫn đúng.
  - Đáp án là từ đồng nghĩa (dấu phẩy, chấm phẩy, nhiều khoảng trắng).
  - Từ đồng nghĩa bị tắt thì không chấp nhận.
  - Chế độ `Definition`: nhập đúng `BackText`.
  - Chế độ `Definition`: nhập sai `BackText`.
  - Sai → `WrongCount++`, `Status = Learning`.
  - Đúng → `CorrectCount++`, `Status = Mastered`.
  - Tạo `DictationSessionDetail` với `IsCorrect`, `AnsweredText`.
- `CompleteSessionAsync`:
  - Tính `Score` đúng từ các detail.
  - Phiên không tồn tại → `KeyNotFoundException`.
- `GetSessionResultAsync`:
  - Trả đúng danh sách câu sai.
  - Trả đúng tổng số câu và phần trăm.
  - Không phải chủ nhân phiên → `UnauthorizedAccessException`.

### Integration test Controller

- `GET /Study/{setId}/Dictation`:
  - Set hợp lệ → view với model đúng.
  - Set không tồn tại → 404.
  - Chưa đăng nhập → redirect login.
- `POST /Study/{setId}/Dictation/Check`:
  - Đúng → JSON `{ isCorrect: true }`.
  - Sai → JSON `{ isCorrect: false, correctAnswer, hint }`.
  - Card không thuộc set → 404.
  - Chưa đăng nhập → 401 / redirect login.
- `POST /Study/{setId}/Dictation/Complete`:
  - Lưu session, trả `redirectUrl`.
- `GET /Study/{setId}/Dictation/Result/{sessionId}`:
  - Hiển thị đúng màn hình tổng kết.

### Manual test UI/JS

- Nút phát âm hoạt động.
- `Ctrl + 1` phát lại.
- Dropdown tốc độ thay đổi `SpeechSynthesisUtterance.rate`.
- Dropdown giọng đọc thay đổi `voice`.
- `Enter` gửi check.
- Input rỗng không gọi check.
- Đúng + auto-advance → tự chuyển câu.
- Sai → hiện đáp án + hint IPA/hình ảnh/nghĩa.
- Nút "Tôi không biết" tính là sai và hiện đáp án.
- Thanh tiến độ cập nhật sau mỗi câu.
- Sidebar tỉ lệ chính xác cập nhật.
- Modal cài đặt lưu và áp dụng ngay.
- Màn hình tổng kết hiển thị đúng %, số thẻ cần ôn, nút "Ôn lại".

### Edge cases

- Trình duyệt không có `speechSynthesis`: UI graceful degrade.
- Thẻ không có `Synonyms`: không lỗi khi tách chuỗi.
- `Synonyms` có ký tự đặc biệt.
- Mất kết nối khi check: hiện lỗi, giữ nguyên input.
- Người dùng reload giữa chừng: phiên chưa complete, có thể bỏ qua hoặc tính là chưa xong.

## Phạm vi ngoài lần này

- Không hỗ trợ upload file audio (chỉ dùng Web Speech API).
- Không hỗ trợ nhập bằng giọng nói (speech-to-text).
- Không hỗ trợ nhiều ngôn ngữ phát âm ngoài tiếng Anh.
- Không hỗ trợ resume phiên học bị gián đoạn giữa chừng (có thể làm ở lần sau).
