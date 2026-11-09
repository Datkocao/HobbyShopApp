using System.ComponentModel.DataAnnotations;
using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Dữ liệu gửi lên khi SỬA sản phẩm. Không có tồn kho: tồn kho đổi qua API riêng (PATCH .../stock)
// để tránh ghi đè nhầm khi có đơn hàng đang trừ kho cùng lúc.
public class ProductUpdateRequest
{
    [Range(1, long.MaxValue)]
    public long CategoryId { get; set; }

    [Required, StringLength(250)]
    public string Name { get; set; } = null!;

    [StringLength(10000)]
    public string? Description { get; set; }

    // Giá tính bằng đồng, số nguyên.
    [Range(0, 100_000_000_000)]
    public long Price { get; set; }

    [Range(0, 1_000_000)]
    public int WeightGrams { get; set; }

    public bool IsFeatured { get; set; }

    // Hàng của shop chỉ dùng Draft (nháp, chưa bán) hoặc Active (đang bán).
    public ProductStatus Status { get; set; } = ProductStatus.Active;
}

// Khi TẠO sản phẩm thì có thêm tồn kho ban đầu.
public class ProductCreateRequest : ProductUpdateRequest
{
    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }
}

// Tăng hoặc giảm tồn kho: Delta dương là nhập thêm, âm là xuất bớt.
public class StockAdjustRequest
{
    [Range(-1_000_000, 1_000_000)]
    public int Delta { get; set; }

    [StringLength(250)]
    public string? Reason { get; set; }
}

// Các điều kiện lọc, sắp xếp, phân trang của danh sách sản phẩm (nhận từ query string).
public class ProductQuery
{
    public long? CategoryId { get; set; }          // lọc theo danh mục (danh mục gốc sẽ gồm cả danh mục con)
    public string? Keyword { get; set; }           // tìm theo tên
    public long? MinPrice { get; set; }
    public long? MaxPrice { get; set; }

    // newest (mặc định), price_asc, price_desc, name
    public string? Sort { get; set; }

    // Hai tùy chọn dưới đây chỉ có tác dụng với người có quyền product.manage.
    public bool IncludeAllStatuses { get; set; }
    public ProductStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProductListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public long Price { get; set; }
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsFeatured { get; set; }
    public ProductStatus Status { get; set; }

    // Khóa ảnh đại diện trên R2 (chưa phải URL; bước upload ảnh sẽ chuyển thành URL).
    public string? PrimaryImageKey { get; set; }

    public string? PrimaryImageUrl { get; set; }
}

public class ProductImageDto
{
    public long Id { get; set; }
    public string StorageKey { get; set; } = null!;
    public string Url { get; set; } = "";

    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public class ProductDetailDto
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string CategorySlug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public long Price { get; set; }
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public int WeightGrams { get; set; }
    public bool IsFeatured { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ProductImageDto> Images { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}