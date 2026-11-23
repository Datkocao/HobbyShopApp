using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Nội dung gửi lên khi đổi trạng thái đơn: trạng thái ĐÍCH muốn chuyển tới.
// Chấp nhận: Confirmed, Shipping, Delivered, Completed (hủy đơn dùng API riêng).
public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
// Nội dung gửi lên khi hủy đơn. Buyer hủy đơn của mình: Reason không bắt buộc.
// Staff/Admin hủy: Reason bắt buộc (service kiểm tra, tối đa 500 ký tự).
public class CancelOrderRequest
{
    public string? Reason { get; set; }
}