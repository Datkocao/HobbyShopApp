namespace HobbyShopApp.Models;

public enum OrderStatus
{
    PendingConfirmation,   // chờ bên bán xác nhận
    Confirmed,             // đã xác nhận
    Shipping,              // đang vận chuyển
    Delivered,             // đã giao
    Completed,             // hoàn tất
    Cancelled              // đã hủy
}

public enum PaymentMethod
{
    Cod,      // thanh toán khi nhận hàng
    Wallet    // trừ tiền trong ví
}

public enum PaymentStatus
{
    Unpaid,
    CodPending,   // chờ thu tiền khi giao hàng
    Paid,
    Refunded
}

public class Order
{
    public long Id { get; set; }

    // Mã hiển thị cho người dùng (không dùng Id tự tăng để khỏi lộ số lượng đơn).
    public string OrderCode { get; set; } = null!;

    public long BuyerUserId { get; set; }
    public User Buyer { get; set; } = null!;

    // Rỗng = đơn của shop (B2C). Có giá trị = đơn của người bán (C2C, làm ở giai đoạn sau).
    public long? SellerUserId { get; set; }
    public User? Seller { get; set; }

    public OrderStatus OrderStatus { get; set; } = OrderStatus.PendingConfirmation;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    // Tiền theo đồng. Total = Subtotal + ShippingFee.
    public long Subtotal { get; set; }
    public long ShippingFee { get; set; }
    public long Total { get; set; }

    // Bản CHỤP địa chỉ lúc đặt hàng: người dùng sửa sổ địa chỉ sau này cũng không làm đổi đơn cũ.
    public string RecipientName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string AddressText { get; set; } = null!;

    public string? Note { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}