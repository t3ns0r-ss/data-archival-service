using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DataArchival.Core.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IConnectionService _connectionService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly JwtSettings _jwtSettings;

    public AuthenticationService(
        IConnectionService connectionService,
        ILogger<AuthenticationService> logger,
        IOptions<JwtSettings> jwtSettings)
    {
        _connectionService = connectionService;
        _logger = logger;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<bool> RegisterUserAsync(LoginRequest request)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var passwordBytes = Encoding.UTF8.GetBytes(request.Password);
            var hashBytes = sha256.ComputeHash(passwordBytes);
            var hashedPassword = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO users (username, password_hash) VALUES (@username, @password_hash)";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@username", request.Username));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@password_hash", hashedPassword));

            var result = await command.ExecuteNonQueryAsync();
            return result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user {Username}", request.Username);
            return false;
        }
    }
    
    public async Task<LoginResponse?> AuthenticateAsync(LoginRequest request)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var passwordBytes = Encoding.UTF8.GetBytes(request.Password);
            var hashBytes = sha256.ComputeHash(passwordBytes);
            var hashedPassword = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM users WHERE username = @username and password_hash = @password_hash";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@username", request.Username));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@password_hash", hashedPassword));
            
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                if (reader.GetInt32(0) != 1)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            var roles = await GetUserRolesAsync(request.Username);
            var token = GenerateJwtToken(request.Username, roles);
            var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

            return new LoginResponse
            {
                Token = token,
                Expires = expires,
                Roles = roles
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate user {Username}", request.Username);
            throw;
        }
    }

    public async Task<List<string>> GetUserRolesAsync(string username)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT role FROM user_roles WHERE username = @username";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@username", username));

            var roles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles.Add(reader.GetString("role"));
            }

            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roles for user {Username}", username);
            throw;
        }
    }

    public async Task<bool> HasRoleAsync(string username, string role)
    {
        var roles = await GetUserRolesAsync(username);
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase) ||
               roles.Contains("admin", StringComparer.OrdinalIgnoreCase);
    }
    
    public bool ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
                
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> HasTableAccessAsync(string username, string tableName)
    {
        try
        {
            var roles = await GetUserRolesAsync(username);
            return roles.Contains("admin") || roles.Contains(tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check table access for user {Username} and table {TableName}", username, tableName);
            return false;
        }
    }

    private string GenerateJwtToken(string username, List<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.NameIdentifier, username)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    public async Task<LoginResponse> RefreshToken(string oldToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            var principal = tokenHandler.ValidateToken(oldToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = false, // Ignore expiration for refresh
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return null;

            var roles = await GetUserRolesAsync(username);
            var token = GenerateJwtToken(username, roles);
            var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

            return new LoginResponse
            {
                Token = token,
                Expires = expires,
                Roles = roles
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            return null;
        }
    }
}