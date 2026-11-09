using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace HobbyShopApp.Services;

// Lớp trừu tượng cho nơi lưu ảnh. Code nghiệp vụ chỉ biết interface này,
// nên sau này đổi sang nhà cung cấp khác (S3, Azure...) chỉ cần viết lớp cài đặt mới.
public interface IImageStorage
{
    Task UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

// Cài đặt cho Cloudflare R2 (R2 tương thích API của Amazon S3 nên dùng AWSSDK.S3).
public class R2ImageStorage : IImageStorage, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;

    public R2ImageStorage(IOptions<R2Options> options)
    {
        var o = options.Value;

        if (string.IsNullOrWhiteSpace(o.AccountId) ||
            string.IsNullOrWhiteSpace(o.AccessKeyId) ||
            string.IsNullOrWhiteSpace(o.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(o.Bucket))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình R2. Cần R2:AccountId, R2:AccessKeyId, R2:SecretAccessKey (user-secrets) và R2:Bucket (appsettings.json).");
        }

        _bucket = o.Bucket;

        var config = new AmazonS3Config
        {
            // Địa chỉ API của R2 theo tài khoản Cloudflare.
            ServiceURL = $"https://{o.AccountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",   // R2 dùng vùng "auto"
            ForcePathStyle = true,
        };

        _s3 = new AmazonS3Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), config);
    }

    public async Task UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,

            // Hai cờ bắt buộc với R2: R2 chưa hỗ trợ kiểu ký "streaming SigV4" mà AWSSDK.S3 dùng mặc định.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };

        // Tên file chứa GUID ngẫu nhiên nên nội dung không bao giờ đổi: cho trình duyệt/CDN lưu đệm lâu dài.
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _s3.PutObjectAsync(request, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_bucket, key, ct);
    }

    public void Dispose()
    {
        _s3.Dispose();
    }
}