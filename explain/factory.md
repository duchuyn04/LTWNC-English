# Factory Method, để concrete creator quyết định Command cần tạo

## Chỉ cần nhớ một ý

Factory Method định nghĩa điểm tạo object ở Creator và để các Concrete Creator override điểm đó.

Trong project này:

```text
"Delete" -> DeleteCardsCommand
"Star"   -> StarCardsCommand
"Unstar" -> UnstarCardsCommand
```

`CardActionCommandFactory` chọn creator theo action type; creator được chọn mới tạo command.

## Vì sao đây là Factory Method GoF?

`CardActionCommandCreator` là Creator. Method `Create()` giữ quy trình gọi chung và chuyển việc khởi tạo cho Factory Method `CreateCommand()`. Ba Concrete Creator override method này để trả về ba Concrete Product khác nhau.

`CardActionCommandFactory` chỉ làm nhiệm vụ tìm creator phù hợp từ danh sách DI. Nó không trực tiếp `new` command và không phải nơi cài đặt Factory Method.

## Trước khi có Factory, code triển khai ra sao?

Controller từng tự chọn và tạo concrete command:

```csharp
ICardActionCommand command = action switch
{
    BatchActionType.Delete => new DeleteCardsCommand(
        context, setId, userId, selectedCardIds),

    BatchActionType.Star => new StarCardsCommand(
        context, setId, userId, selectedCardIds),

    BatchActionType.Unstar => new UnstarCardsCommand(
        context, setId, userId, selectedCardIds),

    _ => throw new InvalidOperationException()
};
```

Controller phải biết tên từng class và biết constructor của chúng cần những tham số nào.

Việc hoàn tác cũng cần tạo lại command từ chuỗi `ActionType` trong log. Nếu đặt thêm một `switch` ở `CardActionService`, project sẽ có hai nơi chứa cùng quy tắc tạo object.

## Vì sao dùng Factory cho chức năng này?

Cả lúc thực hiện và lúc hoàn tác đều cần chuyển một loại hành động thành object Command.

Nếu gom quy tắc vào Factory, mọi nơi chỉ gọi:

```csharp
ICardActionCommand command = _commandFactory.Create(
    actionType,
    setId,
    userId,
    cardIds);
```

Controller không còn tự `new DeleteCardsCommand`. `CardActionService` cũng không cần lặp lại `switch` khi hoàn tác.

Có thể so sánh:

```text
Trước Factory:
Controller và service có thể phải tự biết cách new từng Command.

Sau Factory:
Cả hai yêu cầu Factory tạo ICardActionCommand.
```

## Factory nằm ở đâu trong project?

| Thành phần | Code |
| --- | --- |
| Resolver contract | [`ICardActionCommandFactory`](../Services/CardActions/ICardActionCommandFactory.cs) |
| Resolver | [`CardActionCommandFactory`](../Services/CardActions/CardActionCommandFactory.cs) |
| Creator | [`CardActionCommandCreator`](../Services/CardActions/CardActionCommandCreators.cs) |
| Concrete Creator | `DeleteCardsCommandCreator`, `StarCardsCommandCreator`, `UnstarCardsCommandCreator` |
| Sản phẩm chung | [`ICardActionCommand`](../Services/CardActions/ICardActionCommand.cs) |
| Các sản phẩm cụ thể | `DeleteCardsCommand`, `StarCardsCommand`, `UnstarCardsCommand` |
| Nơi sử dụng | [`CardActionsController`](../Controllers/CardActionsController.cs), [`CardActionService`](../Services/CardActions/CardActionService.cs) |

Creator định nghĩa Factory Method như sau:

```csharp
public ICardActionCommand Create(...)
    => CreateCommand(...);

protected abstract ICardActionCommand CreateCommand(...);
```

Mỗi Concrete Creator quyết định sản phẩm:

```csharp
protected override ICardActionCommand CreateCommand(...)
    => new StarCardsCommand(Context, setId, userId, cardIds);
```

## Vì sao Factory trả về interface?

Method trả về `ICardActionCommand`, không trả về một concrete command cụ thể.

Nhờ vậy code gọi chỉ cần biết mọi command đều có `ExecuteAsync()` và `UndoAsync()`:

```csharp
ICardActionCommand command = _commandFactory.Create(...);
await command.ExecuteAsync();
```

Dù Factory tạo `StarCardsCommand` hay `DeleteCardsCommand`, dòng gọi phía sau không đổi.

## Factory hỗ trợ Undo như thế nào?

Log chỉ lưu tên hành động như `"Delete"` hoặc `"Star"`. Nó không thể lưu nguyên object Command đang nằm trong bộ nhớ.

Khi hoàn tác, service đọc tên từ log và nhờ Factory tạo lại object:

```text
CardActionLog.ActionType = "Star"
              |
              v
CardActionCommandFactory
              |
              v
StarCardsCommand mới
```

Sau đó snapshot được nạp vào command mới trước khi gọi `UndoAsync()`.

## Khi thêm thao tác mới

Giả sử thêm `MoveCardsCommand`. Các bước chính là:

1. Tạo class `MoveCardsCommand` triển khai `ICardActionCommand`.
2. Tạo `MoveCardsCommandCreator` và override `CreateCommand()`.
3. Đăng ký creator mới vào DI và thêm giá trị hành động mà giao diện có thể gửi.

Controller không cần biết constructor của command mới.

Resolver không cần thêm nhánh lựa chọn mới vì creator được tìm từ tập implementation do DI cung cấp.

## Tự kiểm tra

1. Trước Factory, controller phải biết điều gì?
2. Vì sao quá trình Undo cũng cần Factory?
3. Factory Method nằm ở class nào?

Đáp án:

1. Tên class và cách gọi constructor của từng command.
2. Vì service phải tạo lại command từ `ActionType` đã lưu trong log.
3. `CreateCommand()` trên `CardActionCommandCreator`, được override bởi các Concrete Creator.

## Kết luận ngắn

Project dùng Factory Method GoF: resolver chọn Creator theo action type, còn mỗi Concrete Creator quyết định Concrete Command cần khởi tạo.
