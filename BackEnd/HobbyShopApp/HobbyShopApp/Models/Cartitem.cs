namespace HobbyShopApp.Models;

// Một dòng trong giỏ hàng. Cố ý KHÔNG lưu giá: giá luôn lấy từ sản phẩm tại thời điểm xem và đặt hàng.
public class CartItem
{
    public long Id { get; set; }

    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}