using System.Data.Common;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IOrderCancelService
{
    // Buyer hủy đơn của chính mình (đơn của người khác trả 404). Lý do không bắt buộc.
    Task<ServiceResult<OrderDto>> CancelMineAsync(long userId, long orderId, string? reason);

    // Staff/Admin hủy bất kỳ đơn nào. Lý do bắt buộc.
    Task<ServiceResult<OrderDto>> CancelAsStaffAsync(long staffUserId, long orderId, string? reason);
}

public class OrderCancelService : IOrderCancelService
{
    private const int MaxReasonLength = 500;

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<OrderCancelService> _logger;

    public OrderCancelService(AppDbContext db, IAuditService audit, ILogger<OrderCancelService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public Task<ServiceResult<OrderDto>> CancelMineAsync(long userId, long orderId, string? reason) =>
        CancelCoreAsync(userId, orderId, reason, isStaff: false);

    public Task<ServiceResult<OrderDto>> CancelAsStaffAsync(long staffUserId, long orderId, string? reason) =>
        CancelCoreAsync(staffUserId, orderId, reason, isStaff: true);

    private async Task<ServiceResult<OrderDto>> CancelCoreAsync(long actorUserId, long orderId, string? reason, bool isStaff)
    {
        // ---------- Phần 1: kiểm tra sớm, chưa đổi dữ liệu nào ----------

        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) reason = null;

        if (isStaff && reason is null)
            return Fail(ServiceErrorType.Validation, "Cần nhập lý do hủy đơn.");
        if (reason is not null && reason.Length > MaxReasonLength)
            return Fail(ServiceErrorType.Validation, $"Lý do tối đa {MaxReasonLength} ký tự.");

        // Buyer chỉ thấy đơn của mình: đơn của người khác trả 404 (không lộ việc nó có tồn tại).
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Id == orderId && (isStaff || o.BuyerUserId == actorUserId))
            .FirstOrDefaultAsync();
        if (order is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy đơn hàng.");

        // Báo sớm cho dễ hiểu. Kiểm tra thật nằm ở bước 0 trong transaction.
        if (order.OrderStatus != OrderStatus.PendingConfirmation)
            return Fail(ServiceErrorType.Conflict,
                "Đơn không còn ở trạng thái chờ xác nhận nên không thể hủy.");

        // Chỉ hoàn tiền khi đơn đã thanh toán và có tiền. Trạng thái thanh toán của đơn đang
        // PendingConfirmation không đổi được ở chỗ nào khác, nên giá trị đọc ở đây đáng tin.
        var refundNeeded = order.PaymentStatus == PaymentStatus.Paid && order.Total > 0;
        var newPaymentStatus = refundNeeded ? PaymentStatus.Refunded : order.PaymentStatus;

        long buyerWalletId = 0;
        long platformWalletId = 0;
        if (refundNeeded)
        {
            var buyerId = await _db.Wallets.AsNoTracking()
                .Where(w => w.UserId == order.BuyerUserId && w.WalletType == WalletType.User)
                .Select(w => (long?)w.Id)
                .FirstOrDefaultAsync();
            if (buyerId is null)
                return Fail(ServiceErrorType.Unavailable, "Không tìm thấy ví của người mua để hoàn tiền.");

            var platformId = await _db.Wallets.AsNoTracking()
                .Where(w => w.WalletType == WalletType.Platform)
                .Select(w => (long?)w.Id)
                .FirstOrDefaultAsync();
            if (platformId is null)
                return Fail(ServiceErrorType.Unavailable, "Hệ thống chưa sẵn sàng hoàn tiền, hãy liên hệ quản trị.");

            buyerWalletId = buyerId.Value;
            platformWalletId = platformId.Value;
        }

        // ---------- Phần 2: một transaction duy nhất, lỗi giữa chừng thì hoàn tác tất cả ----------

        var now = DateTime.UtcNow;
        var total = order.Total;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Bước 0: "giành quyền" hủy bằng UPDATE có điều kiện còn PendingConfirmation.
            // Hai yêu cầu cùng lúc (bấm đúp, buyer hủy trong lúc Staff xác nhận): chỉ một bên được 1 dòng,
            // bên còn lại được 0 dòng và dừng ngay, nên không bao giờ hoàn kho hoặc hoàn tiền hai lần.
            var claimed = await _db.Orders
                .Where(o => o.Id == orderId && o.OrderStatus == OrderStatus.PendingConfirmation)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.OrderStatus, OrderStatus.Cancelled)
                    .SetProperty(o => o.PaymentStatus, newPaymentStatus)
                    .SetProperty(o => o.CancelReason, reason)
                    .SetProperty(o => o.UpdatedAt, now));

            if (claimed == 0)
            {
                await tx.RollbackAsync();
                return Fail(ServiceErrorType.Conflict,
                    "Đơn vừa được xử lý hoặc đã hủy, hãy tải lại trang.");
            }

            // Bước 1: hoàn kho, theo Id sản phẩm tăng dần (cùng thứ tự khóa với lúc đặt hàng).
            // Không lọc theo trạng thái sản phẩm: hàng đã ngừng bán vẫn phải được cộng trả lại kho.
            foreach (var item in order.Items.OrderBy(i => i.ProductId))
            {
                var productId = item.ProductId;
                var qty = item.Quantity;

                var rows = await _db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity + qty)
                        .SetProperty(p => p.UpdatedAt, now));

                if (rows == 0)
                {
                    await tx.RollbackAsync();
                    return Fail(ServiceErrorType.Conflict, "Không hoàn được kho cho đơn này, hãy liên hệ quản trị.");
                }
            }

            // Bước 2: hoàn tiền bằng bút toán ngược với lúc thanh toán.
            // Thứ tự khóa ví cố định: ví người dùng TRƯỚC, ví hệ thống SAU.
            if (refundNeeded)
            {
                await _db.Wallets
                    .Where(w => w.Id == buyerWalletId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.Balance, w => w.Balance + total)
                        .SetProperty(w => w.UpdatedAt, now));

                // Dòng ví đang bị khóa tới hết transaction nên số đọc ra chính là số dư sau giao dịch.
                var buyerBalanceAfter = await _db.Wallets.AsNoTracking()
                    .Where(w => w.Id == buyerWalletId).Select(w => w.Balance).FirstAsync();

                await _db.Wallets
                    .Where(w => w.Id == platformWalletId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.Balance, w => w.Balance - total)
                        .SetProperty(w => w.UpdatedAt, now));

                var platformBalanceAfter = await _db.Wallets.AsNoTracking()
                    .Where(w => w.Id == platformWalletId).Select(w => w.Balance).FirstAsync();

                var group = Guid.NewGuid();

                _db.LedgerEntries.AddRange(
                    new LedgerEntry
                    {
                        WalletId = buyerWalletId,
                        Amount = total,
                        BalanceAfter = buyerBalanceAfter,
                        EntryType = LedgerEntryType.Refund,
                        RefType = nameof(Order),
                        RefId = order.Id,
                        TransferGroupId = group,
                        IdempotencyKey = $"order-refund:{order.Id}:buyer",
                        Note = $"Hoàn tiền đơn {order.OrderCode}",
                        CreatedByUserId = actorUserId,
                        CreatedAt = now
                    },
                    new LedgerEntry
                    {
                        WalletId = platformWalletId,
                        Amount = -total,
                        BalanceAfter = platformBalanceAfter,
                        EntryType = LedgerEntryType.Refund,
                        RefType = nameof(Order),
                        RefId = order.Id,
                        TransferGroupId = group,
                        IdempotencyKey = $"order-refund:{order.Id}:platform",
                        Note = $"Hoàn tiền đơn {order.OrderCode}",
                        CreatedByUserId = actorUserId,
                        CreatedAt = now
                    });
                await _db.SaveChangesAsync();
            }

            // Bước 3: ghi nhật ký, lưu cùng transaction.
            _audit.Add(actorUserId, "order.cancel", nameof(Order), order.Id.ToString(),
                new { OrderStatus = OrderStatus.PendingConfirmation.ToString(), PaymentStatus = order.PaymentStatus.ToString() },
                new
                {
                    OrderStatus = OrderStatus.Cancelled.ToString(),
                    PaymentStatus = newPaymentStatus.ToString(),
                    Reason = reason,
                    RefundedAmount = refundNeeded ? total : 0,
                    CancelledByStaff = isStaff
                });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ServiceResult<OrderDto>.Ok(ToDto(order, newPaymentStatus));
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            // Lỗi giữa chừng: transaction tự hoàn tác, đơn, kho và ví đều như chưa có gì xảy ra.
            _logger.LogError(ex, "Hủy đơn {OrderId} thất bại (người thao tác {UserId})", orderId, actorUserId);
            return Fail(ServiceErrorType.Conflict, "Không hủy được đơn, hãy thử lại.");
        }
    }

    // ---------- Hàm hỗ trợ ----------

    private static ServiceResult<OrderDto> Fail(ServiceErrorType type, string error) =>
        ServiceResult<OrderDto>.Fail(type, error);

    // Đơn đọc ra ở phần 1 là bản cũ (trước khi hủy), nên trạng thái mới được ghi đè ở đây.
    private static OrderDto ToDto(Order o, PaymentStatus paymentStatus) => new()
    {
        Id = o.Id,
        OrderCode = o.OrderCode,
        OrderStatus = OrderStatus.Cancelled,
        PaymentMethod = o.PaymentMethod,
        PaymentStatus = paymentStatus,
        Subtotal = o.Subtotal,
        ShippingFee = o.ShippingFee,
        Total = o.Total,
        RecipientName = o.RecipientName,
        Phone = o.Phone,
        AddressText = o.AddressText,
        Note = o.Note,
        CreatedAt = o.CreatedAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            LineTotal = i.LineTotal,
            WeightGrams = i.WeightGrams
        }).ToList()
    };
}