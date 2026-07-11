using ltwnc.Services.StudyEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace ltwnc.Tests.StudyEvents;

// Kiểm tra trạm phát (Subject) gửi tin đúng cho mọi observer
public class StudyEventPublisherTests
{
    // Observer giả: chỉ ghi nhận đã được gọi bao nhiêu lần
    private sealed class CountingObserver : IStudyEventObserver
    {
        public int CallCount { get; private set; }
        public StudyEvent? LastEvent { get; private set; }

        public Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEvent = studyEvent;
            return Task.CompletedTask;
        }
    }

    // Observer giả: luôn ném lỗi để kiểm tra observer khác vẫn chạy
    private sealed class ThrowingObserver : IStudyEventObserver
    {
        public Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer cố tình lỗi để test.");
    }

    // Cả hai người theo dõi đều phải nhận cùng một mẩu tin
    [Fact]
    public async Task PublishAsync_notifies_all_registered_observers()
    {
        var first = new CountingObserver();
        var second = new CountingObserver();
        var publisher = new StudyEventPublisher(
            [first, second],
            NullLogger<StudyEventPublisher>.Instance);

        var studyEvent = new CardProgressChangedEvent(
            "user-1",
            DateTime.UtcNow,
            SetId: 1,
            FlashcardId: 2,
            IsLearned: true,
            Status: ltwnc.Models.Entities.UserProgressStatus.Mastered);

        await publisher.PublishAsync(studyEvent);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Same(studyEvent, first.LastEvent);
        Assert.Same(studyEvent, second.LastEvent);
    }

    // Một observer lỗi không được chặn observer còn lại
    [Fact]
    public async Task PublishAsync_continues_when_one_observer_throws()
    {
        var healthy = new CountingObserver();
        var publisher = new StudyEventPublisher(
            [new ThrowingObserver(), healthy],
            NullLogger<StudyEventPublisher>.Instance);

        await publisher.PublishAsync(new StudySessionCompletedEvent(
            "user-1",
            DateTime.UtcNow,
            SetId: 1,
            SessionId: 9,
            Mode: ltwnc.Models.Entities.StudyMode.Flashcard,
            Score: null));

        Assert.Equal(1, healthy.CallCount);
    }
}
