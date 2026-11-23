using System.Data.Common;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IOrderStatusService
{
    Task<ServiceResult<AdminOrderDto>> UpdateStatusAsync(long orderId, OrderStatus target, long actorId);
}

public class OrderStatusService : IOrderStatusService
{
    // Các bước hợp lệ: trạng thái ĐÍCH -> trạng thái mà đơn BẮT BUỘC đang ở. Chỉ đi tới, không nhảy cóc, không đi lùi.
    // Tạm thời nhân viên tự bấm từng bước; đến giai đoạn vận chuyển thì các bước vận chuyển sẽ tự động.
    private static readonly Dictionary<OrderStatus, OrderStatus> RequiredBefore = new()
    {
        [OrderStatus.Confirmed] = OrderStatus.PendingConfirmation,
        [OrderStatus.Shipping] = OrderStatus.Confirmed,
        [OrderStatus.Delivered] = OrderStatus.Shipping,
        [OrderStatus.Completed] = OrderStatus.Delivered,
    };

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IOrderQueryService _query;
    private readonly ILogger<OrderStatusService> _logger;

    public OrderStatusService(AppDbContext db, IAuditService audit, IOrderQueryService query,
                              ILogger<OrderStatusService> logger)
    {
        _db = db;
        _audit = audit;
        _query = query;
        _logger = logger;
    }

    public async Task<ServiceResult<AdminOrderDto>> UpdateStatusAsync(long orderId, OrderStatus target, long actorId)
    {
        if (!RequiredBefore.TryGetValue(target, out var required))
            return Fail(ServiceErrorType.Validation,
                "Chỉ chuyển được tới Confirmed, Shipping, Delivered hoặc Completed. Hủy đơn dùng API hủy riêng.");

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy đơn hàng.");

        // Đơn của người bán (C2C) có luồng riêng ở giai đoạn sau.
        if (order.SellerUserId is not null)
            return Fail(ServiceErrorType.Validation, "Đơn của người bán sẽ được xử lý ở giai đoạn sau.");

        // Kiểm tra sớm để báo lỗi dễ hiểu; việc đảm bảo đúng nằm ở câu UPDATE có điều kiện bên dưới.
        if (order.OrderStatus != required)
            return Fail(ServiceErrorType.Conflict,
                $"Đơn đang ở trạng thái {order.OrderStatus}; để chuyển sang {target} đơn phải đang ở {required}.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Chỉ đổi nếu đơn VẪN đang ở trạng thái bắt buộc. Hai người bấm cùng lúc: chỉ một câu UPDATE thắng,
            // câu còn lại được 0 dòng và nhận 409, nên một bước không bao giờ bị làm hai lần.
            var rows = await _db.Orders
                .Where(o => o.Id == orderId && o.OrderStatus == required)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.OrderStatus, target)
                    .SetProperty(o => o.UpdatedAt, now));

            if (rows == 0)
            {
                await tx.RollbackAsync();
                return Fail(ServiceErrorType.Conflict, "Đơn vừa được cập nhật bởi người khác, hãy tải lại.");
            }

            _audit.Add(actorId, "order.update_status", nameof(Order), orderId.ToString(),
                new { Status = required.ToString() },
                new { Status = target.ToString() });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            _logger.LogError(ex, "Đổi trạng thái đơn {OrderId} thất bại", orderId);
            return Fail(ServiceErrorType.Conflict, "Không đổi được trạng thái, hãy thử lại.");
        }

        return await _query.GetAnyAsync(orderId);
    }

    private static ServiceResult<AdminOrderDto> Fail(ServiceErrorType type, string error) =>
        ServiceResult<AdminOrderDto>.Fail(type, error);
}