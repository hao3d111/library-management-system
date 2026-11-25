namespace LMS.Business.DTOs;

public class MemberDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string MembershipCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveBorrowingsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMemberRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MembershipCode { get; set; }
}

public class UpdateMemberRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
    public string? NewPassword { get; set; }
}
