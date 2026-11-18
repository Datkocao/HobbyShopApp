using System.ComponentModel.DataAnnotations;

namespace HobbyShopApp.DTOs;

// Dữ liệu gửi lên khi tạo hoặc sửa địa chỉ (dùng chung một lớp).
public class AddressRequest
{
    [Required, StringLength(150)]
    public string RecipientName { get; set; } = null!;

    // Chấp nhận 0xxxxxxxxx hoặc +84xxxxxxxxx; dấu cách, dấu chấm, gạch ngang sẽ được bỏ đi.
    [Required, StringLength(20)]
    public string Phone { get; set; } = null!;

    [Required, StringLength(100)]
    public string Province { get; set; } = null!;

    [Required, StringLength(100)]
    public string District { get; set; } = null!;

    [Required, StringLength(100)]
    public string Ward { get; set; } = null!;

    [Required, StringLength(300)]
    public string DetailLine { get; set; } = null!;

    // Đặt làm địa chỉ mặc định. Địa chỉ đầu tiên luôn tự thành mặc định.
    public bool IsDefault { get; set; }
}

public class AddressDto
{
    public long Id { get; set; }
    public string RecipientName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string District { get; set; } = null!;
    public string Ward { get; set; } = null!;
    public string DetailLine { get; set; } = null!;

    // Địa chỉ ghép sẵn thành một dòng (dùng để hiển thị và để chụp vào đơn hàng sau này).
    public string FullAddress { get; set; } = null!;

    public bool IsDefault { get; set; }
}