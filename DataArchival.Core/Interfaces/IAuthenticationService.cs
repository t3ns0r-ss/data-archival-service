using DataArchival.Core.Models;

namespace DataArchival.Core.Interfaces;

public interface IAuthenticationService
{
    Task<bool> RegisterUserAsync(LoginRequest request);
    Task<LoginResponse?> AuthenticateAsync(LoginRequest request);
    Task<List<string>> GetUserRolesAsync(string username);
    Task<bool> HasRoleAsync(string username, string role);
    Task<bool> HasTableAccessAsync(string username, string tableName);
    bool ValidateToken(string token);
    Task<LoginResponse> RefreshToken(string oldToken);
}