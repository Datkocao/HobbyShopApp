namespace HobbyShopApp.Models;

public enum TopupStatus
{
    Pending,
    Approved,
    Rejected
}

// Yêu cầu nạp ví: người dùng chuyển khoản, quản trị đối chiếu rồi mới duyệt để cộng tiền.
public class TopupRequest
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long Amount { get; set; }

    // Nội dung chuyển khoản duy nhất, giúp quản trị đối chiếu nhanh.
    public string TransferCode { get; set; } = null!;

    // Ảnh chứng từ chuyển khoản trên R2 (bucket riêng, không công khai).
    public string? ProofStorageKey { get; set; }

    public TopupStatus Status { get; set; } = TopupStatus.Pending;

    public long? ReviewedByUserId { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    // Dòng sổ giao dịch được tạo khi duyệt.
    public long? LedgerEntryId { get; set; }
    public LedgerEntry? LedgerEntry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}