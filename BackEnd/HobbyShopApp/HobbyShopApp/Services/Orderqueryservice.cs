using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IOrderQueryService
{
    Task<ServiceResult<OrderPageDto<OrderSummaryDto>>> ListMineAsync(long userId, int page, int pageSize);
    Task<ServiceResult<OrderDto>> GetMineAsync(long userId, long orderId);

    Task<ServiceResult<OrderPageDto<AdminOrderSummaryDto>>> ListAllAsync(OrderStatus? status, int page, int pageSize);
    Task<ServiceResult<AdminOrderDto>> GetAnyAsync(long orderId);
}

public class OrderQueryService : IOrderQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public OrderQueryService(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Người mua: đơn của tôi ----------

    public async Task<ServiceResult<OrderPageDto<OrderSummaryDto>>> ListMineAsync(long userId, int page, int pageSize)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.Orders.AsNoTracking().Where(o => o.BuyerUserId == userId);
        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                OrderStatus = o.OrderStatus,
                PaymentStatus = o.PaymentStatus,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return ServiceResult<OrderPageDto<OrderSummaryDto>>.Ok(new OrderPageDto<OrderSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ServiceResult<OrderDto>> GetMineAsync(long userId, long orderId)
    {
        // Đơn của người khác trả 404 (không lộ việc nó có tồn tại).
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == userId);

        if (order is null)
            return ServiceResult<OrderDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy đơn hàng.");

        return ServiceResult<OrderDto>.Ok(FillDetail(new OrderDto(), order));
    }

    // ---------- Quản trị: mọi đơn ----------

    public async Task<ServiceResult<OrderPageDto<AdminOrderSummaryDto>>> ListAllAsync(
        OrderStatus? status, int page, int pageSize)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(o => o.OrderStatus == status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderSummaryDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                OrderStatus = o.OrderStatus,
                PaymentStatus = o.PaymentStatus,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                CreatedAt = o.CreatedAt,
                BuyerUserId = o.BuyerUserId,
                BuyerEmail = o.Buyer.Email,
                BuyerFullName = o.Buyer.FullName
            })
            .ToListAsync();

        return ServiceResult<OrderPageDto<AdminOrderSummaryDto>>.Ok(new OrderPageDto<AdminOrderSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ServiceResult<AdminOrderDto>> GetAnyAsync(long orderId)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
            return ServiceResult<AdminOrderDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy đơn hàng.");

        var dto = FillDetail(new AdminOrderDto(), order);
        dto.BuyerUserId = order.BuyerUserId;
        dto.BuyerEmail = order.Buyer.Email;
        dto.BuyerFullName = order.Buyer.FullName;

        return ServiceResult<AdminOrderDto>.Ok(dto);
    }

    // ---------- Hàm hỗ trợ ----------

    // Giá trị ngoài khoảng thì chỉnh về khoảng hợp lệ chứ không báo lỗi.
    private static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;
        return (page, pageSize);
    }

    private static TDto FillDetail<TDto>(TDto dto, Order o) where TDto : OrderDto
    {
        dto.Id = o.Id;
        dto.OrderCode = o.OrderCode;
        dto.OrderStatus = o.OrderStatus;
        dto.PaymentMethod = o.PaymentMethod;
        dto.PaymentStatus = o.PaymentStatus;
        dto.Subtotal = o.Subtotal;
        dto.ShippingFee = o.ShippingFee;
        dto.Total = o.Total;
        dto.RecipientName = o.RecipientName;
        dto.Phone = o.Phone;
        dto.AddressText = o.AddressText;
        dto.Note = o.Note;
        dto.CancelReason = o.CancelReason;
        dto.CreatedAt = o.CreatedAt;
        dto.Items = o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.LineTotal,
                WeightGrams = i.WeightGrams
            })
            .ToList();
        return dto;
    }
}