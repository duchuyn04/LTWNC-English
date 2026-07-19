using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Audit;

public sealed class AdminAuditService : IAdminAuditService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AdminAuditService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public void Enqueue(AdminAuditEntry entry)
    {
        _context.AdminAuditLogs.Add(BuildLog(entry));
    }

    public async Task<AdminAuditLog> RecordAsync(
        AdminAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        AdminAuditLog log = BuildLog(entry);
        _context.AdminAuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<AdminAuditLogPage> SearchAsync(
        AdminAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        IQueryable<AdminAuditLog> logs = _context.AdminAuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            string action = query.Action.Trim();
            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            string outcome = query.Outcome.Trim();
            logs = logs.Where(log => log.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            logs = logs.Where(log =>
                log.ActorDisplay.Contains(term)
                || log.ActorUserId.Contains(term)
                || log.Action.Contains(term)
                || (log.TargetId != null && log.TargetId.Contains(term)));
        }

        int totalCount = await logs.CountAsync(cancellationToken);
        List<AdminAuditLog> items = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminAuditLogPage(items, totalCount, page, pageSize);
    }

    private AdminAuditLog BuildLog(AdminAuditEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ActorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ActorDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Outcome);

        return new AdminAuditLog
        {
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            ActorUserId = entry.ActorUserId.Trim(),
            ActorDisplay = entry.ActorDisplay.Trim(),
            Action = entry.Action.Trim(),
            TargetType = TrimOrNull(entry.TargetType),
            TargetId = TrimOrNull(entry.TargetId),
            Outcome = entry.Outcome.Trim(),
            Reason = TrimOrNull(entry.Reason),
            CorrelationId = TrimOrNull(entry.CorrelationId),
            MetadataJson = AdminAuditMetadata.Serialize(entry.Metadata)
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
