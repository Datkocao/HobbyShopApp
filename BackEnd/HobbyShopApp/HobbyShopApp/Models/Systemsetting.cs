namespace HobbyShopApp.Models;

// Cấu hình hệ thống dạng khóa - giá trị (ví dụ khóa ảnh QR nạp tiền, các tham số nghiệp vụ).
public class SystemSetting
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;

    public long? UpdatedByUserId { get; set; }
    public User? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}