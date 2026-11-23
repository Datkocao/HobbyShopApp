using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IOrderService
{
    Task<ServiceResult<OrderDto>> PlaceOrderAsync(long userId, PlaceOrderRequest request);
}

public class OrderService : IOrderService
{
    private const string KeyFlatShippingFee = "shipping.flat_fee";   // phí vận chuyển cố định tạm thời (đồng)
    private const int MaxNoteLength = 500;

    // Mã đơn: "HS" + ngày (yyMMdd) + 6 ký tự ngẫu nhiên, bỏ các ký tự dễ nhầm (0, O, 1, I).
    private const string CodePrefix = "HS";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeRandomLength = 6;
    private const int MaxCodeAttempts = 5;

    // Dòng giỏ hàng kèm thông tin sản phẩm lúc bắt đầu đặt (chỉ dùng để kiểm tra sớm và báo lỗi dễ hiểu).
    private sealed class CartLine
    {
        public long ProductId { get; init; }
        public int Quantity { get; init; }
        public string Name { get; init; } = "";
        public bool Visible { get; init; }
        public long? SellerUserId { get; init; }
        public int StockQuantity { get; init; }
    }

    private readonly AppDbContext _db;
    private readonly IWalletService _wallets;
    private readonly IAuditService _audit;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, IWalletService wallets, IAuditService audit, ILogger<OrderService> logger)
    {
        _db = db;
        _wallets = wallets;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ServiceResult<OrderDto>> PlaceOrderAsync(long userId, PlaceOrderRequest request)
    {
        // ---------- Phần 1: kiểm tra sớm, chưa đụng vào dữ liệu nào ----------

        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) note = null;
        if (note is not null && note.Length > MaxNoteLength)
            return Fail(ServiceErrorType.Validation, $"Ghi chú tối đa {MaxNoteLength} ký tự.");

        // Địa chỉ của người khác (hoặc đã xóa) trả 404, giống quy ước của sổ địa chỉ.
        var address = await _db.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == userId && !a.IsDeleted);
        if (address is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy địa chỉ giao hàng.");

        var lines = await _db.CartItems.AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .Select(i => new CartLine
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Name = i.Product.Name,
                Visible = !i.Product.IsDeleted
                          && i.Product.Status == ProductStatus.Active
                          && i.Product.Category.IsActive
                          && !i.Product.Category.IsDeleted,
                SellerUserId = i.Product.SellerUserId,
                StockQuantity = i.Product.StockQuantity
            })
            .ToListAsync();

        if (lines.Count == 0)
            return Fail(ServiceErrorType.Validation, "Giỏ hàng đang trống.");

        foreach (var line in lines)
        {
            if (!line.Visible)
                return Fail(ServiceErrorType.Conflict,
                    $"Sản phẩm \"{line.Name}\" không còn được bán, hãy bỏ khỏi giỏ.");

            // Hàng của người bán (C2C) làm ở giai đoạn sau; hiện chỉ đặt được hàng của shop.
            if (line.SellerUserId is not null)
                return Fail(ServiceErrorType.Validation,
                    $"Sản phẩm \"{line.Name}\" của người bán chưa hỗ trợ đặt hàng.");

            if (line.StockQuantity < line.Quantity)
                return Fail(ServiceErrorType.Conflict,
                    $"Sản phẩm \"{line.Name}\" chỉ còn {line.StockQuantity} trong kho, hãy giảm số lượng trong giỏ.");
        }

        // Phí vận chuyển tạm: một mức cố định trong system_settings (đến khi làm giai đoạn vận chuyển).
        var feeText = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == KeyFlatShippingFee)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        if (!long.TryParse(feeText, NumberStyles.None, CultureInfo.InvariantCulture, out var shippingFee))
            return Fail(ServiceErrorType.Unavailable, "Chưa cấu hình phí vận chuyển, hãy liên hệ quản trị.");

        var buyerWalletId = await _wallets.GetOrCreateUserWalletIdAsync(userId);
        if (buyerWalletId is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy ví của bạn.");

        var platformWalletId = await _db.Wallets.AsNoTracking()
            .Where(w => w.WalletType == WalletType.Platform)
            .Select(w => (long?)w.Id)
            .FirstOrDefaultAsync();
        if (platformWalletId is null)
            return Fail(ServiceErrorType.Unavailable, "Hệ thống chưa sẵn sàng nhận thanh toán, hãy liên hệ quản trị.");

        var orderCode = await GenerateUniqueOrderCodeAsync();
        if (orderCode is null)
            return Fail(ServiceErrorType.Conflict, "Không tạo được mã đơn, hãy thử lại.");

        // ---------- Phần 2: một transaction duy nhất, lỗi giữa chừng thì hoàn tác tất cả ----------

        // Luôn xử lý sản phẩm theo thứ tự Id tăng dần: hai đơn cùng đặt các sản phẩm giống nhau sẽ khóa theo
        // cùng một thứ tự, nên không thể kẹt chéo nhau (deadlock).
        var ordered = lines.OrderBy(l => l.ProductId).ToList();
        var productIds = ordered.Select(l => l.ProductId).ToList();
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Bước 1: trừ kho bằng câu UPDATE có điều kiện "còn đủ hàng". Dòng sản phẩm bị khóa tới hết transaction,
            // nên giá đọc ở bước 2 chắc chắn là giá tại thời điểm chốt đơn. 0 dòng = vừa hết hàng, dừng cả đơn.
            foreach (var line in ordered)
            {
                var productId = line.ProductId;
                var qty = line.Quantity;

                var rows = await _db.Products
                    .Where(p => p.Id == productId
                                && !p.IsDeleted
                                && p.Status == ProductStatus.Active
                                && p.StockQuantity >= qty)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity - qty)
                        .SetProperty(p => p.UpdatedAt, now));

                if (rows == 0)
                {
                    await tx.RollbackAsync();
                    return Fail(ServiceErrorType.Conflict,
                        $"Sản phẩm \"{line.Name}\" vừa hết hàng hoặc không đủ số lượng, hãy kiểm tra lại giỏ.");
                }
            }

            // Bước 2: chụp lại tên, giá, cân nặng của từng sản phẩm và tính tiền.
            var products = await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Price, p.WeightGrams })
                .ToDictionaryAsync(p => p.Id);

            var items = ordered.Select(l =>
            {
                var p = products[l.ProductId];
                return new OrderItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    UnitPrice = p.Price,
                    Quantity = l.Quantity,
                    LineTotal = p.Price * l.Quantity,
                    WeightGrams = p.WeightGrams
                };
            }).ToList();

            var subtotal = items.Sum(i => i.LineTotal);
            var total = subtotal + shippingFee;

            // Bước 3: tạo đơn. Đơn shop thanh toán trước bằng ví nên ghi Paid ngay (nếu bước 4 lỗi thì hoàn tác cả đơn).
            var order = new Order
            {
                OrderCode = orderCode,
                BuyerUserId = userId,
                SellerUserId = null,
                OrderStatus = OrderStatus.PendingConfirmation,
                PaymentMethod = PaymentMethod.Wallet,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Total = total,
                RecipientName = address.RecipientName,
                Phone = address.Phone,
                AddressText = BuildAddressText(address),
                Note = note,
                CreatedAt = now,
                UpdatedAt = now,
                Items = items
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Bước 4: trừ ví người mua, cộng ví hệ thống, ghi hai dòng sổ cùng một TransferGroupId.
            // Quy ước khóa ví: luôn ví người dùng TRƯỚC, ví hệ thống SAU (các thao tác hoàn tiền sau này cũng phải theo thứ tự này).
            if (total > 0)
            {
                var buyerId = buyerWalletId.Value;
                var platformId = platformWalletId.Value;

                var debited = await _db.Wallets
                    .Where(w => w.Id == buyerId && w.Balance >= total)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.Balance, w => w.Balance - total)
                        .SetProperty(w => w.UpdatedAt, now));

                if (debited == 0)
                {
                    await tx.RollbackAsync();
                    return Fail(ServiceErrorType.Validation, "Số dư ví không đủ để thanh toán đơn này.");
                }

                // Dòng ví đang bị khóa tới hết transaction nên số đọc ra chính là số dư sau giao dịch.
                var buyerBalanceAfter = await _db.Wallets.AsNoTracking()
                    .Where(w => w.Id == buyerId).Select(w => w.Balance).FirstAsync();

                await _db.Wallets
                    .Where(w => w.Id == platformId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.Balance, w => w.Balance + total)
                        .SetProperty(w => w.UpdatedAt, now));

                var platformBalanceAfter = await _db.Wallets.AsNoTracking()
                    .Where(w => w.Id == platformId).Select(w => w.Balance).FirstAsync();

                var group = Guid.NewGuid();

                _db.LedgerEntries.AddRange(
                    new LedgerEntry
                    {
                        WalletId = buyerId,
                        Amount = -total,
                        BalanceAfter = buyerBalanceAfter,
                        EntryType = LedgerEntryType.OrderPayment,
                        RefType = nameof(Order),
                        RefId = order.Id,
                        TransferGroupId = group,
                        IdempotencyKey = $"order-pay:{order.Id}:buyer",
                        Note = $"Thanh toán đơn {orderCode}",
                        CreatedByUserId = userId,
                        CreatedAt = now
                    },
                    new LedgerEntry
                    {
                        WalletId = platformId,
                        Amount = total,
                        BalanceAfter = platformBalanceAfter,
                        EntryType = LedgerEntryType.OrderPayment,
                        RefType = nameof(Order),
                        RefId = order.Id,
                        TransferGroupId = group,
                        IdempotencyKey = $"order-pay:{order.Id}:platform",
                        Note = $"Nhận tiền đơn {orderCode}",
                        CreatedByUserId = userId,
                        CreatedAt = now
                    });
                await _db.SaveChangesAsync();
            }

            // Bước 5: bỏ các dòng đã đặt khỏi giỏ (chỉ đúng những sản phẩm trong đơn), rồi ghi nhật ký.
            await _db.CartItems
                .Where(i => i.Cart.UserId == userId && productIds.Contains(i.ProductId))
                .ExecuteDeleteAsync();

            _audit.Add(userId, "order.create", nameof(Order), order.Id.ToString(), null,
                new
                {
                    order.OrderCode,
                    order.Subtotal,
                    order.ShippingFee,
                    order.Total,
                    Items = items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice }).ToList()
                });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return ServiceResult<OrderDto>.Ok(ToDto(order));
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            // Lỗi giữa chừng: transaction tự hoàn tác, kho, ví, giỏ và đơn đều như chưa có gì xảy ra.
            _logger.LogError(ex, "Đặt hàng thất bại cho người dùng {UserId}", userId);
            return Fail(ServiceErrorType.Conflict, "Không đặt được hàng, hãy thử lại.");
        }
    }

    // ---------- Hàm hỗ trợ ----------

    // Chụp địa chỉ vào đơn: ghép chi tiết, phường/xã, quận/huyện, tỉnh/thành.
    private static string BuildAddressText(Address a) =>
        string.Join(", ", new[] { a.DetailLine, a.Ward, a.District, a.Province }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    // Mã ngẫu nhiên có thể trùng (rất hiếm): kiểm tra trước khi dùng. Chỉ mục duy nhất của database vẫn là lưới an toàn cuối.
    private async Task<string?> GenerateUniqueOrderCodeAsync()
    {
        var datePart = DateTime.UtcNow.ToString("yyMMdd", CultureInfo.InvariantCulture);

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var chars = new char[CodeRandomLength];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];

            var code = CodePrefix + datePart + new string(chars);
            if (!await _db.Orders.AnyAsync(o => o.OrderCode == code))
                return code;
        }

        return null;
    }

    private static ServiceResult<OrderDto> Fail(ServiceErrorType type, string error) =>
        ServiceResult<OrderDto>.Fail(type, error);

    private static OrderDto ToDto(Order o) => new()
    {
        Id = o.Id,
        OrderCode = o.OrderCode,
        OrderStatus = o.OrderStatus,
        PaymentMethod = o.PaymentMethod,
        PaymentStatus = o.PaymentStatus,
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