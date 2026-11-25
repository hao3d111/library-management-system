using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Repositories.Implementations;

public class BorrowingRepository : IBorrowingRepository
{
    private readonly LibraryDbContext _context;

    public BorrowingRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public Task<BorrowingTransaction?> GetByIdAsync(int id, bool includeDetails = true, CancellationToken ct = default)
    {
        IQueryable<BorrowingTransaction> query = _context.BorrowingTransactions;
        if (includeDetails)
        {
            query = query
                .Include(t => t.Book).ThenInclude(b => b!.Category)
                .Include(t => t.Member)
                .Include(t => t.ProcessedByUser);
        }
        return query.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<List<BorrowingTransaction>> GetAllAsync(
        BorrowingStatus? status = null,
        int? memberId = null,
        int? bookId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        IQueryable<BorrowingTransaction> query = _context.BorrowingTransactions
            .AsNoTracking()
            .Include(t => t.Book).ThenInclude(b => b!.Category)
            .Include(t => t.Member)
            .Include(t => t.ProcessedByUser);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (memberId.HasValue)
        {
            query = query.Where(t => t.MemberId == memberId.Value);
        }

        if (bookId.HasValue)
        {
            query = query.Where(t => t.BookId == bookId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(t => t.BorrowDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endInclusive = endDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.BorrowDate <= endInclusive);
        }

        return await query.OrderByDescending(t => t.BorrowDate).ThenByDescending(t => t.Id).ToListAsync(ct);
    }

    public Task<List<BorrowingTransaction>> GetByMemberIdAsync(int memberId, CancellationToken ct = default)
    {
        return _context.BorrowingTransactions
            .AsNoTracking()
            .Include(t => t.Book).ThenInclude(b => b!.Category)
            .Include(t => t.Member)
            .Include(t => t.ProcessedByUser)
            .Where(t => t.MemberId == memberId)
            .OrderByDescending(t => t.BorrowDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync(ct);
    }

    public Task<int> CountActiveBorrowingsByMemberAsync(int memberId, CancellationToken ct = default)
    {
        return _context.BorrowingTransactions.CountAsync(
            t => t.MemberId == memberId && (t.Status == BorrowingStatus.Borrowed || t.Status == BorrowingStatus.Overdue), ct);
    }

    public Task<List<BorrowingTransaction>> GetPastDueActiveBorrowingsAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var todayUtc = utcNow.Date;
        return _context.BorrowingTransactions
            .Where(t => t.Status == BorrowingStatus.Borrowed && t.DueDate.Date < todayUtc)
            .ToListAsync(ct);
    }

    public async Task<List<BorrowingTransaction>> GetOverdueTransactionsAsync(string? search = null, CancellationToken ct = default)
    {
        IQueryable<BorrowingTransaction> query = _context.BorrowingTransactions
            .AsNoTracking()
            .Include(t => t.Book).ThenInclude(b => b!.Category)
            .Include(t => t.Member)
            .Include(t => t.ProcessedByUser)
            .Where(t => t.Status == BorrowingStatus.Overdue);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                (t.Member != null && (t.Member.FullName.ToLower().Contains(term) ||
                                      t.Member.Email.ToLower().Contains(term) ||
                                      (t.Member.MembershipCode != null && t.Member.MembershipCode.ToLower().Contains(term)))) ||
                (t.Book != null && (t.Book.Title.ToLower().Contains(term) ||
                                    t.Book.ISBN.ToLower().Contains(term))));
        }

        return await query.OrderBy(t => t.DueDate).ToListAsync(ct);
    }

    public async Task AddAsync(BorrowingTransaction transaction, CancellationToken ct = default)
    {
        await _context.BorrowingTransactions.AddAsync(transaction, ct);
    }

    public void Update(BorrowingTransaction transaction)
    {
        _context.BorrowingTransactions.Update(transaction);
    }
}
