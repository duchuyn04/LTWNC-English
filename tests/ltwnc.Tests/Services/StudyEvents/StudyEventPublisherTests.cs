using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using Microsoft.Extensions.Logging;

namespace ltwnc.Tests.Services.StudyEvents;

public sealed class StudyEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_CallsObserversInRegistrationOrder()
    {
        List<string> calls = [];
        TestObserver first = new("first", (_, _) =>
        {
            calls.Add("first");
            return Task.CompletedTask;
        });
        TestObserver second = new("second", (_, _) =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        await CreatePublisher(first, second).PublishAsync(CreateEvent());

        Assert.Equal(new[] { "first", "second" }, calls);
    }

    [Fact]
    public async Task PublishAsync_LogsOrdinaryFailureAndContinuesToLaterObservers()
    {
        InvalidOperationException failure = new("observer failed");
        TestObserver failing = new("failing", (_, _) => Task.FromException(failure));
        TestObserver later = new("later", (_, _) => Task.CompletedTask);
        RecordingLogger logger = new();

        await CreatePublisher(logger, failing, later).PublishAsync(CreateEvent());

        Assert.Equal(1, failing.Calls);
        Assert.Equal(1, later.Calls);
        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Contains("TestObserver", entry.Message);
        Assert.Contains(nameof(CardProgressChangedEvent), entry.Message);
        Assert.Contains("user-1", entry.Message);
    }

    [Fact]
    public async Task PublishAsync_PropagatesRequestCancellationAndSkipsLaterObservers()
    {
        using CancellationTokenSource cancellation = new();
        TestObserver canceled = new("canceled", (_, token) =>
        {
            cancellation.Cancel();
            return Task.FromException(new OperationCanceledException(token));
        });
        TestObserver later = new("later", (_, _) => Task.CompletedTask);

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreatePublisher(canceled, later).PublishAsync(CreateEvent(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, later.Calls);
    }

    [Fact]
    public async Task PublishAsync_WhenRequestAlreadyCanceled_DoesNotCallObservers()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        TestObserver observer = new("observer", (_, _) => Task.CompletedTask);

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreatePublisher(observer).PublishAsync(CreateEvent(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, observer.Calls);
    }

    private static StudyEventPublisher CreatePublisher(params TestObserver[] observers)
        => CreatePublisher(new RecordingLogger(), observers);

    private static StudyEventPublisher CreatePublisher(
        RecordingLogger logger,
        params TestObserver[] observers)
        => new(observers, logger);

    private static CardProgressChangedEvent CreateEvent()
        => new(
            UserId: "user-1",
            OccurredAtUtc: DateTime.UtcNow,
            SetId: 7,
            FlashcardId: 8,
            IsLearned: true,
            Status: UserProgressStatus.Mastered);

    private sealed class TestObserver(
        string name,
        Func<StudyEvent, CancellationToken, Task> handler) : IStudyEventObserver
    {
        public string Name { get; } = name;

        public int Calls { get; private set; }

        public async Task OnStudyEventAsync(
            StudyEvent studyEvent,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            await handler(studyEvent, cancellationToken);
        }
    }

    private sealed class RecordingLogger : ILogger<StudyEventPublisher>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
