using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Repository truy xuất bảng StudySessions và UserProgresses — sử dụng EF Core
public class StudySessionRepository : IStudySessionRepository
{
    private readonly AppDbContext _context;

    // Inject DbContext để truy cập database
    public StudySessionRepository(AppDbContext context)
    {
        _context = context;
    }

    // Thêm phiên học mới và lưu ngay vào database
    public async Task AddAsync(StudySession session)
    {
        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    // Lấy tiến trình học của một người dùng cho một thẻ cụ thể
    // Tìm theo cặp (userId, flashcardId) — trả về null nếu chưa có
    public async Task<UserProgress?> GetProgressAsync(string userId, int flashcardId)
    {
        return await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);
    }

    // Lấy tất cả tiến trình học của một người dùng trong một bộ thẻ
    // Join qua bảng Flashcards để lọc theo FlashcardSetId
    public async Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId)
    {
        return await _context.UserProgresses
            .Where(p => p.UserId == userId && p.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
    }

    // Đánh dấu tiến trình cần cập nhật (cần gọi SaveChangesAsync sau)
    public void UpdateProgress(UserProgress progress)
    {
        _context.UserProgresses.Update(progress);
    }

    // Thêm tiến trình học mới vào DbSet (chưa lưu vào DB)
    public async Task AddProgressAsync(UserProgress progress)
    {
        await _context.UserProgresses.AddAsync(progress);
    }
}
