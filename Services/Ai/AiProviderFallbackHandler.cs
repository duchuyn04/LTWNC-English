using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Ai;

// Chain of Responsibility: mỗi handler thử một provider rồi chuyển lỗi an toàn
// cho handler kế tiếp trong chuỗi fallback.
public sealed class AiProviderFallbackHandler
{
    private readonly AiProviderOptions _provider;
    private readonly IAiProviderAdapter? _adapter;
    private readonly AiProviderFallbackHandler? _next;
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AiProviderFallbackHandler(
        AiProviderOptions provider,
        IAiProviderAdapter? adapter,
        AiProviderFallbackHandler? next,
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _adapter = adapter;
        _next = next;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<AiCompletionResult?> HandleAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator,
        int fallbackAttempt,
        CancellationTokenSource overallTimeout,
        CancellationToken cancellationToken)
    {
        if (_adapter == null)
        {
            await RecordAttemptAsync(
                succeeded: false,
                failureKind: "UnsupportedAdapter",
                latencyMs: 0,
                fallbackAttempt: fallbackAttempt,
                cancellationToken: cancellationToken);
            return await NextAsync(
                request,
                responseValidator,
                fallbackAttempt,
                overallTimeout,
                cancellationToken);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            AiProviderConnection connection = new(
                _provider.Name,
                _provider.BaseUrl,
                _provider.ModelId,
                _provider.TimeoutSeconds);
            _adapter.ValidateConfiguration(connection);
            string content = await _adapter.CompleteAsync(
                connection,
                _provider.ApiKey,
                request,
                overallTimeout.Token);

            if (responseValidator != null && !responseValidator(content))
            {
                await RecordAttemptAsync(
                    succeeded: false,
                    failureKind: "InvalidResponse",
                    latencyMs: ElapsedMilliseconds(stopwatch),
                    fallbackAttempt: fallbackAttempt,
                    cancellationToken: cancellationToken);
                return await NextAsync(
                    request,
                    responseValidator,
                    fallbackAttempt,
                    overallTimeout,
                    cancellationToken);
            }

            await RecordAttemptAsync(
                succeeded: true,
                failureKind: null,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken: cancellationToken);
            return new AiCompletionResult(
                content,
                null,
                _provider.Name,
                _provider.ModelId);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && overallTimeout.IsCancellationRequested)
        {
            await RecordAttemptAsync(
                succeeded: false,
                failureKind: "TotalTimeout",
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken: CancellationToken.None);
            return null;
        }
        catch (Exception exception) when (IsFallbackSafeFailure(exception))
        {
            await RecordAttemptAsync(
                succeeded: false,
                failureKind: exception.GetType().Name,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken: cancellationToken);
            return await NextAsync(
                request,
                responseValidator,
                fallbackAttempt,
                overallTimeout,
                cancellationToken);
        }
    }

    private Task<AiCompletionResult?> NextAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator,
        int fallbackAttempt,
        CancellationTokenSource overallTimeout,
        CancellationToken cancellationToken)
    {
        if (overallTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AiCompletionResult?>(null);
        }

        return _next?.HandleAsync(
            request,
            responseValidator,
            fallbackAttempt + 1,
            overallTimeout,
            cancellationToken)
            ?? Task.FromResult<AiCompletionResult?>(null);
    }

    private async Task RecordAttemptAsync(
        bool succeeded,
        string? failureKind,
        int latencyMs,
        int fallbackAttempt,
        CancellationToken cancellationToken)
    {
        DateTime occurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (!_context.Database.IsRelational())
        {
            _context.AiOperationLogs.Add(new AiOperationLog
            {
                OccurredAtUtc = occurredAtUtc,
                ProviderId = null,
                ProviderName = _provider.Name,
                ModelId = _provider.ModelId,
                Operation = "Completion",
                Succeeded = succeeded,
                FailureKind = failureKind,
                LatencyMs = latencyMs,
                FallbackAttempt = fallbackAttempt
            });
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO AiOperationLogs
                (OccurredAtUtc, ProviderId, ProviderName, ModelId, Operation, Succeeded, FailureKind, LatencyMs, FallbackAttempt)
            VALUES
                ({occurredAtUtc}, NULL, {_provider.Name}, {_provider.ModelId}, {"Completion"}, {succeeded}, {failureKind}, {latencyMs}, {fallbackAttempt})
            """,
            cancellationToken);
    }

    private static bool IsFallbackSafeFailure(Exception exception)
    {
        return exception is AiProviderUnavailableException
            or AiProviderConfigurationException
            or JsonException
            or CryptographicException;
    }

    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
    }
}
