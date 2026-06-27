using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Interface định nghĩa các phương thức truy xuất bảng Flashcards
public interface IFlashcardRepository
{
    // Lấy danh sách thẻ theo bộ (sắp xếp theo OrderIndex)
    Task<List<Flashcard>> GetBySetIdAsync(int setId, bool starredOnly = false);

    // Lấy thẻ theo id
    Task<Flashcard?> GetByIdAsync(int id);

    // Thêm thẻ mới vào database
    Task AddAsync(Flashcard card);

    // Đánh dấu thẻ cần cập nhật
    void Update(Flashcard card);

    // Đánh dấu thẻ cần xóa
    void Delete(Flashcard card);
}
