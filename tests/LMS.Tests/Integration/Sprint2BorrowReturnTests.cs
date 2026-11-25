using System.Net;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Tests.Infrastructure;

namespace LMS.Tests.Integration;

[Collection("LmsIntegration")]
public class Sprint2BorrowReturnTests
{
    private readonly LmsWebApplicationFactory _factory;

    public Sprint2BorrowReturnTests(LmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TC05_MemberBorrowingHistory_AllowsOwnHistory_AndBlocksOtherMembersHistory()
    {
        var (member1Client, member1Session) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");
        var (_, member2Session) = await _factory.CreateAuthenticatedClientAsync("member2", "Member@123");

        // Own history via /my-history succeeds
        var myHistoryResp = await member1Client.GetAsync("/api/borrowings/my-history");
        Assert.Equal(HttpStatusCode.OK, myHistoryResp.StatusCode);
        var myHistory = await myHistoryResp.Content.ReadFromJsonAsync<List<BorrowingDto>>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(myHistory);
        Assert.NotEmpty(myHistory!);
        Assert.All(myHistory, r => Assert.Equal(member1Session.User.Id, r.MemberId));

        // Own history via /member/{ownId} succeeds
        var ownByIdResp = await member1Client.GetAsync($"/api/borrowings/member/{member1Session.User.Id}");
        Assert.Equal(HttpStatusCode.OK, ownByIdResp.StatusCode);

        // Accessing another member's history is strictly forbidden (403)
        var otherHistoryResp = await member1Client.GetAsync($"/api/borrowings/member/{member2Session.User.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherHistoryResp.StatusCode);
    }

    [Fact]
    public async Task TC11_BorrowAvailableBook_CreatesTransactionAndDecrementsStockAtomically()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (_, member1Session) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=978-1449373320", LmsWebApplicationFactory.JsonOptions);
        var ddia = books!.Single();
        var initialAvailable = ddia.AvailableQuantity;

        var borrowResp = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member1Session.User.Id,
            BookId = ddia.Id
        });

        Assert.Equal(HttpStatusCode.Created, borrowResp.StatusCode);
        var borrowing = await borrowResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(borrowing);
        Assert.Equal("Borrowed", borrowing!.Status);
        Assert.Equal(borrowing.BorrowDate.Date.AddDays(14), borrowing.DueDate.Date);

        // Verify book stock decremented by 1
        var updatedBook = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{ddia.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(initialAvailable - 1, updatedBook!.AvailableQuantity);

        // Cleanup: return to keep seed stock clean
        var returnResp = await librarianClient.PutAsync($"/api/borrowings/{borrowing.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
    }

    [Fact]
    public async Task TC12_BorrowOutOfStockBook_ReturnsConflictAndKeepsInventoryZero()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (_, member1Session) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        // "Refactoring" has TotalQuantity = 1, AvailableQuantity = 1
        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=978-0134757599", LmsWebApplicationFactory.JsonOptions);
        var singleCopyBook = books!.Single();

        // Borrow the only copy
        var firstBorrow = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member1Session.User.Id,
            BookId = singleCopyBook.Id
        });
        Assert.Equal(HttpStatusCode.Created, firstBorrow.StatusCode);
        var firstTx = await firstBorrow.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);

        // Second borrow must fail with 409 Conflict
        var secondBorrow = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member1Session.User.Id,
            BookId = singleCopyBook.Id
        });
        Assert.Equal(HttpStatusCode.Conflict, secondBorrow.StatusCode);

        // Stock must remain 0 (never negative)
        var afterCheck = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{singleCopyBook.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(0, afterCheck!.AvailableQuantity);

        // Cleanup
        await librarianClient.PutAsync($"/api/borrowings/{firstTx!.Id}/return", null);
    }

    [Fact]
    public async Task TC13_BorrowForInvalidOrInactiveMember_FailsCleanly()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=978-0441172719", LmsWebApplicationFactory.JsonOptions);
        var dune = books!.Single();

        // Non-existent member -> 404
        var missingMemberResp = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = 999999,
            BookId = dune.Id
        });
        Assert.Equal(HttpStatusCode.NotFound, missingMemberResp.StatusCode);

        // Create & deactivate a member -> 400
        var createMemberResp = await librarianClient.PostAsJsonAsync("/api/members", new CreateMemberRequest
        {
            Username = "inactive_borrow_test",
            Password = "Password@123",
            FullName = "Inactive Reader",
            Email = "inactive.reader@lms.local"
        });
        var member = await createMemberResp.Content.ReadFromJsonAsync<MemberDto>(LmsWebApplicationFactory.JsonOptions);
        await librarianClient.PutAsJsonAsync($"/api/members/{member!.Id}", new UpdateMemberRequest
        {
            FullName = member.FullName,
            Email = member.Email,
            IsActive = false
        });

        var inactiveResp = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member.Id,
            BookId = dune.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveResp.StatusCode);
    }

    [Fact]
    public async Task TC14_BorrowExceedingMaxLimit_ReturnsConflict()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);

        // Create a dedicated test member and a high-stock test book
        var memberResp = await librarianClient.PostAsJsonAsync("/api/members", new CreateMemberRequest
        {
            Username = "limit_tester",
            Password = "Password@123",
            FullName = "Limit Tester",
            Email = "limit.tester@lms.local"
        });
        var member = await memberResp.Content.ReadFromJsonAsync<MemberDto>(LmsWebApplicationFactory.JsonOptions);

        var bookResp = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "High Stock Test Book",
            Author = "Test Author",
            ISBN = "978-0596007126",
            CategoryId = categories!.First().Id,
            TotalQuantity = 10
        });
        var book = await bookResp.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);

        // Borrow 5 copies (default MaxBorrowLimit = 5)
        var txIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var r = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
            {
                MemberId = member!.Id,
                BookId = book!.Id
            });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var dto = await r.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);
            txIds.Add(dto!.Id);
        }

        // 6th borrow must fail with 409 Conflict
        var sixthResp = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member!.Id,
            BookId = book!.Id
        });
        Assert.Equal(HttpStatusCode.Conflict, sixthResp.StatusCode);

        // Verify inventory stayed exact (10 - 5 = 5)
        var afterBook = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{book!.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(5, afterBook!.AvailableQuantity);

        // Return all 5 and verify stock restores to 10 (TC15 inventory consistency)
        foreach (var txId in txIds)
        {
            var ret = await librarianClient.PutAsync($"/api/borrowings/{txId}/return", null);
            Assert.Equal(HttpStatusCode.OK, ret.StatusCode);
        }

        var restoredBook = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{book.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(10, restoredBook!.AvailableQuantity);
    }

    [Fact]
    public async Task TC16_TC17_TC18_ReturnBook_IncrementsStock_ZeroFineOnTime_AndRejectsDuplicateReturn()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (_, member1Session) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=978-0062316097", LmsWebApplicationFactory.JsonOptions);
        var sapiens = books!.Single();
        var startQty = sapiens.AvailableQuantity;

        // Borrow
        var borrowResp = await librarianClient.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
        {
            MemberId = member1Session.User.Id,
            BookId = sapiens.Id
        });
        var tx = await borrowResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);

        // TC16 + TC18: Return on time -> status Returned, ReturnDate set, Fine = $0.00, stock incremented
        var returnResp = await librarianClient.PutAsync($"/api/borrowings/{tx!.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
        var returned = await returnResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.Equal("Returned", returned!.Status);
        Assert.NotNull(returned.ReturnDate);
        Assert.Equal(0m, returned.FineAmount);

        var afterReturnBook = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{sapiens.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(startQty, afterReturnBook!.AvailableQuantity);

        // TC17: Duplicate return must be rejected (409) and MUST NOT double-increment stock
        var duplicateReturn = await librarianClient.PutAsync($"/api/borrowings/{tx.Id}/return", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateReturn.StatusCode);

        var afterDuplicateBook = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{sapiens.Id}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(startQty, afterDuplicateBook!.AvailableQuantity);
    }

    [Fact]
    public async Task TC19_ReturnOverdueTransaction_CalculatesExpectedFineAndRestoresStock()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");

        // Find the seeded overdue transaction (member2 borrowed The Pragmatic Programmer 20 days ago, DueDate 6 days ago)
        var overdueList = await librarianClient.GetFromJsonAsync<List<BorrowingDto>>("/api/borrowings?status=Overdue", LmsWebApplicationFactory.JsonOptions);
        var seededOverdue = overdueList!.First(t => t.BookISBN == "978-0135957059");

        var bookBefore = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{seededOverdue.BookId}", LmsWebApplicationFactory.JsonOptions);
        var expectedDays = Math.Max(0, (DateTime.UtcNow.Date - seededOverdue.DueDate.Date).Days);

        var returnResp = await librarianClient.PutAsync($"/api/borrowings/{seededOverdue.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
        var returned = await returnResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);

        Assert.Equal("Returned", returned!.Status);
        Assert.Equal(expectedDays, returned.DaysOverdue);
        Assert.Equal(expectedDays * 1.00m, returned.FineAmount);
        Assert.True(returned.FineAmount > 0m);

        var bookAfter = await librarianClient.GetFromJsonAsync<BookDto>($"/api/books/{seededOverdue.BookId}", LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(bookBefore!.AvailableQuantity + 1, bookAfter!.AvailableQuantity);
    }
}
