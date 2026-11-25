namespace LMS.Business.DTOs;

public class CreateBorrowingRequest
{
    public int MemberId { get; set; }
    public int BookId { get; set; }
}

public class BorrowingDto
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string BookISBN { get; set; } = string.Empty;
    public string ProcessedByName { get; set; } = string.Empty;
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
    public decimal FineAmount { get; set; }
    public decimal EstimatedFine { get; set; }
}
