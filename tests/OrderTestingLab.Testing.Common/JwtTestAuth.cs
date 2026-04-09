using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrderTestingLab.Testing.Common;

/// <summary>
/// Giá trị ký JWT và issuer/audience dùng chung cho API test (WebApplicationFactory + test client).
/// Phải trùng với <c>Jwt</c> trong <c>appsettings.json</c> của API (host test nạp cùng file đó).
/// </summary>
public static class JwtTestAuth
{
    public const string Issuer = "OrderTestingLab";
    public const string Audience = "OrderTestingLab";
    public const string SigningKey = "OrderTestingLab-Dev-Signing-Key-MinimumLength32Chars!!";
}

/// <summary>
/// Sinh JWT Bearer cho integration/E2E (mock claims/roles).
/// </summary>
public static class JwtTestTokenFactory
{
    public static string CreateToken(IEnumerable<string> roles, string? subject = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTestAuth.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject ?? "test-user"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var token = new JwtSecurityToken(
            JwtTestAuth.Issuer,
            JwtTestAuth.Audience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
