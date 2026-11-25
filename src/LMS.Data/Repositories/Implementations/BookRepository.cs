using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Repositories.Implementations;

public class BookRepository : IBookRepository
{
    private readonly LibraryDbContext _context;

    public BookRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public Task<Book?> GetByIdAsync(int id, bool includeCategory = true, CancellationToken ct = default)
    {
        IQueryable<Book> query = _context.Books;
        if (includeCategory)
        {
            query = query.Include(b => b.Category);
        }
        return query.FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<List<Book>> SearchAsync(
        string? query,
        string? title,
        string? author,
        string? isbn,
        int? categoryId,
        CancellationToken ct = default)
    {
        IQueryable<Book> books = _context.Books
            .AsNoTracking()
            .Include(b => b.Category);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            var cleanTerm = term.Replace("-", string.Empty).Replace(" ", string.Empty);
            books = books.Where(b =>
                b.Title.ToLower().Contains(term) ||
                b.Author.ToLower().Contains(term) ||
                b.ISBN.ToLower().Contains(term) ||
                b.ISBN.Replace("-", "").ToLower().Contains(cleanTerm));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim().ToLower();
            books = books.Where(b => b.Title.ToLower().Contains(t));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            var a = author.Trim().ToLower();
            books = books.Where(b => b.Author.ToLower().Contains(a));
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            var raw = isbn.Trim().ToLower();
            var clean = raw.Replace("-", string.Empty).Replace(" ", string.Empty);
            books = books.Where(b =>
                b.ISBN.ToLower().Contains(raw) ||
                b.ISBN.Replace("-", "").ToLower().Contains(clean));
        }

        if (categoryId.HasValue)
        {
            books = books.Where(b => b.CategoryId == categoryId.Value);
        }

        return await books.OrderBy(b => b.Title).ToListAsync(ct);
    }

    public Task<bool> ExistsByIsbnAsync(string normalizedIsbn, int? excludeId = null, CancellationToken ct = default)
    {
        var clean = normalizedIsbn.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return _context.Books.AsNoTracking().AnyAsync(
            b => b.ISBN.Replace("-", "").Replace(" ", "").ToUpper() == clean &&
                 (!excludeId.HasValue || b.Id != excludeId.Value), ct);
    }

    public Task<bool> HasActiveBorrowingsAsync(int bookId, CancellationToken ct = default)
    {
        return _context.BorrowingTransactions.AsNoTracking().AnyAsync(
            t => t.BookId == bookId && (t.Status == BorrowingStatus.Borrowed || t.Status == BorrowingStatus.Overdue), ct);
    }

    public Task<bool> HasAnyBorrowingsAsync(int bookId, CancellationToken ct = default)
    {
        return _context.BorrowingTransactions.AsNoTracking().AnyAsync(t => t.BookId == bookId, ct);
    }

    public Task<int> CountActiveBorrowingsAsync(int bookId, CancellationToken ct = default)
    {
        return _context.BorrowingTransactions.AsNoTracking().CountAsync(
            t => t.BookId == bookId && (t.Status == BorrowingStatus.Borrowed || t.Status == BorrowingStatus.Overdue), ct);
    }

    public async Task<(int TotalBooks, int TotalCopies, int AvailableCopies)> GetInventoryTotalsAsync(CancellationToken ct = default)
    {
        var totals = await _context.Books
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalBooks = g.Count(),
                TotalCopies = g.Sum(b => b.TotalQuantity),
                AvailableCopies = g.Sum(b => b.AvailableQuantity)
            })
            .FirstOrDefaultAsync(ct);

        return totals is null
            ? (0, 0, 0)
            : (totals.TotalBooks, totals.TotalCopies, totals.AvailableCopies);
    }

    public async Task AddAsync(Book book, CancellationToken ct = default)
    {
        await _context.Books.AddAsync(book, ct);
    }

    public void Update(Book book)
    {
        _context.Books.Update(book);
    }

    public void Remove(Book book)
    {
        var returnedRows = _context.BorrowingTransactions
            .Where(t => t.BookId == book.Id && t.Status == BorrowingStatus.Returned)
            .ToList();
        if (returnedRows.Count > 0)
        {
            _context.BorrowingTransactions.RemoveRange(returnedRows);
        }
        _context.Books.Remove(book);
    }
}
