namespace HobbyShopApp.Models;

public enum WalletType
{
    User,       // ví của một người dùng
    Platform    // ví của hệ thống (nhận tiền đơn hàng của shop)
}

public class Wallet
{
    public long Id { get; set; }

    // Rỗng với ví hệ thống.
    public long? UserId { get; set; }
    public User? User { get; set; }

    public WalletType WalletType { get; set; } = WalletType.User;

    // Số dư hiện tại (đồng). Đây là bản lưu nhanh; nguồn sự thật là tổng các dòng trong LedgerEntry.
    public long Balance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}