using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Một yêu cầu nạp, dành cho quản trị: có thêm thông tin người nạp để đối chiếu.
public class AdminTopupDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public string UserFullName { get; set; } = "";
    public long Amount { get; set; }
    public string TransferCode { get; set; } = "";
    public TopupStatus Status { get; set; }
    public bool HasProof { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
}

// Chi tiết một yêu cầu: kèm link xem ảnh chứng từ tạm thời (tự hết hạn).
public class AdminTopupDetailDto : AdminTopupDto
{
    public string? ProofUrl { get; set; }
    public DateTime? ProofUrlExpiresAt { get; set; }
}

public class AdminTopupListDto
{
    public List<AdminTopupDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}