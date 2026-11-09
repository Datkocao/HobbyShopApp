namespace HobbyShopApp.Models;

public enum ProductStatus
{
    Draft,
    PendingReview,
    Active,
    Rejected,
    Removed
}

public class Product
{
    public long Id { get; set; }

    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Rỗng = hàng của shop (B2C). Có giá trị = hàng của Seller (C2C, làm ở giai đoạn sau).
    public long? SellerUserId { get; set; }
    public User? Seller { get; set; }

    // Người tạo bản ghi (staff đăng hàng của shop), phục vụ truy vết.
    public long CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }

    // Giá và tồn kho. Tiền lưu bằng số nguyên theo đồng, không dùng số thực.
    public long Price { get; set; }
    public int StockQuantity { get; set; }
    public int WeightGrams { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public bool IsFeatured { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Productimage> Images { get; set; } = new();
}