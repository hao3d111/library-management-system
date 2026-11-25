using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;

namespace LMS.Business.Services.Implementations;

public class ReportService : IReportService
{
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly ISystemSettingRepository _settingRepository;
    private readonly IBorrowingService _borrowingService;

    public ReportService(
        IBookRepository bookRepository,
        IUserRepository userRepository,
        ICategoryRepository categoryRepository,
        IBorrowingRepository borrowingRepository,
        ISystemSettingRepository settingRepository,
        IBorrowingService borrowingService)
    {
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _borrowingRepository = borrowingRepository;
        _settingRepository = settingRepository;
        _borrowingService = borrowingService;
    }

    public async Task<ReportOverviewDto> GetOverviewAsync(ReportFilterQuery query, CancellationToken ct = default)
    {
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate.Value.Date > query.EndDate.Value.Date)
        {
            throw new ValidationException("Start date cannot be later than end date.", new Dictionary<string, string[]>
            {
                ["startDate"] = new[] { "Start date must be on or before end date." }
            });
        }

        await _borrowingService.SyncOverdueStatusesAsync(ct);

        var (totalBooks, totalCopies, availableCopies) = await _bookRepository.GetInventoryTotalsAsync(ct);
        var totalMembers = await _userRepository.CountByRoleAsync(UserRole.LibraryMember, ct);
        var categories = await _categoryRepository.GetAllWithBookCountsAsync(ct);
        var settings = await _settingRepository.GetCurrentAsync(ct);
        var dailyFine = settings?.DailyFineAmount ?? 1.00m;

        var transactions = await _borrowingRepository.GetAllAsync(
            startDate: query.StartDate,
            endDate: query.EndDate,
            ct: ct);

        var now = DateTime.UtcNow;
        var activeBorrowings = transactions.Count(t => t.Status == BorrowingStatus.Borrowed || t.Status == BorrowingStatus.Overdue);
        var overdueTransactions = transactions.Count(t => t.Status == BorrowingStatus.Overdue);
        var returnedTransactions = transactions.Count(t => t.Status == BorrowingStatus.Returned);
        var totalFinesCollected = transactions
            .Where(t => t.Status == BorrowingStatus.Returned)
            .Sum(t => t.FineAmount);

        var estimatedPendingFines = transactions
            .Where(t => t.Status == BorrowingStatus.Overdue)
            .Sum(t => Math.Round(Math.Max(0, (now.Date - t.DueDate.Date).Days) * dailyFine, 2, MidpointRounding.AwayFromZero));

        var borrowCountsByCategory = transactions
            .Where(t => t.Book?.Category is not null)
            .GroupBy(t => t.Book!.Category!.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        var categoryBreakdown = categories
            .Select(c => new CategoryStatDto
            {
                CategoryName = c.Name,
                BookCount = c.Books?.Count ?? 0,
                BorrowCount = borrowCountsByCategory.GetValueOrDefault(c.Name, 0)
            })
            .OrderByDescending(c => c.BorrowCount)
            .ThenBy(c => c.CategoryName)
            .ToList();

        var recent = transactions.Take(10).Select(tx =>
        {
            var effectiveEnd = tx.ReturnDate ?? now;
            var daysOverdue = Math.Max(0, (effectiveEnd.Date - tx.DueDate.Date).Days);
            var estFine = tx.Status == BorrowingStatus.Returned
                ? tx.FineAmount
                : Math.Round(daysOverdue * dailyFine, 2, MidpointRounding.AwayFromZero);

            return new BorrowingDto
            {
                Id = tx.Id,
                MemberId = tx.MemberId,
                MemberName = tx.Member?.FullName ?? string.Empty,
                MemberCode = tx.Member?.MembershipCode ?? string.Empty,
                MemberEmail = tx.Member?.Email ?? string.Empty,
                BookId = tx.BookId,
                BookTitle = tx.Book?.Title ?? string.Empty,
                BookAuthor = tx.Book?.Author ?? string.Empty,
                BookISBN = tx.Book?.ISBN ?? string.Empty,
                ProcessedByName = tx.ProcessedByUser?.FullName ?? string.Empty,
                BorrowDate = tx.BorrowDate,
                DueDate = tx.DueDate,
                ReturnDate = tx.ReturnDate,
                Status = tx.Status.ToString(),
                DaysOverdue = daysOverdue,
                FineAmount = tx.FineAmount,
                EstimatedFine = estFine
            };
        }).ToList();

        return new ReportOverviewDto
        {
            TotalBooks = totalBooks,
            TotalCopies = totalCopies,
            AvailableCopies = availableCopies,
            TotalRegisteredMembers = totalMembers,
            ActiveBorrowings = activeBorrowings,
            OverdueTransactions = overdueTransactions,
            ReturnedTransactions = returnedTransactions,
            TotalTransactionsInRange = transactions.Count,
            TotalFinesCollected = totalFinesCollected,
            EstimatedPendingFines = estimatedPendingFines,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            GeneratedAtUtc = now,
            CategoryBreakdown = categoryBreakdown,
            RecentTransactions = recent
        };
    }
}
