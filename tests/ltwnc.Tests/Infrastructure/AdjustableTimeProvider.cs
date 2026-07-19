namespace ltwnc.Tests.Infrastructure;

public sealed class AdjustableTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset InitialUtcNow =
        new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _utcNow = InitialUtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

    public void Reset() => _utcNow = InitialUtcNow;
}
