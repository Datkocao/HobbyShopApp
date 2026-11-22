using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Thông tin tài khoản nhận tiền nạp (lấy từ system_settings, không viết cứng trong code).
public class BankInfoDto
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
}

// Nội dung gửi lên khi tạo yêu cầu nạp. Amount tính bằng đồng.
public class CreateTopupRequest
{
    public long Amount { get; set; }
}

// Một yêu cầu nạp, trả về cho người dùng. Không lộ khóa file chứng từ, chỉ cho biết đã có hay chưa.
public class TopupDto
{
    public long Id { get; set; }
    public long Amount { get; set; }
    public string TransferCode { get; set; } = "";
    public TopupStatus Status { get; set; }
    public bool HasProof { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}