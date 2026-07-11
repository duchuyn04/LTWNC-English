namespace ltwnc.Models;

// Contract để một entity tự tạo bản sao độc lập của chính nó.
// Dùng trong Prototype pattern, ví dụ khi sao chép bộ thẻ công khai.
public interface IPrototype<T> where T : class
{
    T Clone();
}
