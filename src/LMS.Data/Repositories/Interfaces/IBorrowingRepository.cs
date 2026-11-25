using LMS.Data.Entities;
using LMS.Data.Enums;

namespace LMS.Data.Repositories.Interfaces;

public interface IBorrowingRepository
{
    Task<BorrowingTransaction?> GetByIdAsync(int id, bool includeDetails = true, CancellationToken ct = default);
    Task<List<BorrowingTransaction>> GetAllAsync(BorrowingStatus? status = null, int? memberId = null, int? bookId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);
    Task<List<BorrowingTransaction>> GetByMemberIdAsync(int memberId, CancellationToken ct = default);
    Task<int> CountActiveBorrowingsByMemberAsync(int memberId, CancellationToken ct = default);
    Task<List<BorrowingTransaction>> GetPastDueActiveBorrowingsAsync(DateTime utcNow, CancellationToken ct = default);
    Task<List<BorrowingTransaction>> GetOverdueTransactionsAsync(string? search = null, CancellationToken ct = default);
    Task AddAsync(BorrowingTransaction transaction, CancellationToken ct = default);
    void Update(BorrowingTransaction transaction);
}
