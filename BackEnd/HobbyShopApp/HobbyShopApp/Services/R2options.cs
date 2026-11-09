namespace HobbyShopApp.Services;

// Cấu hình Cloudflare R2, đọc từ mục "R2" trong cấu hình:
//   - Bucket, PublicBaseUrl: để trong appsettings.json (không bí mật)
//   - AccountId, AccessKeyId, SecretAccessKey: để trong user-secrets (bí mật)
public class R2Options
{
    public string AccountId { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Bucket { get; set; } = "";

    // Địa chỉ gốc để xem ảnh công khai, ví dụ https://pub-xxxx.r2.dev hoặc tên miền riêng.
    public string PublicBaseUrl { get; set; } = "";
}