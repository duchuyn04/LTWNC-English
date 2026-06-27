using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Repository truy xuất bảng FlashcardSets — sử dụng EF Core
public class FlashcardSetRepository : IFlashcardSetRepository
{
    private readonly AppDbContext _context;

    // Inject DbContext để truy cập database
    public FlashcardSetRepository(AppDbContext context)
    {
        _context = context;
    }

    // Lấy tất cả bộ thẻ của một người dùng, sắp xếp theo thời gian cập nhật giảm dần
    public async Task<List<FlashcardSet>> GetByUserIdAsync(string userId)
    {
        return await _context.FlashcardSets
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    // Lấy 20 bộ thẻ public mới nhất
    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    // Tìm kiếm bộ thẻ public theo tiêu đề (chứa từ khóa)
    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic && s.Title.Contains(query))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    // Tìm bộ thẻ theo id — trả về null nếu không tìm thấy
    public async Task<FlashcardSet?> GetByIdAsync(int id)
    {
        return await _context.FlashcardSets.FindAsync(id);
    }

    // Tìm bộ thẻ theo id kèm danh sách thẻ (dùng Include để load quan hệ)
    // Flashcards được sắp xếp theo OrderIndex
    public async Task<FlashcardSet?> GetByIdWithCardsAsync(int id)
    {
        return await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    // Thêm bộ thẻ mới vào DbSet (chưa lưu vào DB)
    public async Task AddAsync(FlashcardSet set)
    {
        await _context.FlashcardSets.AddAsync(set);
    }

    // Đánh dấu bộ thẻ cần cập nhật
    public void Update(FlashcardSet set)
    {
        _context.FlashcardSets.Update(set);
    }

    // Đánh dấu bộ thẻ cần xóa
    public void Delete(FlashcardSet set)
    {
        _context.FlashcardSets.Remove(set);
    }

    // Lưu tất cả thay đổi vào database (INSERT, UPDATE, DELETE)
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // Xóa phiên học trước khi xóa bộ thẻ để tránh lỗi khóa ngoại Restrict
    public async Task DeleteSessionsBySetIdAsync(int setId)
    {
        await _context.StudySessions
            .Where(s => s.FlashcardSetId == setId)
            .ExecuteDeleteAsync();
    }
}
