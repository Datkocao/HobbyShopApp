namespace HobbyShopApp.Models;

// Một dòng trong đơn. Tên, giá, cân nặng được CHỤP lại lúc đặt hàng.
public class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductName { get; set; } = null!;
    public long UnitPrice { get; set; }
    public int Quantity { get; set; }
    public long LineTotal { get; set; }
    public int WeightGrams { get; set; }
}