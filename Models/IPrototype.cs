namespace ltwnc.Models;

// Contract Prototype: entity tự tạo bản sao độc lập về nội dung.
// Caller chịu trách nhiệm object nguồn đã đủ dữ liệu cần nhân bản
// (ví dụ danh sách thẻ đã được load từ database trước khi clone bộ thẻ).
// Clone không gán ownership hay lineage; service làm việc đó sau khi Clone trả về.
public interface IPrototype<T> where T : class
{
    T Clone();
}
