namespace HobbyShopApp.Models;

// Sổ địa chỉ giao hàng của người dùng.
public class Address
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string RecipientName { get; set; } = null!;
    public string Phone { get; set; } = null!;

    // Lưu dạng chữ: đơn vị hành chính ở Việt Nam đã có thay đổi, nên chưa ràng buộc vào bảng danh mục cứng.
    public string Province { get; set; } = null!;
    public string District { get; set; } = null!;
    public string Ward { get; set; } = null!;
    public string DetailLine { get; set; } = null!;

    public bool IsDefault { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}