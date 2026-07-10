# Thiết kế chế độ học Nghe chép câu

## Tóm tắt

Thêm chế độ học **Nghe chép câu** bên cạnh chế độ Nghe chép từ đơn lẻ hiện có. Ngườ dùng nghe câu ví dụ tiếng Anh (`Flashcard.ExampleSentence`) qua Web Speech API, sau đó nhập lại toàn bộ câu. Hệ thống kiểm tra độ chính xác và tổng kết như chế độ Nghe chép thông thường.

## Quyết định thiết kế

| Chủ đề | Quyết định |
|--------|-----------|
| Vị trí | Thẻ chế độ học mới **Nghe chép câu** trên `/Study/{setId}`, cạnh các chế độ hiện có. |
| Route mới | `GET /Study/{setId}/Dictation/Sentence`. |
| Kiến trúc | Tái sử dụng view `Dictation.cshtml` với cờ `IsSentenceMode`. Dùng service `DictationService` với tham số `DictationSource`. |
| Nguồn âm thanh | `ExampleSentence` của thẻ. |
| Đáp án đúng | `ExampleSentence` của thẻ. |
| Chế độ phiên | Thêm giá trị `SentenceDictation` vào enum `StudyMode`. |
| Cài đặt | Không thêm cột mới. Tái sử dụng filters, shuffle, auto-advance, TTS, hint. Ẩn nhóm “Chế độ trả lờ” và “Tùy chọn trả lờ” khi ở chế độ câu. |

## Thay đổi dữ liệu

### 1. `StudyMode` enum

Thêm giá trị `SentenceDictation` trong `Models/Entities/StudySession.cs`.

```csharp
public enum StudyMode
{
    Flashcard,
    Quiz,
    Write,
    Match,
    Dictation,
    SentenceDictation
}
```

### 2. `DictationSource` enum

Thêm enum mới trong `Services/DictationService.cs` để xác định nội dung được nghe/kiểm tra:

```csharp
public enum DictationSource
{
    Term,
    Definition,
    ExampleSentence
}
```

## Controller và Service

### `DictationService` (`Services/DictationService.cs`)

Thay đổi signature `CheckAnswerAsync`:

```csharp
public async Task<DictationCheckResult> CheckAnswerAsync(
    int sessionId,
    int cardId,
    string answeredText,
    string userId,
    DictationSource source,
    bool acceptSynonyms)
```

- `source == Term`: đáp án đúng là `FrontText`, hỗ trợ từ đồng nghĩa nếu bật.
- `source == Definition`: đáp án đúng là `BackText`.
- `source == ExampleSentence`: đáp án đúng là `ExampleSentence`, không hỗ trợ từ đồng nghĩa.

Thêm overload `CreateSessionAsync` cho phép chỉ định `StudyMode`:

```csharp
public async Task<StudySession> CreateSessionAsync(string userId, int setId, StudyMode mode)
```

### `StudyController` (`Controllers/StudyController.cs`)

Thêm action:

```csharp
[Authorize]
[Route("/Study/{setId}/Dictation/Sentence")]
public async Task<IActionResult> SentenceDictation(int setId)
```

Hành vi:
- Kiểm tra quyền truy cập bộ thẻ.
- Lấy settings, lọc thẻ có `ExampleSentence` không rỗng.
- Tạo phiên với `Mode = StudyMode.SentenceDictation`.
- Trả về view `Dictation` với model có `IsSentenceMode = true`.

Sửa `DictationCheck` để nhận thêm `source` từ request và truyền vào service dưới dạng `DictationSource`.

### ViewModel

Thêm vào `DictationStudyViewModel`:

```csharp
public bool IsSentenceMode { get; set; }
public string Source { get; set; } = "Term"; // Term | Definition | ExampleSentence
```

## View

### `Views/Study/Index.cshtml`

Thêm thẻ chế độ học:

```html
<a href="/Study/@setId/Dictation/Sentence" class="text-decoration-none">
    <div class="card-custom text-center py-5">
        <i class="ph ph-chat-text" style="font-size: 3rem;"></i>
        <h5 class="mt-3 mb-1" style="font-weight: 600;">Nghe chép câu</h5>
        <p style="color: #787774; font-size: 0.875rem; margin: 0;">Nghe và viết lại câu ví dụ</p>
    </div>
</a>
```

### `Views/Study/Dictation.cshtml`

- Eyebrow: `Nghe chép câu` khi `IsSentenceMode`, ngược lại `Nghe chép chính tả`.
- Card title: `Bài tập nghe chép câu` / `Bài tập nghe chép`.
- Input label: `Điền lại câu ví dụ bạn nghe được` / `Điền lại nội dung bạn nghe được`.
- Serializing `Model.Cards` thêm `ExampleSentence`.
- Truyền `isSentenceMode` và `source` vào JS.
- JS `playCurrentTerm` đổi tên thành `playCurrentPrompt`, chọn `Term` hoặc `ExampleSentence` theo `source`.
- `submitCheck` gửi thêm param `source`.
- Drawer: ẩn nhóm “Chế độ trả lờ” và “Tùy chọn trả lờ” khi `IsSentenceMode`.

### `Views/Study/DictationResult.cshtml`

Khi phiên có `Mode == SentenceDictation`, hiển thị thêm câu ví dụ trong mỗi wrong-card chip.

## Luồng màn hình

1. **Trang chọn chế độ `/Study/{setId}`**: thêm nút **Nghe chép câu**.
2. **Màn hình học `/Study/{setId}/Dictation/Sentence`**: giao diện giống Nghe chép nhưng phát và kiểm tra câu ví dụ.
3. **Màn hình tổng kết `/Study/{setId}/Dictation/Result/{sessionId}`**: hiển thị %, số câu đúng, danh sách câu sai kèm term + câu ví dụ.

## Xử lý lỗi và trường hợp biên

| Trường hợp | Hành vi |
|------------|---------|
| Bộ từ không có thẻ nào có `ExampleSentence` | Redirect về `/Study/{setId}` với message “Không có câu ví dụ để học.” |
| Bộ lọc rỗng | Giữ nguyên hành vi hiện tại: message + nút Xóa bộ lọc. |
| `source` không hợp lệ trong request check | Mặc định `Term`. |
| Trình duyệt không hỗ trợ TTS | Vô hiệu hóa nút phát âm. |

## Kiểm thử

### Unit test `DictationService`

- `CheckAnswerAsync_ExampleSentence_CorrectAnswer_ReturnsTrue`
- `CheckAnswerAsync_ExampleSentence_WrongAnswer_ReturnsFalse`
- `CreateSessionAsync_SentenceMode_SetsModeCorrectly`
- `GetCardsForDictationAsync_SentenceMode_ExcludesCardsWithoutExampleSentence`

### Controller integration test

- `SentenceDictation_Get_ReturnsViewWithModel`
- `SentenceDictationCheck_Post_CorrectExample_ReturnsSuccess`
- `SentenceDictation_EmptyExamples_RedirectsToIndex`

### Manual test

- Nút **Nghe chép câu** hiển thị trên index.
- Phát âm đúng câu ví dụ.
- Nhập đúng câu ví dụ → đúng.
- Nhập sai → hiện đáp án đúng là câu ví dụ.
- Hoàn thành phiên → result page hiển thị câu sai kèm ví dụ.
- Settings drawer ở sentence mode ẩn “Chế độ trả lờ” và “Tùy chọn trả lờ”.
