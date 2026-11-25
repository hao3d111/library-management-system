namespace LMS.Business.DTOs;

public class ReportFilterQuery
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CategoryStatDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int BorrowCount { get; set; }
}

public class ReportOverviewDto
{
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int TotalRegisteredMembers { get; set; }
    public int ActiveBorrowings { get; set; }
    public int OverdueTransactions { get; set; }
    public int ReturnedTransactions { get; set; }
    public int TotalTransactionsInRange { get; set; }
    public decimal TotalFinesCollected { get; set; }
    public decimal EstimatedPendingFines { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<CategoryStatDto> CategoryBreakdown { get; set; } = new();
    public List<BorrowingDto> RecentTransactions { get; set; } = new();
}
