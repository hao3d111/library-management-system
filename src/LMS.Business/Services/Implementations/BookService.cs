using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Business.Validation;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;

namespace LMS.Business.Services.Implementations;

public class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BookService(
        IBookRepository bookRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _bookRepository = bookRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<BookDto>> SearchAsync(BookSearchQuery query, CancellationToken ct = default)
    {
        var books = await _bookRepository.SearchAsync(
            query.Q,
            query.Title,
            query.Author,
            query.ISBN,
            query.CategoryId,
            ct);

        return books.Select(MapToDto).ToList();
    }

    public async Task<BookDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, includeCategory: true, ct)
            ?? throw new NotFoundException($"Book with ID {id} was not found.");
        return MapToDto(book);
    }

    public async Task<BookDto> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        ValidateBookInput(request.Title, request.Author, request.ISBN, request.CategoryId, request.TotalQuantity);

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new ValidationException($"Category with ID {request.CategoryId} does not exist.",
                new Dictionary<string, string[]> { ["categoryId"] = new[] { "Selected category does not exist." } });

        var normalizedIsbn = IsbnValidator.Normalize(request.ISBN!);
        if (await _bookRepository.ExistsByIsbnAsync(normalizedIsbn, ct: ct))
        {
            throw new ConflictException($"A book with ISBN '{normalizedIsbn}' already exists.");
        }

        var book = new Book
        {
            Title = request.Title!.Trim(),
            Author = request.Author!.Trim(),
            ISBN = normalizedIsbn,
            CategoryId = category.Id,
            TotalQuantity = request.TotalQuantity,
            AvailableQuantity = request.TotalQuantity,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _bookRepository.AddAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        book.Category = category;
        return MapToDto(book);
    }

    public async Task<BookDto> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, includeCategory: true, ct)
            ?? throw new NotFoundException($"Book with ID {id} was not found.");

        ValidateBookInput(request.Title, request.Author, request.ISBN, request.CategoryId, request.TotalQuantity);

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new ValidationException($"Category with ID {request.CategoryId} does not exist.",
                new Dictionary<string, string[]> { ["categoryId"] = new[] { "Selected category does not exist." } });

        var normalizedIsbn = IsbnValidator.Normalize(request.ISBN!);
        if (await _bookRepository.ExistsByIsbnAsync(normalizedIsbn, excludeId: id, ct: ct))
        {
            throw new ConflictException($"A book with ISBN '{normalizedIsbn}' already exists.");
        }

        var activeBorrowedCount = await _bookRepository.CountActiveBorrowingsAsync(id, ct);
        if (request.TotalQuantity < activeBorrowedCount)
        {
            throw new ValidationException(
                $"Total quantity ({request.TotalQuantity}) cannot be less than the number of currently borrowed copies ({activeBorrowedCount}).",
                new Dictionary<string, string[]>
                {
                    ["totalQuantity"] = new[] { $"Must be at least {activeBorrowedCount} (copies currently borrowed)." }
                });
        }

        book.Title = request.Title!.Trim();
        book.Author = request.Author!.Trim();
        book.ISBN = normalizedIsbn;
        book.CategoryId = category.Id;
        book.Category = category;
        book.TotalQuantity = request.TotalQuantity;
        book.AvailableQuantity = request.TotalQuantity - activeBorrowedCount;
        book.Description = request.Description?.Trim();

        _bookRepository.Update(book);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(book);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, includeCategory: false, ct)
            ?? throw new NotFoundException($"Book with ID {id} was not found.");

        if (await _bookRepository.HasActiveBorrowingsAsync(id, ct))
        {
            throw new ConflictException("Cannot delete this book because copies are currently borrowed or overdue.");
        }

        _bookRepository.Remove(book);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static void ValidateBookInput(string? title, string? author, string? isbn, int categoryId, int totalQuantity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
            errors["title"] = new[] { "Title is required." };
        if (string.IsNullOrWhiteSpace(author))
            errors["author"] = new[] { "Author is required." };
        if (string.IsNullOrWhiteSpace(isbn))
            errors["isbn"] = new[] { "ISBN is required." };
        else if (!IsbnValidator.IsValid(isbn))
            errors["isbn"] = new[] { "ISBN must be a valid ISBN-10 or ISBN-13 format." };
        if (categoryId <= 0)
            errors["categoryId"] = new[] { "A valid category is required." };
        if (totalQuantity < 0)
            errors["totalQuantity"] = new[] { "Quantity cannot be negative." };

        if (errors.Count > 0)
            throw new ValidationException("Book validation failed.", errors);
    }

    private static BookDto MapToDto(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        ISBN = book.ISBN,
        CategoryId = book.CategoryId,
        CategoryName = book.Category?.Name ?? string.Empty,
        TotalQuantity = book.TotalQuantity,
        AvailableQuantity = book.AvailableQuantity,
        Description = book.Description,
        CreatedAt = book.CreatedAt
    };
}
