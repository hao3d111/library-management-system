namespace LMS.Business.DTOs;

public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? MembershipCode { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
    public UserProfileDto User { get; set; } = new();
}
