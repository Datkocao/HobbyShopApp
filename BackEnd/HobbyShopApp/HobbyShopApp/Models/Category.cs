namespace HobbyShopApp.Models
{
    public class Category
    {
        public long Id { get; set; }

        // Rỗng = danh mục gốc (tầng 1). Có giá trị = danh mục con (tầng 2).
        public long? ParentId { get; set; }
        public Category? Parent { get; set; }
        public List<Category> Children { get; set; } = new();

        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<Product> Products { get; set; } = new();
    }
}
