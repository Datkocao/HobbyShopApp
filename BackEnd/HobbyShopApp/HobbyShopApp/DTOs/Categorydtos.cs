using System.ComponentModel.DataAnnotations;

namespace HobbyShopApp.DTOs;

// Dữ liệu gửi lên khi tạo hoặc sửa danh mục (dùng chung một lớp).
public class CategoryRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    // Rỗng = danh mục gốc. Có giá trị = danh mục con của danh mục có Id đó.
    public long? ParentId { get; set; }

    [Range(0, 100000)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

// Dữ liệu trả về. Children chứa các danh mục con (rỗng nếu là danh mục con tầng 2).
public class CategoryDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryDto> Children { get; set; } = new();
}