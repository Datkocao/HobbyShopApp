namespace HobbyShopApp.Models
{
    public class Productimage
    {
        public long Id { get; set; }

        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Đường dẫn (key) của file trên Cloudflare R2. Không lưu URL đầy đủ;
        // URL được dựng lại từ key và địa chỉ gốc trong cấu hình.
        public string StorageKey { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }

        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
