using Microsoft.Extensions.Options;

namespace HobbyShopApp.Services;

// Dựng URL xem ảnh từ khóa lưu trong database. Tách riêng khỏi IImageStorage để việc XEM ảnh
// không phụ thuộc vào việc đã có khóa bí mật của R2 hay chưa.
public interface IImageUrlBuilder
{
    string GetUrl(string storageKey);
}

public class ImageUrlBuilder : IImageUrlBuilder
{
    private readonly string _baseUrl;

    public ImageUrlBuilder(IOptions<R2Options> options)
    {
        _baseUrl = (options.Value.PublicBaseUrl ?? "").TrimEnd('/');
    }

    // Chưa cấu hình PublicBaseUrl thì trả về chính khóa (để dễ phát hiện khi quên cấu hình).
    public string GetUrl(string storageKey) =>
        string.IsNullOrEmpty(_baseUrl) ? storageKey : $"{_baseUrl}/{storageKey}";
}