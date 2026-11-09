namespace HobbyShopApp.Models;

public enum LedgerEntryType
{
    Topup,          // nạp ví
    OrderPayment,   // thanh toán đơn hàng
    Refund,         // hoàn tiền
    Adjustment      // điều chỉnh của quản trị
}

// Sổ giao dịch: chỉ THÊM, không bao giờ sửa hay xóa. Nhập sai thì thêm một dòng bút toán ngược.
public class LedgerEntry
{
    public long Id { get; set; }

    public long WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    // Số có dấu: dương = tiền vào ví, âm = tiền ra khỏi ví.
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }

    public LedgerEntryType EntryType { get; set; }

    // Dòng này sinh ra từ đâu (ví dụ "Order" và Id của đơn).
    public string? RefType { get; set; }
    public long? RefId { get; set; }

    // Hai dòng của cùng một lần chuyển tiền giữa hai ví dùng chung mã này.
    public Guid? TransferGroupId { get; set; }

    // Khóa chống ghi trùng: cùng một thao tác chạy hai lần cũng chỉ tạo một dòng.
    public string IdempotencyKey { get; set; } = null!;

    public string? Note { get; set; }

    public long? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}