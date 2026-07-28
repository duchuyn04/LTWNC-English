using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.StudyModes;
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services.Study;

// Nghiệp vụ học: settings, tiến độ thẻ, Study Hub, phát sự kiện Observer.
// Không lọc thẻ trong service (giao strategy). Không tự mở huy hiệu.
public class StudyService : IStudyService
{
    // Progress, settings, session, flashcard set
    private readonly AppDbContext _context;

    // Các strategy đăng ký DI (dùng liệt kê mode trên hub)
    private readonly IEnumerable<IStudyModeStrategy> _strategies;

    // Resolve đúng strategy theo StudyMode
    private readonly IStudyModeStrategyResolver _strategyResolver;

    // Subject Observer: publish sau khi Save progress / session
    private readonly IStudyEventPublisher _studyEvents;
    private readonly TimeProvider _timeProvider;

    // Inject DbContext, strategy, resolver, publisher
    public StudyService(
        AppDbContext context,
        IEnumerable<IStudyModeStrategy> strategies,
        IStudyModeStrategyResolver strategyResolver,
        IStudyEventPublisher studyEvents,
        TimeProvider? timeProvider = null)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_strategies` để các phương thức khác sử dụng.
        _strategies = strategies;
        // 3. Lưu dependency `_strategyResolver` để các phương thức khác sử dụng.
        _strategyResolver = strategyResolver;
        // 4. Lưu dependency `_studyEvents` để các phương thức khác sử dụng.
        _studyEvents = studyEvents;
        // 5. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Lấy danh sách thẻ cho một chế độ học cụ thể.
    // Controller gọi method này thay vì tự query hoặc tự resolve strategy.
    public async Task<List<Flashcard>> GetCardsForModeAsync(
        StudyMode mode,
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        // 1. Gọi `Resolve` và lưu kết quả vào `strategy`.
        IStudyModeStrategy strategy = _strategyResolver.Resolve(mode);
        // 2. Gọi `GetCardsAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await strategy.GetCardsAsync(setId, settings, userId);
        // 3. Trả `cards` cho nơi gọi.
        return cards;
    }

    // Lấy tiến trình học của user cho từng thẻ trong bộ, dùng để hiển thị trạng thái đã biết/chưa biết
    public async Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(userId))
        {
            // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new Dictionary<int, UserProgress>();
        }

        // 3. Gọi `ToDictionaryAsync` và lưu kết quả vào `progressByCardId`.
        Dictionary<int, UserProgress> progressByCardId = await _context.UserProgresses
            .Where(progress =>
                progress.UserId == userId
                && progress.Flashcard != null
                && progress.Flashcard.FlashcardSetId == setId)
            .ToDictionaryAsync(progress => progress.FlashcardId);

        // 4. Trả `progressByCardId` cho nơi gọi.
        return progressByCardId;
    }

    // Lấy settings của user; nếu chưa có thì trả về settings mặc định
    public async Task<UserStudySettings> GetSettingsAsync(string? userId)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(userId))
        {
            // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new UserStudySettings();
        }

        // 3. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `settings`.
        UserStudySettings? settings = await _context.UserStudySettings
            .FirstOrDefaultAsync(row => row.UserId == userId);

        // 4. Kiểm tra `settings == null` để chọn nhánh xử lý phù hợp.
        if (settings == null)
        {
            // 5. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new UserStudySettings { UserId = userId };
        }

        // 6. Trả `settings` cho nơi gọi.
        return settings;
    }

    // Lưu toàn bộ settings học tập của user
    public async Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `settings`.
        UserStudySettings? settings = await _context.UserStudySettings
            .FirstOrDefaultAsync(row => row.UserId == userId);

        // 2. Kiểm tra `settings == null` để chọn nhánh xử lý phù hợp.
        if (settings == null)
        {
            // 3. Cập nhật `settings` bằng giá trị mới.
            settings = new UserStudySettings { UserId = userId };
            // 4. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
            await _context.UserStudySettings.AddAsync(settings);
        }

        // Cập nhật bộ lọc và cài đặt hiển thị flashcard
        // 5. Cập nhật `settings.StarredOnly` bằng giá trị mới.
        settings.StarredOnly = input.StarredOnly;
        // 6. Cập nhật `settings.UnlearnedOnly` bằng giá trị mới.
        settings.UnlearnedOnly = input.UnlearnedOnly;
        // 7. Cập nhật `settings.ShowFrontTerm` bằng giá trị mới.
        settings.ShowFrontTerm = input.ShowFrontTerm;
        // 8. Cập nhật `settings.ShowFrontDefinition` bằng giá trị mới.
        settings.ShowFrontDefinition = input.ShowFrontDefinition;
        // 9. Cập nhật `settings.ShowFrontIpa` bằng giá trị mới.
        settings.ShowFrontIpa = input.ShowFrontIpa;
        // 10. Cập nhật `settings.ShowFrontImage` bằng giá trị mới.
        settings.ShowFrontImage = input.ShowFrontImage;
        // 11. Cập nhật `settings.ShowBackTerm` bằng giá trị mới.
        settings.ShowBackTerm = input.ShowBackTerm;
        // 12. Cập nhật `settings.ShowBackDefinition` bằng giá trị mới.
        settings.ShowBackDefinition = input.ShowBackDefinition;
        // 13. Cập nhật `settings.ShowBackIpa` bằng giá trị mới.
        settings.ShowBackIpa = input.ShowBackIpa;
        // 14. Cập nhật `settings.ShowBackExample` bằng giá trị mới.
        settings.ShowBackExample = input.ShowBackExample;
        // 15. Cập nhật `settings.ShowBackImage` bằng giá trị mới.
        settings.ShowBackImage = input.ShowBackImage;
        // 16. Cập nhật `settings.HideImage` bằng giá trị mới.
        settings.HideImage = input.HideImage;
        // 17. Cập nhật `settings.BlurImage` bằng giá trị mới.
        settings.BlurImage = input.BlurImage;
        // 18. Cập nhật `settings.LargeImage` bằng giá trị mới.
        settings.LargeImage = input.LargeImage;
        // 19. Cập nhật `settings.PronounceFront` bằng giá trị mới.
        settings.PronounceFront = input.PronounceFront;
        // 20. Cập nhật `settings.PronounceBack` bằng giá trị mới.
        settings.PronounceBack = input.PronounceBack;

        // Cập nhật cài đặt riêng của Dictation
        // 21. Cập nhật `settings.DictationContentMode` bằng giá trị mới.
        settings.DictationContentMode = input.DictationContentMode;
        // 22. Cập nhật `settings.DictationAnswerMode` bằng giá trị mới.
        settings.DictationAnswerMode = input.DictationAnswerMode;
        // 23. Cập nhật `settings.DictationAutoAdvance` bằng giá trị mới.
        settings.DictationAutoAdvance = input.DictationAutoAdvance;
        // 24. Cập nhật `settings.DictationPlaybackSpeed` bằng giá trị mới.
        settings.DictationPlaybackSpeed = input.DictationPlaybackSpeed;
        // 25. Cập nhật `settings.DictationVoiceUri` bằng giá trị mới.
        settings.DictationVoiceUri = input.DictationVoiceUri;
        // 26. Cập nhật `settings.DictationShowHint` bằng giá trị mới.
        settings.DictationShowHint = input.DictationShowHint;
        // 27. Cập nhật `settings.DictationAcceptSynonyms` bằng giá trị mới.
        settings.DictationAcceptSynonyms = input.DictationAcceptSynonyms;
        // 28. Cập nhật `settings.DictationShuffle` bằng giá trị mới.
        settings.DictationShuffle = input.DictationShuffle;

        // 29. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 30. Trả `settings` cho nơi gọi.
        return settings;
    }

    // Cập nhật nhanh hai bộ lọc StarredOnly/UnlearnedOnly từ query string trên URL
    public async Task SaveFilterSettingsAsync(string userId, bool? starredOnly, bool? unlearnedOnly)
    {
        // 1. Gọi `GetSettingsAsync` và lưu kết quả vào `settings`.
        UserStudySettings settings = await GetSettingsAsync(userId);

        // 2. Kiểm tra `starredOnly.HasValue` để chọn nhánh xử lý phù hợp.
        if (starredOnly.HasValue)
        {
            // 3. Cập nhật `settings.StarredOnly` bằng giá trị mới.
            settings.StarredOnly = starredOnly.Value;
        }

        // 4. Kiểm tra `unlearnedOnly.HasValue` để chọn nhánh xử lý phù hợp.
        if (unlearnedOnly.HasValue)
        {
            // 5. Cập nhật `settings.UnlearnedOnly` bằng giá trị mới.
            settings.UnlearnedOnly = unlearnedOnly.Value;
        }

        // 6. Gọi `SaveSettingsAsync` để thực hiện bước nghiệp vụ này.
        await SaveSettingsAsync(userId, settings);
    }

    // Đánh dấu một thẻ là đã biết hoặc chưa biết
    public async Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        // 4. Tính giá trị và lưu vào `canStudyAsPublic` để dùng ở bước tiếp theo.
        bool canStudyAsPublic =
            set.IsPublic
            && set.ModerationStatus == FlashcardSetModerationStatus.Active;
        // 5. Kiểm tra `!canStudyAsPublic && set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (!canStudyAsPublic && set.UserId != userId)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền học bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        // 7. Gọi `FindAsync` và lưu kết quả vào `card`.
        Flashcard? card = await _context.Flashcards.FindAsync(flashcardId);
        // 8. Kiểm tra `card == null || card.FlashcardSetId != setId` để chọn nhánh xử lý phù hợp.
        if (card == null || card.FlashcardSetId != setId)
        {
            // 9. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.")`.
            throw new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.");
        }

        // 10. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `progress`.
        UserProgress? progress = await _context.UserProgresses
            .FirstOrDefaultAsync(row => row.UserId == userId && row.FlashcardId == flashcardId);

        // 11. Kiểm tra `progress == null` để chọn nhánh xử lý phù hợp.
        if (progress == null)
        {
            // 12. Cập nhật `progress` bằng giá trị mới.
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            // 13. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
            await _context.UserProgresses.AddAsync(progress);
        }

        // 14. Cập nhật `progress.IsLearned` bằng giá trị mới.
        progress.IsLearned = learned;

        // 15. Kiểm tra `learned` để chọn nhánh xử lý phù hợp.
        if (learned)
        {
            // 16. Cập nhật `progress.Status` bằng giá trị mới.
            progress.Status = UserProgressStatus.Mastered;
            // 17. Cập nhật bộ đếm hoặc trạng thái `progress.CorrectCount`.
            progress.CorrectCount++;
        }
        else
        {
            // 18. Cập nhật `progress.Status` bằng giá trị mới.
            progress.Status = UserProgressStatus.Learning;
            // 19. Cập nhật bộ đếm hoặc trạng thái `progress.WrongCount`.
            progress.WrongCount++;
        }

        // 20. Cập nhật `progress.LastReviewed` bằng giá trị mới.
        progress.LastReviewed = DateTime.UtcNow;

        // Lưu tiến độ xong trước; observer đọc DB sẽ thấy dữ liệu mới
        // 21. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        // Báo cho tất cả "người theo dõi" biết user vừa cập nhật một thẻ
        // (ví dụ: mở huy hiệu "thẻ đầu tiên đã thuộc", ghi log hệ thống)
        // 22. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
        await _studyEvents.PublishAsync(new CardProgressChangedEvent(
            UserId: userId,
            OccurredAtUtc: DateTime.UtcNow,
            SetId: setId,
            FlashcardId: flashcardId,
            IsLearned: learned,
            Status: progress.Status));
    }

    public async Task<StudySession> StartSessionAsync(string userId, int setId, StudyMode mode)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        // 4. Tính giá trị và lưu vào `canStudyAsPublic` để dùng ở bước tiếp theo.
        bool canStudyAsPublic =
            set.IsPublic
            && set.ModerationStatus == FlashcardSetModerationStatus.Active;
        // 5. Kiểm tra `!canStudyAsPublic && set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (!canStudyAsPublic && set.UserId != userId)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền học bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        // 7. Khởi tạo `session` với dữ liệu ban đầu cần thiết.
        StudySession session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        // 8. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
        await _context.StudySessions.AddAsync(session);
        // 9. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        // 10. Trả `session` cho nơi gọi.
        return session;
    }

    // Ghi nhận hoàn thành một phiên học; thao tác lặp lại không publish/cộng thêm.
    public async Task CompleteSessionAsync(string userId, int setId, int sessionId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions.FindAsync(sessionId);
        // 2. Kiểm tra `session == null` để chọn nhánh xử lý phù hợp.
        if (session == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên học không tồn tại.")`.
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        // 4. Kiểm tra `session.UserId != userId || session.FlashcardSetId != setId || sess...` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Flashcard)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền hoàn tất phiên học ...`.
            throw new UnauthorizedAccessException("Không có quyền hoàn tất phiên học này.");
        }

        // 6. Kiểm tra `session.CompletedAt.HasValue` để chọn nhánh xử lý phù hợp.
        if (session.CompletedAt.HasValue)
        {
            // 7. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 8. Tính giá trị và lưu vào `completedAt` để dùng ở bước tiếp theo.
        DateTime completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        // 9. Cập nhật `session.DurationSeconds` bằng giá trị mới.
        session.DurationSeconds = StudySessionTiming.CalculateDurationSeconds(
            session.StartedAt,
            completedAt);
        // 10. Cập nhật `session.CompletedAt` bằng giá trị mới.
        session.CompletedAt = completedAt;
        // 11. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        // Báo buổi học đã xong; observer có thể mở huy hiệu "buổi Flashcard đầu tiên"...
        // 12. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            UserId: session.UserId,
            OccurredAtUtc: completedAt,
            SetId: session.FlashcardSetId,
            SessionId: session.Id,
            Mode: session.Mode,
            Score: session.Score));
    }

    // Lấy dữ liệu cho Study Hub (trang chọn chế độ học).
    // Mỗi strategy tự quyết định thẻ khả dụng và tự xây dựng option hiển thị.
    public async Task<StudyModeSelectorViewModel> GetStudyModeSelectorDataAsync(int setId, string? userId)
    {
        // Thông tin cơ bản của bộ thẻ
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        // 2. Gọi `ToListAsync` và lưu kết quả vào `allCards`.
        List<Flashcard> allCards = await _context.Flashcards
            .Where(flashcard => flashcard.FlashcardSetId == setId)
            .ToListAsync();

        // Tiến trình học của user cho các thẻ trong bộ
        // 3. Khai báo `progresses` để lưu dữ liệu dùng ở các bước sau.
        Dictionary<int, UserProgress> progresses;
        // 4. Kiểm tra `string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(userId))
        {
            // 5. Cập nhật `progresses` bằng giá trị mới.
            progresses = new Dictionary<int, UserProgress>();
        }
        else
        {
            // 6. Gọi `ToList` và lưu kết quả vào `cardIds`.
            List<int> cardIds = allCards.Select(flashcard => flashcard.Id).ToList();

            // 7. Cập nhật `progresses` bằng giá trị mới.
            progresses = await _context.UserProgresses
                .Where(progress =>
                    progress.UserId == userId
                    && cardIds.Contains(progress.FlashcardId))
                .ToDictionaryAsync(progress => progress.FlashcardId);
        }

        // Thống kê hiển thị trên Study Hub
        // 8. Tính giá trị và lưu vào `learnedCount` để dùng ở bước tiếp theo.
        int learnedCount = 0;
        // 9. Tính giá trị và lưu vào `starredCount` để dùng ở bước tiếp theo.
        int starredCount = 0;

        // 10. Duyệt từng `card` trong `allCards` để xử lý lần lượt.
        foreach (Flashcard card in allCards)
        {
            // 11. Kiểm tra `progresses.TryGetValue(card.Id, out UserProgress? progress) && prog...` để chọn nhánh xử lý phù hợp.
            if (progresses.TryGetValue(card.Id, out UserProgress? progress) && progress.IsLearned)
            {
                // 12. Cập nhật bộ đếm hoặc trạng thái `learnedCount`.
                learnedCount++;
            }

            // 13. Kiểm tra `card.IsStarred` để chọn nhánh xử lý phù hợp.
            if (card.IsStarred)
            {
                // 14. Cập nhật bộ đếm hoặc trạng thái `starredCount`.
                starredCount++;
            }
        }

        // 15. Tính giá trị và lưu vào `masteryPercent` để dùng ở bước tiếp theo.
        int masteryPercent = 0;
        // 16. Kiểm tra `allCards.Count > 0` để chọn nhánh xử lý phù hợp.
        if (allCards.Count > 0)
        {
            // 17. Cập nhật `masteryPercent` bằng giá trị mới.
            masteryPercent = learnedCount * 100 / allCards.Count;
        }

        // Số phiên học trong 7 ngày gần nhất
        // 18. Gọi `AddDays` và lưu kết quả vào `recentCutoff`.
        DateTime recentCutoff = DateTime.UtcNow.AddDays(-7);
        // 19. Tính giá trị và lưu vào `recentSessionCount` để dùng ở bước tiếp theo.
        int recentSessionCount = 0;

        // 20. Kiểm tra `!string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(userId))
        {
            // 21. Cập nhật `recentSessionCount` bằng giá trị mới.
            recentSessionCount = await _context.StudySessions.CountAsync(session =>
                session.UserId == userId
                && session.FlashcardSetId == setId
                && session.CompletedAt.HasValue
                && session.CompletedAt.Value >= recentCutoff);
        }

        // 22. Gọi `GetSettingsAsync` và lưu kết quả vào `settings`.
        UserStudySettings settings = await GetSettingsAsync(userId);

        // Xây dựng danh sách mode khả dụng từ các strategy đã đăng ký.
        // Duyệt theo từng mode duy nhất và resolve qua resolver để đảm bảo tính duy nhất.
        // 23. Gọi `ToList` và lưu kết quả vào `registeredModes`.
        List<StudyMode> registeredModes = _strategies
            .Select(strategy => strategy.Mode)
            .Distinct()
            .OrderBy(mode => (int)mode)
            .ToList();

        // 24. Khởi tạo `modes` với dữ liệu ban đầu cần thiết.
        List<StudyModeOptionViewModel> modes = new();
        // 25. Duyệt từng `mode` trong `registeredModes` để xử lý lần lượt.
        foreach (StudyMode mode in registeredModes)
        {
            // 26. Gọi `Resolve` và lưu kết quả vào `strategy`.
            IStudyModeStrategy strategy = _strategyResolver.Resolve(mode);
            // 27. Gọi `GetCardsAsync` và lưu kết quả vào `cardsForMode`.
            List<Flashcard> cardsForMode = await strategy.GetCardsAsync(setId, settings, userId);
            // 28. Gọi `BuildOptionAsync` và lưu kết quả vào `option`.
            StudyModeOptionViewModel option = await strategy.BuildOptionAsync(
                setId,
                cardsForMode,
                settings,
                userId);
            // 29. Gọi `Add` để thực hiện bước nghiệp vụ này.
            modes.Add(option);
        }

        // Xác định mode được đề xuất dựa trên mastery và khả năng thực tế của Dictation
        // 30. Khởi tạo `warnings` với dữ liệu ban đầu cần thiết.
        List<string> warnings = new();
        // 31. Gọi `DetermineRecommendedMode` và lưu kết quả vào `recommendedMode`.
        StudyMode recommendedMode = DetermineRecommendedMode(masteryPercent, modes);

        // Nếu mode đề xuất không khả dụng, chuyển sang mode khả dụng đầu tiên và cảnh báo user
        // 32. Gọi `Any` và lưu kết quả vào `recommendedIsAvailable`.
        bool recommendedIsAvailable = modes.Any(option =>
            option.Mode == recommendedMode && option.IsAvailable);

        // 33. Kiểm tra `!recommendedIsAvailable` để chọn nhánh xử lý phù hợp.
        if (!recommendedIsAvailable)
        {
            // 34. Gọi `FirstOrDefault` và lưu kết quả vào `fallback`.
            StudyModeOptionViewModel? fallback = modes.FirstOrDefault(option => option.IsAvailable);

            // 35. Kiểm tra `fallback != null` để chọn nhánh xử lý phù hợp.
            if (fallback != null)
            {
                // 36. Gọi `FirstOrDefault` và lưu kết quả vào `recommendedOption`.
                StudyModeOptionViewModel? recommendedOption =
                    modes.FirstOrDefault(option => option.Mode == recommendedMode);

                // 37. Khai báo `recommendedName` để lưu dữ liệu dùng ở các bước sau.
                string recommendedName;
                // 38. Kiểm tra `recommendedOption != null` để chọn nhánh xử lý phù hợp.
                if (recommendedOption != null)
                {
                    // 39. Cập nhật `recommendedName` bằng giá trị mới.
                    recommendedName = recommendedOption.Name;
                }
                else
                {
                    // 40. Cập nhật `recommendedName` bằng giá trị mới.
                    recommendedName = recommendedMode.ToString();
                }

                // 41. Gọi `Add` để thực hiện bước nghiệp vụ này.
                warnings.Add(
                    $"{recommendedName} không khả dụng với bộ lọc hiện tại. Đã chuyển sang {fallback.Name}.");
                // 42. Cập nhật `recommendedMode` bằng giá trị mới.
                recommendedMode = fallback.Mode;
            }
            else
            {
                // 43. Gọi `Add` để thực hiện bước nghiệp vụ này.
                warnings.Add(
                    "Không có thẻ phù hợp với bộ lọc hiện tại. Hãy điều chỉnh bộ lọc hoặc thêm thẻ mới.");
            }
        }

        // 44. Gọi `MarkRecommended` để thực hiện bước nghiệp vụ này.
        MarkRecommended(modes, recommendedMode);

        // Roadmap: chỉ hiển thị các mode chưa có strategy thật đăng ký
        // 45. Gọi `ToHashSet` và lưu kết quả vào `activeModes`.
        HashSet<StudyMode> activeModes = modes.Select(option => option.Mode).ToHashSet();

        // 46. Tính giá trị và lưu vào `plannedRoadmapModes` để dùng ở bước tiếp theo.
        StudyMode[] plannedRoadmapModes =
        {
            StudyMode.Quiz,
            StudyMode.Write,
            StudyMode.Match
        };

        // 47. Khởi tạo `roadmapModes` với dữ liệu ban đầu cần thiết.
        List<StudyModeOptionViewModel> roadmapModes = new();
        // 48. Duyệt từng `plannedMode` trong `plannedRoadmapModes` để xử lý lần lượt.
        foreach (StudyMode plannedMode in plannedRoadmapModes)
        {
            // 49. Kiểm tra `!activeModes.Contains(plannedMode)` để chọn nhánh xử lý phù hợp.
            if (!activeModes.Contains(plannedMode))
            {
                // 50. Gọi `Add` để thực hiện bước nghiệp vụ này.
                roadmapModes.Add(BuildRoadmapMode(plannedMode));
            }
        }

        // 51. Tính giá trị và lưu vào `setTitle` để dùng ở bước tiếp theo.
        string setTitle = string.Empty;
        // 52. Tính giá trị và lưu vào `setDescription` để dùng ở bước tiếp theo.
        string? setDescription = null;
        // 53. Kiểm tra `set != null` để chọn nhánh xử lý phù hợp.
        if (set != null)
        {
            // 54. Cập nhật `setTitle` bằng giá trị mới.
            setTitle = set.Title;
            // 55. Cập nhật `setDescription` bằng giá trị mới.
            setDescription = set.Description;
        }

        // 56. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new StudyModeSelectorViewModel
        {
            SetId = setId,
            SetTitle = setTitle,
            SetDescription = setDescription,
            TotalCards = allCards.Count,
            LearnedCount = learnedCount,
            StarredCount = starredCount,
            MasteryPercent = masteryPercent,
            RecentSessionCount = recentSessionCount,
            StarredOnly = settings.StarredOnly,
            UnlearnedOnly = settings.UnlearnedOnly,
            RecommendedMode = recommendedMode,
            Modes = modes,
            RoadmapModes = roadmapModes,
            Warnings = warnings
        };
    }

    // Tạo option cho các mode chưa triển khai (roadmap)
    private static StudyModeOptionViewModel BuildRoadmapMode(StudyMode mode)
    {
        // 1. Gọi `GetModeMetadata` và lưu kết quả vào `metadata`.
        ModeMetadata metadata = GetModeMetadata(mode);

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new StudyModeOptionViewModel
        {
            Mode = mode,
            Name = metadata.Name,
            Description = metadata.Description,
            IconClass = metadata.IconClass,
            IsAvailable = false,
            UnavailableReason = "Sắp ra mắt"
        };
    }

    // Metadata dự phòng cho các mode chưa có strategy thật (roadmap)
    private static ModeMetadata GetModeMetadata(StudyMode mode)
    {
        // 1. Phân nhánh xử lý theo giá trị `mode`.
        switch (mode)
        {
            case StudyMode.Quiz:
                // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new ModeMetadata("Trắc nghiệm", "Chọn đáp án đúng", "ph-question", 30);
            case StudyMode.Write:
                // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new ModeMetadata("Viết chính tả", "Viết lại từ từ gợi ý", "ph-pencil-simple", 30);
            case StudyMode.Match:
                // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new ModeMetadata("Ghép đôi", "Ghép từ với nghĩa", "ph-shuffle", 30);
            default:
                // 5. Tạo và trả đối tượng kết quả cho nơi gọi.
                return new ModeMetadata(mode.ToString(), string.Empty, "ph-question", 30);
        }
    }

    // Metadata tạm cho mode roadmap (chưa có strategy thật)
    private sealed record ModeMetadata(string Name, string Description, string IconClass, int SecondsPerCard);

    // Đề xuất Dictation khi user đã thuộc >= 50% thẻ VÀ Dictation đang khả dụng với settings hiện tại
    private static StudyMode DetermineRecommendedMode(
        int masteryPercent,
        IReadOnlyList<StudyModeOptionViewModel> modes)
    {
        // 1. Gọi `Any` và lưu kết quả vào `dictationAvailable`.
        bool dictationAvailable = modes.Any(option =>
            option.Mode == StudyMode.Dictation && option.IsAvailable);

        // 2. Kiểm tra `masteryPercent >= 50 && dictationAvailable` để chọn nhánh xử lý phù hợp.
        if (masteryPercent >= 50 && dictationAvailable)
        {
            // 3. Trả `StudyMode.Dictation` cho nơi gọi.
            return StudyMode.Dictation;
        }

        // 4. Trả `StudyMode.Flashcard` cho nơi gọi.
        return StudyMode.Flashcard;
    }

    // Đánh dấu mode được đề xuất trên danh sách option
    private static void MarkRecommended(List<StudyModeOptionViewModel> modes, StudyMode recommended)
    {
        // 1. Duyệt từng `option` trong `modes` để xử lý lần lượt.
        foreach (StudyModeOptionViewModel option in modes)
        {
            // 2. Cập nhật `option.IsRecommended` bằng giá trị mới.
            option.IsRecommended = option.Mode == recommended;
        }
    }
}
