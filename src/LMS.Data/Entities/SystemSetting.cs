namespace LMS.Data.Entities;

public class SystemSetting
{
    public int Id { get; set; }
    public int MaxBorrowLimit { get; set; } = 5;
    public int LoanDurationDays { get; set; } = 14;
    public decimal DailyFineAmount { get; set; } = 1.00m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
