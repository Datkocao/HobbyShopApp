using System.ComponentModel.DataAnnotations;

namespace HobbyShopApp.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required, StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(20)]
    public string? Phone { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

public class AuthResponse
{
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public long UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
}