using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.StudyEvents;

// ============================================================
// OBSERVER cụ thể #1: "người theo dõi thành tích".
//
// Vai trò trong mẫu Observer:
// - Không phát tin, chỉ LẮNG NGHE tin từ StudyEventPublisher.
// - Mỗi khi có sự kiện học phù hợp, kiểm tra xem user đã đủ điều kiện
//   nhận huy hiệu chưa; nếu đủ và chưa có thì ghi vào database.
//
// StudyService không biết class này tồn tại — chỉ cần Publish sự kiện.
// ============================================================
public class AchievementStudyObserver : IStudyEventObserver
{
    private readonly AppDbContext _context;

    public AchievementStudyObserver(AppDbContext context)
    {
        _context = context;
    }

    // Cửa vào duy nhất: nhận mẩu tin và quyết định có mở huy hiệu nào không
    public async Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        switch (studyEvent)
        {
            // User vừa đánh dấu thẻ đã thuộc / chưa thuộc
            case CardProgressChangedEvent progress when progress.IsLearned:
                await TryUnlockFirstCardAndTenCardsAsync(progress.UserId, cancellationToken);
                break;

            // User trả lời đúng một câu nghe chép → thẻ được coi là đã thuộc
            case DictationAnswerCheckedEvent answer when answer.IsCorrect:
                await TryUnlockFirstCardAndTenCardsAsync(answer.UserId, cancellationToken);
                break;

            // User vừa bấm hoàn thành một buổi học
            case StudySessionCompletedEvent session:
                await TryUnlockSessionAchievementsAsync(session, cancellationToken);
                break;
        }
    }

    // Mở "thẻ đầu tiên" và/hoặc "thuộc 10 thẻ" tùy số thẻ đã thuộc hiện tại
    private async Task TryUnlockFirstCardAndTenCardsAsync(string userId, CancellationToken cancellationToken)
    {
        // Đếm bao nhiêu thẻ user này đã thuộc trong toàn bộ app
        var masteredCount = await _context.UserProgresses
            .CountAsync(p => p.UserId == userId && p.IsLearned, cancellationToken);

        if (masteredCount >= 1)
        {
            await TryUnlockAsync(userId, AchievementCatalog.FirstCardMastered, cancellationToken);
        }

        if (masteredCount >= 10)
        {
            await TryUnlockAsync(userId, AchievementCatalog.CardsMastered10, cancellationToken);
        }
    }

    // Mở huy hiệu theo loại buổi học (Flashcard / Dictation / điểm 100)
    private async Task TryUnlockSessionAchievementsAsync(
        StudySessionCompletedEvent session,
        CancellationToken cancellationToken)
    {
        if (session.Mode == StudyMode.Flashcard)
        {
            await TryUnlockAsync(session.UserId, AchievementCatalog.FirstFlashcardSession, cancellationToken);
        }

        if (session.Mode == StudyMode.Dictation)
        {
            await TryUnlockAsync(session.UserId, AchievementCatalog.FirstDictationSession, cancellationToken);

            // Điểm 100 = buổi nghe chép hoàn hảo
            if (session.Score == 100)
            {
                await TryUnlockAsync(session.UserId, AchievementCatalog.DictationPerfectSession, cancellationToken);
            }
        }
    }

    // Ghi một huy hiệu cho user nếu chưa có; bỏ qua nếu đã mở rồi
    private async Task TryUnlockAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var definition = AchievementCatalog.Find(code);
        if (definition == null)
            return;

        // Đã có huy hiệu này rồi thì không tạo thêm (tránh spam)
        var alreadyUnlocked = await _context.UserAchievements
            .AnyAsync(a => a.UserId == userId && a.Code == code, cancellationToken);
        if (alreadyUnlocked)
            return;

        _context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            Code = definition.Code,
            Title = definition.Title,
            Description = definition.Description,
            UnlockedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
