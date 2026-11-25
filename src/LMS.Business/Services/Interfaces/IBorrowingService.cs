using LMS.Business.DTOs;

namespace LMS.Business.Services.Interfaces;

public interface IBorrowingService
{
    Task<List<BorrowingDto>> GetAllAsync(string? status = null, int? memberId = null, int? bookId = null, CancellationToken ct = default);
    Task<BorrowingDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<BorrowingDto> BorrowBookAsync(CreateBorrowingRequest request, int processedByUserId, CancellationToken ct = default);
    Task<BorrowingDto> ReturnBookAsync(int transactionId, CancellationToken ct = default);
    Task<List<BorrowingDto>> GetMemberHistoryAsync(int targetMemberId, int requestingUserId, string requestingRole, CancellationToken ct = default);
    Task<List<BorrowingDto>> GetOverdueAsync(string? search = null, int? minDaysOverdue = null, CancellationToken ct = default);
    Task<int> SyncOverdueStatusesAsync(CancellationToken ct = default);
}
