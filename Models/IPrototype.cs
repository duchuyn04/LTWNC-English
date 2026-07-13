namespace ltwnc.Models;

// Prototype: entity tự Clone nội dung độc lập.
// Caller load đủ navigation trước (ví dụ Flashcards). Clone không gán owner/lineage.
public interface IPrototype<T> where T : class
{
    // Bản object mới; Id/FK identity thường để 0/null cho EF gán
    T Clone();
}
