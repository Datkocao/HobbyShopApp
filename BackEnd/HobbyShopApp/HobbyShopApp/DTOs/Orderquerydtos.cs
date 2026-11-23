using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Một trang kết quả (dùng cho danh sách đơn).
public class OrderPageDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

// Một dòng trong danh sách "đơn của tôi". ItemCount là tổng số lượng sản phẩm trong đơn.
public class OrderSummaryDto
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = "";
    public OrderStatus OrderStatus { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public long Total { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Danh sách đơn cho quản trị: thêm thông tin người mua để đối chiếu.
public class AdminOrderSummaryDto : OrderSummaryDto
{
    public long BuyerUserId { get; set; }
    public string BuyerEmail { get; set; } = "";
    public string BuyerFullName { get; set; } = "";
}

// Chi tiết đơn cho quản trị.
public class AdminOrderDto : OrderDto
{
    public long BuyerUserId { get; set; }
    public string BuyerEmail { get; set; } = "";
    public string BuyerFullName { get; set; } = "";
}