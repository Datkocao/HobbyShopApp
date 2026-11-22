using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace HobbyShopApp.Services;

// Nơi lưu file RIÊNG TƯ (ví dụ ảnh chứng từ chuyển khoản). Khác với IImageStorage (ảnh công khai):
// file ở đây không có địa chỉ công khai, chỉ xem được qua link tạm thời có chữ ký và tự hết hạn.
public interface IPrivateFileStorage
{
    Task UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);

    // Tạo link xem tạm thời cho một file riêng tư. Hết hạn sau "lifetime" thì link vô dụng.
    string GetTemporaryUrl(string key, TimeSpan lifetime);
}

public class R2PrivateFileStorage : IPrivateFileStorage, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;

    public R2PrivateFileStorage(IOptions<R2Options> options)
    {
        var o = options.Value;

        if (string.IsNullOrWhiteSpace(o.AccountId) ||
            string.IsNullOrWhiteSpace(o.AccessKeyId) ||
            string.IsNullOrWhiteSpace(o.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(o.PrivateBucket))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình R2 riêng tư. Cần R2:AccountId, R2:AccessKeyId, R2:SecretAccessKey (user-secrets) và R2:PrivateBucket (appsettings.json).");
        }

        _bucket = o.PrivateBucket;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{o.AccountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
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

            // Hai cờ bắt buộc với R2 (giống R2ImageStorage).
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };

        // File riêng tư: không cho trình duyệt hay CDN lưu đệm.
        request.Headers.CacheControl = "private, no-store";

        await _s3.PutObjectAsync(request, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_bucket, key, ct);
    }

    public string GetTemporaryUrl(string key, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS,
            Expires = DateTime.UtcNow.Add(lifetime),
        };

        return _s3.GetPreSignedURL(request);
    }

    public void Dispose()
    {
        _s3.Dispose();
    }
}