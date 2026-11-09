using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HobbyShopApp.Services;

public static class SlugHelper
{
    // "Mô hình Gundam" -> "mo-hinh-gundam"
    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // "đ" không tách dấu được bằng Normalize nên đổi riêng.
        var lower = text.Trim().ToLowerInvariant().Replace('đ', 'd');

        // Tách chữ và dấu ra khỏi nhau (ô -> o + dấu mũ), rồi bỏ phần dấu.
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        // Mọi ký tự không phải chữ/số thành dấu gạch ngang, gộp các dấu liên tiếp.
        var slug = Regex.Replace(sb.ToString(), "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }
}