using LMS.Business.DTOs;
using LMS.Business.Services.Implementations;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Moq;

namespace LMS.Tests.Unit;

public class BorrowingServiceTests
{
    private readonly Mock<IBorrowingRepository> _borrowingRepo = new();
    private readonly Mock<IBookRepository> _bookRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ISystemSettingRepository> _settingRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDbTransactionScope> _txScope = new();
    private readonly BorrowingService _sut;

    public BorrowingServiceTests()
    {
        _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_txScope.Object);
        _settingRepo.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemSetting { MaxBorrowLimit = 5, LoanDurationDays = 14, DailyFineAmount = 1.50m });

        _sut = new BorrowingService(
            _borrowingRepo.Object,
            _bookRepo.Object,
            _userRepo.Object,
            _settingRepo.Object,
            _uow.Object);
    }

    [Fact]
    public async Task TC15_BorrowBook_WhenPersistenceFails_RollsBackTransaction()
    {
        _userRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 10, FullName = "Alice", Role = UserRole.LibraryMember, IsActive = true });
        _bookRepo.Setup(r => r.GetByIdAsync(20, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 20, Title = "Clean Code", TotalQuantity = 3, AvailableQuantity = 2 });
        _borrowingRepo.Setup(r => r.CountActiveBorrowingsByMemberAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB write error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.BorrowBookAsync(new CreateBorrowingRequest { MemberId = 10, BookId = 20 }, processedByUserId: 2));

        _txScope.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _txScope.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC15_ReturnBook_WhenPersistenceFails_RollsBackTransaction()
    {
        var tx = new BorrowingTransaction
        {
            Id = 5,
            MemberId = 10,
            BookId = 20,
            BorrowDate = DateTime.UtcNow.AddDays(-5),
            DueDate = DateTime.UtcNow.AddDays(9),
            Status = BorrowingStatus.Borrowed
        };
        _borrowingRepo.Setup(r => r.GetByIdAsync(5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);
        _bookRepo.Setup(r => r.GetByIdAsync(20, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 20, Title = "Clean Code", TotalQuantity = 3, AvailableQuantity = 1 });
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReturnBookAsync(5));

        _txScope.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _txScope.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC18_ReturnOnTime_CalculatesZeroFineAndCommitsTransaction()
    {
        var book = new Book { Id = 20, Title = "Clean Code", TotalQuantity = 3, AvailableQuantity = 1 };
        var tx = new BorrowingTransaction
        {
            Id = 6,
            MemberId = 10,
            BookId = 20,
            BorrowDate = DateTime.UtcNow.AddDays(-4),
            DueDate = DateTime.UtcNow.AddDays(10),
            Status = BorrowingStatus.Borrowed,
            Book = book
        };
        _borrowingRepo.Setup(r => r.GetByIdAsync(6, true, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _bookRepo.Setup(r => r.GetByIdAsync(20, true, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var result = await _sut.ReturnBookAsync(6);

        Assert.Equal("Returned", result.Status);
        Assert.Equal(0m, result.FineAmount);
        Assert.Equal(2, book.AvailableQuantity);
        _txScope.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC19_ReturnOverdue_CalculatesExactDailyFine()
    {
        var book = new Book { Id = 20, Title = "Clean Code", TotalQuantity = 3, AvailableQuantity = 1 };
        var tx = new BorrowingTransaction
        {
            Id = 7,
            MemberId = 10,
            BookId = 20,
            BorrowDate = DateTime.UtcNow.AddDays(-19),
            DueDate = DateTime.UtcNow.AddDays(-5),
            Status = BorrowingStatus.Overdue,
            Book = book
        };
        _borrowingRepo.Setup(r => r.GetByIdAsync(7, true, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _bookRepo.Setup(r => r.GetByIdAsync(20, true, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var result = await _sut.ReturnBookAsync(7);

        Assert.Equal("Returned", result.Status);
        Assert.Equal(5, result.DaysOverdue);
        Assert.Equal(7.50m, result.FineAmount); // 5 days * $1.50/day
        Assert.Equal(2, book.AvailableQuantity);
        _txScope.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
