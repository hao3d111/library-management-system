using LMS.Data.Enums;

namespace LMS.Data.Entities;

public class BorrowingTransaction
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int BookId { get; set; }
    public int ProcessedByUserId { get; set; }
    public DateTime BorrowDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public BorrowingStatus Status { get; set; } = BorrowingStatus.Borrowed;
    public decimal FineAmount { get; set; }

    public User? Member { get; set; }
    public Book? Book { get; set; }
    public User? ProcessedByUser { get; set; }
}
