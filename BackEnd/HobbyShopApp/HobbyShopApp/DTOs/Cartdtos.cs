using System.ComponentModel.DataAnnotations;

namespace HobbyShopApp.DTOs;

// Thêm sản phẩm vào giỏ (cộng dồn nếu sản phẩm đã có trong giỏ).
public class CartItemRequest
{
    [Range(1, long.MaxValue)]
    public long ProductId { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; }
}

// Đặt số lượng mới cho một dòng trong giỏ (số tuyệt đối). Muốn bỏ dòng thì dùng DELETE.
public class CartQuantityRequest
{
    [Range(1, 99)]
    public int Quantity { get; set; }
}

public class CartItemDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;

    // Giá HIỆN TẠI của sản phẩm (giỏ hàng không lưu giá, nên admin đổi giá là giỏ đổi theo).
    public long UnitPrice { get; set; }
    public int Quantity { get; set; }
    public long LineTotal { get; set; }

    public int StockQuantity { get; set; }
    public string? PrimaryImageUrl { get; set; }

    // false = sản phẩm không còn được bán (đã xóa, ngừng bán, danh mục bị ẩn).
    public bool Available { get; set; }

    // Lý do dòng này chưa thể đặt hàng (hết hàng, vượt tồn kho, ngừng bán). Rỗng nếu ổn.
    public string? Issue { get; set; }
}

public class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public int TotalQuantity { get; set; }

    // Tổng tiền của các dòng còn được bán.
    public long Subtotal { get; set; }

    // true = có ít nhất một dòng cần xử lý trước khi đặt hàng.
    public bool HasIssues { get; set; }
}