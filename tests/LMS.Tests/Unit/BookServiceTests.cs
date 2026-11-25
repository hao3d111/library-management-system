using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Implementations;
using LMS.Business.Validation;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;
using Moq;

namespace LMS.Tests.Unit;

public class BookServiceTests
{
    private readonly Mock<IBookRepository> _bookRepo = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly BookService _sut;

    public BookServiceTests()
    {
        _sut = new BookService(_bookRepo.Object, _categoryRepo.Object, _uow.Object);
    }

    [Theory]
    [InlineData("978-0132350884", true)]
    [InlineData("9780132350884", true)]
    [InlineData("0-306-40615-2", true)]
    [InlineData("0-8044-2957-X", true)]
    [InlineData("978-0132350889", false)] // bad checksum
    [InlineData("12345", false)]
    [InlineData("INVALID-ISBN", false)]
    [InlineData("", false)]
    public void IsbnValidator_ValidatesIsbn10AndIsbn13Correctly(string isbn, bool expected)
    {
        Assert.Equal(expected, IsbnValidator.IsValid(isbn));
    }

    [Fact]
    public async Task CreateAsync_WhenIsbnAlreadyExists_ThrowsConflictException()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { Id = 1, Name = "Software Engineering" });
        _bookRepo.Setup(r => r.ExistsByIsbnAsync("978-0132350884", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateBookRequest
        {
            Title = "Duplicate Clean Code",
            Author = "Robert C. Martin",
            ISBN = "978-0132350884",
            CategoryId = 1,
            TotalQuantity = 2
        };

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task DeleteAsync_WhenBookHasActiveBorrowings_ThrowsConflictException()
    {
        _bookRepo.Setup(r => r.GetByIdAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 1, Title = "Clean Code" });
        _bookRepo.Setup(r => r.HasActiveBorrowingsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(1));
        _bookRepo.Verify(r => r.Remove(It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenTotalQuantityLessThanActiveBorrowings_ThrowsValidationException()
    {
        _bookRepo.Setup(r => r.GetByIdAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 1, Title = "Clean Code", ISBN = "978-0132350884", CategoryId = 1, TotalQuantity = 5, AvailableQuantity = 2 });
        _categoryRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { Id = 1, Name = "Software Engineering" });
        _bookRepo.Setup(r => r.ExistsByIsbnAsync("978-0132350884", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bookRepo.Setup(r => r.CountActiveBorrowingsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var request = new UpdateBookRequest
        {
            Title = "Clean Code",
            Author = "Robert C. Martin",
            ISBN = "978-0132350884",
            CategoryId = 1,
            TotalQuantity = 2
        };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.UpdateAsync(1, request));
    }
}
