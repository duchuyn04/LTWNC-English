using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class FlashcardRepository : IFlashcardRepository
{
    private readonly AppDbContext _context;

    public FlashcardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Flashcard>> GetBySetIdAsync(int setId)
    {
        return await _context.Flashcards
            .Where(f => f.FlashcardSetId == setId)
            .OrderBy(f => f.OrderIndex)
            .ToListAsync();
    }

    public async Task<Flashcard?> GetByIdAsync(int id)
    {
        return await _context.Flashcards.FindAsync(id);
    }

    public async Task AddAsync(Flashcard card)
    {
        await _context.Flashcards.AddAsync(card);
    }

    public void Update(Flashcard card)
    {
        _context.Flashcards.Update(card);
    }

    public void Delete(Flashcard card)
    {
        _context.Flashcards.Remove(card);
    }
}
