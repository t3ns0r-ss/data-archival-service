namespace DataArchival.Core.Models;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public List<string> Roles { get; set; } = new();
}