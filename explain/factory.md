# Simple Factory, gom việc tạo Command vào một chỗ

## Chỉ cần nhớ một ý

Factory nhận tên một loại object rồi tạo đúng object đó.

Trong project này:

```text
"Delete" -> DeleteCardsCommand
"Star"   -> StarCardsCommand
"Unstar" -> UnstarCardsCommand
```

Class làm việc này là `CardActionCommandFactory`.

## Lưu ý về tên mẫu

Cách triển khai hiện tại là Simple Factory, không phải Factory Method chuẩn trong sách GoF.

Factory Method chuẩn thường dùng một method có thể được class con override để quyết định sản phẩm cần tạo. `CardActionCommandFactory` không có hệ thống class con như vậy. Nó dùng các nhánh `if` trong một method `Create()`.

Simple Factory vẫn hữu ích, nhưng gọi đúng tên giúp tránh nhầm khi học GoF.

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
| Contract | [`ICardActionCommandFactory`](../Services/CardActions/ICardActionCommandFactory.cs) |
| Factory | [`CardActionCommandFactory`](../Services/CardActions/CardActionCommandFactory.cs) |
| Sản phẩm chung | [`ICardActionCommand`](../Services/CardActions/ICardActionCommand.cs) |
| Các sản phẩm cụ thể | `DeleteCardsCommand`, `StarCardsCommand`, `UnstarCardsCommand` |
| Nơi sử dụng | [`CardActionsController`](../Controllers/CardActionsController.cs), [`CardActionService`](../Services/CardActions/CardActionService.cs) |

Method `Create()` hiện tại hoạt động như sau:

```csharp
if (actionType == "Delete")
{
    return new DeleteCardsCommand(_context, setId, userId, cardIds);
}

if (actionType == "Star")
{
    return new StarCardsCommand(_context, setId, userId, cardIds);
}

if (actionType == "Unstar")
{
    return new UnstarCardsCommand(_context, setId, userId, cardIds);
}

throw new InvalidOperationException();
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
2. Thêm một nhánh tạo object trong Factory.
3. Thêm giá trị hành động mà giao diện có thể gửi.

Controller không cần biết constructor của command mới.

Simple Factory vẫn phải sửa khi có sản phẩm mới. Nó không loại bỏ hoàn toàn thay đổi, chỉ gom thay đổi khởi tạo vào một chỗ.

## Tự kiểm tra

1. Trước Factory, controller phải biết điều gì?
2. Vì sao quá trình Undo cũng cần Factory?
3. Đây có phải Factory Method chuẩn GoF không?

Đáp án:

1. Tên class và cách gọi constructor của từng command.
2. Vì service phải tạo lại command từ `ActionType` đã lưu trong log.
3. Không. Đây là Simple Factory dùng một method `Create()` có các nhánh lựa chọn.

## Kết luận ngắn

`CardActionCommandFactory` là một Simple Factory. Nó gom quy tắc chuyển `Delete`, `Star`, `Unstar` thành object Command vào một chỗ để controller và service không lặp lại việc khởi tạo.
