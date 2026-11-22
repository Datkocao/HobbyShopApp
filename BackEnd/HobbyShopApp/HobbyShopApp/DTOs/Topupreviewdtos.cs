namespace HobbyShopApp.DTOs;

// Nội dung gửi lên khi từ chối một yêu cầu nạp. Lý do bắt buộc, tối đa 500 ký tự.
public class RejectTopupRequest
{
    public string Reason { get; set; } = "";
}