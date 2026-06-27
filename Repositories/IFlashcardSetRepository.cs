using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Interface định nghĩa các phương thức truy xuất bảng FlashcardSets
public interface IFlashcardSetRepository
{
    // Lấy danh sách bộ thẻ theo người dùng (sắp xếp mới nhất trước)
    Task<List<FlashcardSet>> GetByUserIdAsync(string userId);

    // Lấy danh sách bộ thẻ public (tối đa 20 bộ)
    Task<List<FlashcardSet>> GetPublicSetsAsync();

    // Tìm kiếm bộ thẻ public theo tiêu đề (tối đa 20 kết quả)
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);

    // Lấy bộ thẻ theo id (không kèm danh sách thẻ)
    Task<FlashcardSet?> GetByIdAsync(int id);

    // Lấy bộ thẻ theo id kèm danh sách thẻ (dùng Include + OrderBy)
    Task<FlashcardSet?> GetByIdWithCardsAsync(int id);

    // Thêm bộ thẻ mới vào database
    Task AddAsync(FlashcardSet set);

    // Đánh dấu bộ thẻ cần cập nhật (cần gọi SaveChangesAsync sau)
    void Update(FlashcardSet set);

    // Đánh dấu bộ thẻ cần xóa (cần gọi SaveChangesAsync sau)
    void Delete(FlashcardSet set);

    // Lưu tất cả thay đổi vào database
    Task SaveChangesAsync();

    // Xóa phiên học liên quan đến một bộ thẻ
    Task DeleteSessionsBySetIdAsync(int setId);
}
