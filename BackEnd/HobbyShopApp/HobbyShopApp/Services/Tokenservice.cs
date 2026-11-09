using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HobbyShopApp.Models;
using Microsoft.IdentityModel.Tokens;

namespace HobbyShopApp.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user, string roleCode, IEnumerable<string> permissions);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user, string roleCode, IEnumerable<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var minutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("role", roleCode),
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var jwt = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}