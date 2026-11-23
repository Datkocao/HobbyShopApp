using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Nội dung gửi lên khi đặt hàng: chọn địa chỉ giao trong sổ địa chỉ và ghi chú (tùy chọn, tối đa 500 ký tự).
public class PlaceOrderRequest
{
    public long AddressId { get; set; }
    public string? Note { get; set; }
}

// Một dòng trong đơn: tên, giá, cân nặng là bản CHỤP lúc đặt hàng.
public class OrderItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public long UnitPrice { get; set; }
    public int Quantity { get; set; }
    public long LineTotal { get; set; }
    public int WeightGrams { get; set; }
}

public class OrderDto
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = "";
    public OrderStatus OrderStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public long Subtotal { get; set; }
    public long ShippingFee { get; set; }
    public long Total { get; set; }
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string AddressText { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}