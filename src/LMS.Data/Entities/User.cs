using LMS.Data.Enums;

namespace LMS.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? MembershipCode { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BorrowingTransaction> Borrowings { get; set; } = new List<BorrowingTransaction>();
    public ICollection<BorrowingTransaction> ProcessedBorrowings { get; set; } = new List<BorrowingTransaction>();
}
