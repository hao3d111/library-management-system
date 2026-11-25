using LMS.Data.Entities;

namespace LMS.Data.Repositories.Interfaces;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id, bool includeCategory = true, CancellationToken ct = default);
    Task<List<Book>> SearchAsync(string? query, string? title, string? author, string? isbn, int? categoryId, CancellationToken ct = default);
    Task<bool> ExistsByIsbnAsync(string normalizedIsbn, int? excludeId = null, CancellationToken ct = default);
    Task<bool> HasActiveBorrowingsAsync(int bookId, CancellationToken ct = default);
    Task<bool> HasAnyBorrowingsAsync(int bookId, CancellationToken ct = default);
    Task<int> CountActiveBorrowingsAsync(int bookId, CancellationToken ct = default);
    Task<(int TotalBooks, int TotalCopies, int AvailableCopies)> GetInventoryTotalsAsync(CancellationToken ct = default);
    Task AddAsync(Book book, CancellationToken ct = default);
    void Update(Book book);
    void Remove(Book book);
}
