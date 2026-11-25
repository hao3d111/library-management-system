using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;

namespace LMS.Business.Services.Implementations;

public class BorrowingService : IBorrowingService
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISystemSettingRepository _settingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BorrowingService(
        IBorrowingRepository borrowingRepository,
        IBookRepository bookRepository,
        IUserRepository userRepository,
        ISystemSettingRepository settingRepository,
        IUnitOfWork unitOfWork)
    {
        _borrowingRepository = borrowingRepository;
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _settingRepository = settingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<BorrowingDto>> GetAllAsync(
        string? status = null,
        int? memberId = null,
        int? bookId = null,
        CancellationToken ct = default)
    {
        await SyncOverdueStatusesAsync(ct);

        BorrowingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BorrowingStatus>(status.Trim(), ignoreCase: true, out var s))
            {
                throw new ValidationException($"Invalid borrowing status '{status}'. Valid values: Borrowed, Returned, Overdue.");
            }
            parsedStatus = s;
        }

        var settings = await GetSettingsOrDefaultAsync(ct);
        var items = await _borrowingRepository.GetAllAsync(parsedStatus, memberId, bookId, ct: ct);
        return items.Select(t => MapToDto(t, settings.DailyFineAmount)).ToList();
    }

    public async Task<BorrowingDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var transaction = await _borrowingRepository.GetByIdAsync(id, includeDetails: true, ct)
            ?? throw new NotFoundException($"Borrowing transaction with ID {id} was not found.");
        var settings = await GetSettingsOrDefaultAsync(ct);
        return MapToDto(transaction, settings.DailyFineAmount);
    }

    public async Task<BorrowingDto> BorrowBookAsync(
        CreateBorrowingRequest request,
        int processedByUserId,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.MemberId <= 0)
            errors["memberId"] = new[] { "A valid member is required." };
        if (request.BookId <= 0)
            errors["bookId"] = new[] { "A valid book is required." };
        if (errors.Count > 0)
            throw new ValidationException("Invalid borrowing request.", errors);

        // Wrap every borrow operation in an explicit database transaction (NFR05)
        await using var transactionScope = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var member = await _userRepository.GetByIdAsync(request.MemberId, ct);
            if (member is null || member.Role != UserRole.LibraryMember)
            {
                throw new NotFoundException($"Library member with ID {request.MemberId} was not found.");
            }
            if (!member.IsActive)
            {
                throw new ValidationException("Cannot borrow books for an inactive member account.");
            }

            var book = await _bookRepository.GetByIdAsync(request.BookId, includeCategory: true, ct)
                ?? throw new NotFoundException($"Book with ID {request.BookId} was not found.");

            if (book.AvailableQuantity <= 0)
            {
                throw new ConflictException($"'{book.Title}' is currently out of stock (0 copies available).");
            }

            var settings = await GetSettingsOrDefaultAsync(ct);
            var activeCount = await _borrowingRepository.CountActiveBorrowingsByMemberAsync(member.Id, ct);
            if (activeCount >= settings.MaxBorrowLimit)
            {
                throw new ConflictException(
                    $"Member '{member.FullName}' has reached the maximum borrowing limit of {settings.MaxBorrowLimit} active books.");
            }

            var now = DateTime.UtcNow;
            var borrowing = new BorrowingTransaction
            {
                MemberId = member.Id,
                BookId = book.Id,
                ProcessedByUserId = processedByUserId,
                BorrowDate = now,
                DueDate = now.AddDays(settings.LoanDurationDays),
                Status = BorrowingStatus.Borrowed,
                FineAmount = 0m
            };

            book.AvailableQuantity -= 1;

            _bookRepository.Update(book);
            await _borrowingRepository.AddAsync(borrowing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await transactionScope.CommitAsync(ct);

            var created = await _borrowingRepository.GetByIdAsync(borrowing.Id, includeDetails: true, ct);
            if (created is not null)
            {
                return MapToDto(created, settings.DailyFineAmount);
            }

            borrowing.Member = member;
            borrowing.Book = book;
            return MapToDto(borrowing, settings.DailyFineAmount);
        }
        catch
        {
            await transactionScope.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BorrowingDto> ReturnBookAsync(int transactionId, CancellationToken ct = default)
    {
        // Wrap every return operation in an explicit database transaction (NFR05)
        await using var transactionScope = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var borrowing = await _borrowingRepository.GetByIdAsync(transactionId, includeDetails: true, ct)
                ?? throw new NotFoundException($"Borrowing transaction with ID {transactionId} was not found.");

            if (borrowing.Status == BorrowingStatus.Returned || borrowing.ReturnDate.HasValue)
            {
                throw new ConflictException("This borrowing transaction has already been returned.");
            }

            var book = await _bookRepository.GetByIdAsync(borrowing.BookId, includeCategory: true, ct)
                ?? throw new NotFoundException($"Associated book with ID {borrowing.BookId} was not found.");

            var settings = await GetSettingsOrDefaultAsync(ct);
            var returnDate = DateTime.UtcNow;
            var overdueDays = Math.Max(0, (returnDate.Date - borrowing.DueDate.Date).Days);
            var fineAmount = Math.Round(overdueDays * settings.DailyFineAmount, 2, MidpointRounding.AwayFromZero);

            borrowing.ReturnDate = returnDate;
            borrowing.Status = BorrowingStatus.Returned;
            borrowing.FineAmount = fineAmount;

            if (book.AvailableQuantity < book.TotalQuantity)
            {
                book.AvailableQuantity += 1;
            }

            _borrowingRepository.Update(borrowing);
            _bookRepository.Update(book);
            await _unitOfWork.SaveChangesAsync(ct);
            await transactionScope.CommitAsync(ct);

            borrowing.Book = book;
            return MapToDto(borrowing, settings.DailyFineAmount);
        }
        catch
        {
            await transactionScope.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<BorrowingDto>> GetMemberHistoryAsync(
        int targetMemberId,
        int requestingUserId,
        string requestingRole,
        CancellationToken ct = default)
    {
        // Strict member privacy isolation (NFR09 / TC05)
        if (string.Equals(requestingRole, UserRole.LibraryMember.ToString(), StringComparison.OrdinalIgnoreCase) &&
            targetMemberId != requestingUserId)
        {
            throw new ForbiddenException("You are not authorized to access another member's borrowing history.");
        }

        var member = await _userRepository.GetByIdAsync(targetMemberId, ct);
        if (member is null || member.Role != UserRole.LibraryMember)
        {
            throw new NotFoundException($"Library member with ID {targetMemberId} was not found.");
        }

        await SyncOverdueStatusesAsync(ct);
        var settings = await GetSettingsOrDefaultAsync(ct);
        var transactions = await _borrowingRepository.GetByMemberIdAsync(targetMemberId, ct);
        return transactions.Select(t => MapToDto(t, settings.DailyFineAmount)).ToList();
    }

    public async Task<List<BorrowingDto>> GetOverdueAsync(
        string? search = null,
        int? minDaysOverdue = null,
        CancellationToken ct = default)
    {
        await SyncOverdueStatusesAsync(ct);
        var settings = await GetSettingsOrDefaultAsync(ct);
        var overdue = await _borrowingRepository.GetOverdueTransactionsAsync(search, ct);

        var mapped = overdue.Select(t => MapToDto(t, settings.DailyFineAmount)).ToList();
        if (minDaysOverdue.HasValue && minDaysOverdue.Value > 0)
        {
            mapped = mapped.Where(x => x.DaysOverdue >= minDaysOverdue.Value).ToList();
        }

        return mapped;
    }

    public async Task<int> SyncOverdueStatusesAsync(CancellationToken ct = default)
    {
        var pastDue = await _borrowingRepository.GetPastDueActiveBorrowingsAsync(DateTime.UtcNow, ct);
        if (pastDue.Count == 0)
        {
            return 0;
        }

        foreach (var tx in pastDue)
        {
            tx.Status = BorrowingStatus.Overdue;
            _borrowingRepository.Update(tx);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return pastDue.Count;
    }

    private async Task<SystemSetting> GetSettingsOrDefaultAsync(CancellationToken ct)
    {
        return await _settingRepository.GetCurrentAsync(ct) ?? new SystemSetting
        {
            MaxBorrowLimit = 5,
            LoanDurationDays = 14,
            DailyFineAmount = 1.00m
        };
    }

    private static BorrowingDto MapToDto(BorrowingTransaction tx, decimal dailyFineRate)
    {
        var effectiveEndDate = tx.ReturnDate ?? DateTime.UtcNow;
        var daysOverdue = Math.Max(0, (effectiveEndDate.Date - tx.DueDate.Date).Days);
        var estimatedFine = tx.Status == BorrowingStatus.Returned
            ? tx.FineAmount
            : Math.Round(daysOverdue * dailyFineRate, 2, MidpointRounding.AwayFromZero);

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
            EstimatedFine = estimatedFine
        };
    }
}
