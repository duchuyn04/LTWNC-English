using System.Text.Json;

namespace ltwnc.Services.CardActions;

// Giữ trạng thái đã tuần tự hóa để một card action có thể được hoàn tác.
public sealed record CardActionMemento(string StateJson)
{
    internal static T Restore<T>(CardActionMemento? memento)
    {
        if (memento == null || string.IsNullOrWhiteSpace(memento.StateJson))
        {
            throw InvalidMemento();
        }

        try
        {
            T? state = JsonSerializer.Deserialize<T>(memento.StateJson);
            return state ?? throw InvalidMemento();
        }
        catch (JsonException exception)
        {
            throw InvalidMemento(exception);
        }
    }

    internal static InvalidOperationException InvalidMemento(Exception? innerException = null)
        => new("Dữ liệu hoàn tác không hợp lệ.", innerException);
}
