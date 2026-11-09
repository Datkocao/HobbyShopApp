namespace HobbyShopApp.Models;

// Mỗi người dùng có đúng một giỏ hàng.
public class Cart
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CartItem> Items { get; set; } = new();
}