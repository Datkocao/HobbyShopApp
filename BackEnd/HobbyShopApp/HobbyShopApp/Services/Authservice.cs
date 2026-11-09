using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IAuthService
{
    Task<(AuthResponse? Response, string? Error)> RegisterAsync(RegisterRequest request);
    Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<(AuthResponse? Response, string? Error)> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (null, "Email đã được đăng ký.");

        var buyerRole = await _db.Roles.FirstAsync(r => r.Code == "BUYER");

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            RoleId = buyerRole.Id,
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (null, "Email đã được đăng ký.");
        }

        return (await BuildResponseAsync(user, buyerRole.Code), null);
    }

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        const string invalid = "Email hoặc mật khẩu không đúng.";

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

        if (user is null)
            return (null, invalid);

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return (null, invalid);

        if (user.Status == UserStatus.Locked)
            return (null, "Tài khoản đã bị khóa.");

        return (await BuildResponseAsync(user, user.Role.Code), null);
    }

    private async Task<AuthResponse> BuildResponseAsync(User user, string roleCode)
    {
        var permissions = await _db.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync();

        var (token, expiresAt) = _tokens.CreateToken(user, roleCode, permissions);

        return new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = roleCode,
        };
    }
}