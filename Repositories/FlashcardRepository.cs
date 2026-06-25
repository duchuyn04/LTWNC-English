using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Repository truy xuất bảng Flashcards — sử dụng EF Core
public class FlashcardRepository : IFlashcardRepository
{
    private readonly AppDbContext _context;

    // Inject DbContext để truy cập database
    public FlashcardRepository(AppDbContext context)
    {
        _context = context;
    }

    // Lấy tất cả thẻ trong một bộ, sắp xếp theo thứ tự hiển thị
    public async Task<List<Flashcard>> GetBySetIdAsync(int setId)
    {
        return await _context.Flashcards
            .Where(f => f.FlashcardSetId == setId)
            .OrderBy(f => f.OrderIndex)
            .ToListAsync();
    }

    // Tìm thẻ theo id — trả về null nếu không tìm thấy
    public async Task<Flashcard?> GetByIdAsync(int id)
    {
        return await _context.Flashcards.FindAsync(id);
    }

    // Thêm thẻ mới vào DbSet (chưa lưu vào DB)
    public async Task AddAsync(Flashcard card)
    {
        await _context.Flashcards.AddAsync(card);
    }

    // Đánh dấu thẻ cần cập nhật
    public void Update(Flashcard card)
    {
        _context.Flashcards.Update(card);
    }

    // Đánh dấu thẻ cần xóa
    public void Delete(Flashcard card)
    {
        _context.Flashcards.Remove(card);
    }
}
