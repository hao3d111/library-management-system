namespace LMS.Business.DTOs;

public class SystemSettingsDto
{
    public int Id { get; set; }
    public int MaxBorrowLimit { get; set; }
    public int LoanDurationDays { get; set; }
    public decimal DailyFineAmount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateSystemSettingsRequest
{
    public int MaxBorrowLimit { get; set; }
    public int LoanDurationDays { get; set; }
    public decimal DailyFineAmount { get; set; }
}
