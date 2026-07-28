using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Chạy command batch và ghi CardActionLog; Undo từ log + snapshot.
public class CardActionService : ICardActionService
{
    // Lưu log và transaction
    private readonly AppDbContext _context;

    // Tái tạo command khi Undo theo ActionType trong log
    private readonly ICardActionCommandFactory _commandFactory;

    // Inject DbContext và factory command
    public CardActionService(AppDbContext context, ICardActionCommandFactory commandFactory)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_commandFactory` để các phương thức khác sử dụng.
        _commandFactory = commandFactory;
    }

    // Execute command trong transaction, ghi log kèm snapshot, trả về log vừa tạo
    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        // 1. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        // Command tự chụp trạng thái trước khi đổi và trả Memento cho service lưu giữ.
        CardActionMemento memento = await command.ExecuteAsync();

        // 4. Khởi tạo `log` với dữ liệu ban đầu cần thiết.
        CardActionLog log = new CardActionLog
        {
            UserId = command.UserId,
            SetId = command.SetId,
            ActionType = command.ActionType,
            CardIdsJson = JsonSerializer.Serialize(command.CardIds),
            SnapshotJson = memento.StateJson,
            ExecutedAt = DateTime.UtcNow
        };

        // 5. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.CardActionLogs.Add(log);
        // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 7. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync();
        // 8. Trả `log` cho nơi gọi.
        return log;
    }

    // Load log của user, chặn Undo lần 2, nạp snapshot rồi gọi command.UndoAsync
    public async Task UndoAsync(int logId, string userId)
    {
        // 1. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        // 2. Gọi `GetLogByIdAsync` và lưu kết quả vào `log`.
        CardActionLog? log = await GetLogByIdAsync(logId, userId);
        // 3. Kiểm tra `log == null` để chọn nhánh xử lý phù hợp.
        if (log == null)
        {
            // 4. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.")`.
            throw new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.");
        }

        // 5. Kiểm tra `log.UndoneAt.HasValue` để chọn nhánh xử lý phù hợp.
        if (log.UndoneAt.HasValue)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Hành động này đã được hoàn tác.")`.
            throw new InvalidOperationException("Hành động này đã được hoàn tác.");
        }

        // 7. Gọi `Deserialize` và lưu kết quả vào `cardIds`.
        List<int>? cardIds = JsonSerializer.Deserialize<List<int>>(log.CardIdsJson);
        // 8. Kiểm tra `cardIds == null` để chọn nhánh xử lý phù hợp.
        if (cardIds == null)
        {
            // 9. Cập nhật `cardIds` bằng giá trị mới.
            cardIds = new List<int>();
        }

        // 10. Gọi `Create` và lưu kết quả vào `command`.
        ICardActionCommand command = _commandFactory.Create(
            log.ActionType,
            log.SetId,
            userId,
            cardIds);

        // Log cũ và log mới đều lưu cùng JSON nên cùng dựng được Memento.
        CardActionMemento memento = new(log.SnapshotJson);

        // Command tự đọc và kiểm tra Memento trước khi khôi phục.
        await command.UndoAsync(memento);
        // 13. Cập nhật `log.UndoneAt` bằng giá trị mới.
        log.UndoneAt = DateTime.UtcNow;
        // 14. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 15. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync();
    }

    // Log chưa Undo của một bộ thẻ, mới nhất trước, giới hạn limit
    public async Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(
        int setId,
        string userId,
        int limit = 5)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `logs`.
        List<CardActionLog> logs = await _context.CardActionLogs
            .Where(log =>
                log.SetId == setId
                && log.UserId == userId
                && !log.UndoneAt.HasValue)
            .OrderByDescending(log => log.ExecutedAt)
            .Take(limit)
            .ToListAsync();

        // 2. Trả `logs` cho nơi gọi.
        return logs;
    }

    // Log theo id, chỉ khi đúng user
    public async Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `log`.
        CardActionLog? log = await _context.CardActionLogs
            .FirstOrDefaultAsync(row => row.Id == logId && row.UserId == userId);

        // 2. Trả `log` cho nơi gọi.
        return log;
    }
}
