using System.Net;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Tests.Infrastructure;

namespace LMS.Tests.Integration;

[Collection("LmsIntegration")]
public class Sprint1BooksAndMembersTests
{
    private readonly LmsWebApplicationFactory _factory;

    public Sprint1BooksAndMembersTests(LmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TC06_CreateBook_WithValidFieldsAndIsbn_ReturnsCreated()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);
        var categoryId = categories!.First().Id;

        var response = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "Domain-Driven Design",
            Author = "Eric Evans",
            ISBN = "978-0321125217",
            CategoryId = categoryId,
            TotalQuantity = 3,
            Description = "Tackling Complexity in the Heart of Software."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Domain-Driven Design", created!.Title);
        Assert.Equal(3, created.TotalQuantity);
        Assert.Equal(3, created.AvailableQuantity);
    }

    [Theory]
    [InlineData("", "Author", "978-0201633610", 2)]
    [InlineData("Valid Title", "", "978-0201633610", 2)]
    [InlineData("Valid Title", "Author", "NOT-AN-ISBN", 2)]
    [InlineData("Valid Title", "Author", "978-0201633619", 2)] // invalid ISBN-13 checksum
    [InlineData("Valid Title", "Author", "978-0201633610", -1)]
    public async Task TC07_CreateBook_InvalidInputOrBadIsbn_ReturnsBadRequest(
        string title, string author, string isbn, int quantity)
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);

        var response = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = title,
            Author = author,
            ISBN = isbn,
            CategoryId = categories!.First().Id,
            TotalQuantity = quantity
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TC08_CreateBook_DuplicateIsbn_ReturnsConflict()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);

        // Seeded "Clean Code" has ISBN 978-0132350884; also test without hyphen
        var response = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "Clean Code Duplicate",
            Author = "Robert C. Martin",
            ISBN = "9780132350884",
            CategoryId = categories!.First().Id,
            TotalQuantity = 2
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TC09_UpdateBook_SucceedsAndProtectsActiveBorrowQuantityInvariant()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?q=Clean%20Code", LmsWebApplicationFactory.JsonOptions);
        var cleanCode = books!.Single(b => b.ISBN == "978-0132350884");

        // Clean Code has 1 active borrow -> lowering TotalQuantity to 0 must fail (400)
        var invalidUpdate = await librarianClient.PutAsJsonAsync($"/api/books/{cleanCode.Id}", new UpdateBookRequest
        {
            Title = cleanCode.Title,
            Author = cleanCode.Author,
            ISBN = cleanCode.ISBN,
            CategoryId = cleanCode.CategoryId,
            TotalQuantity = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        // Valid update increasing TotalQuantity to 6 -> AvailableQuantity becomes 5 (6 - 1 borrowed)
        var validUpdate = await librarianClient.PutAsJsonAsync($"/api/books/{cleanCode.Id}", new UpdateBookRequest
        {
            Title = "Clean Code (Updated Edition)",
            Author = cleanCode.Author,
            ISBN = cleanCode.ISBN,
            CategoryId = cleanCode.CategoryId,
            TotalQuantity = 6,
            Description = cleanCode.Description
        });
        Assert.Equal(HttpStatusCode.OK, validUpdate.StatusCode);
        var updated = await validUpdate.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.Equal("Clean Code (Updated Edition)", updated!.Title);
        Assert.Equal(6, updated.TotalQuantity);
        Assert.Equal(5, updated.AvailableQuantity);

        // Restore original title & quantity so other tests see clean state
        await librarianClient.PutAsJsonAsync($"/api/books/{cleanCode.Id}", new UpdateBookRequest
        {
            Title = "Clean Code",
            Author = cleanCode.Author,
            ISBN = cleanCode.ISBN,
            CategoryId = cleanCode.CategoryId,
            TotalQuantity = 5,
            Description = cleanCode.Description
        });
    }

    [Fact]
    public async Task TC10_DeleteBook_SucceedsWhenUnused_AndBlockedWhenActivelyBorrowed()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);

        // 1. Create a brand-new book with no borrowings -> Delete succeeds (204)
        var createResp = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "Temporary Disposable Book",
            Author = "Test Author",
            ISBN = "978-0201633610",
            CategoryId = categories!.First().Id,
            TotalQuantity = 2
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);

        var deleteUnused = await librarianClient.DeleteAsync($"/api/books/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteUnused.StatusCode);

        var afterDelete = await librarianClient.GetAsync($"/api/books/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);

        // 2. Attempt to delete seeded "Clean Code" which has an active borrowing -> 409 Conflict
        var books = await librarianClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=978-0132350884", LmsWebApplicationFactory.JsonOptions);
        var borrowedBook = books!.Single();

        var deleteBorrowed = await librarianClient.DeleteAsync($"/api/books/{borrowedBook.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteBorrowed.StatusCode);
    }

    [Fact]
    public async Task MemberManagement_RegisterSearchUpdateAndLogin_Succeeds()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");

        // Register new member
        var createResp = await librarianClient.PostAsJsonAsync("/api/members", new CreateMemberRequest
        {
            Username = "charlie_reader",
            Password = "Password@123",
            FullName = "Charlie Reader",
            Email = "charlie.reader@lms.local",
            Phone = "555-0999"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var member = await createResp.Content.ReadFromJsonAsync<MemberDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(member);
        Assert.StartsWith("MEM-", member!.MembershipCode);

        // Search member
        var found = await librarianClient.GetFromJsonAsync<List<MemberDto>>("/api/members?search=Charlie", LmsWebApplicationFactory.JsonOptions);
        Assert.Contains(found!, m => m.Id == member.Id);

        // Update member
        var updateResp = await librarianClient.PutAsJsonAsync($"/api/members/{member.Id}", new UpdateMemberRequest
        {
            FullName = "Charlie Updated",
            Email = member.Email,
            Phone = "555-1000",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<MemberDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.Equal("Charlie Updated", updated!.FullName);

        // Verify newly registered member can log in with hashed password
        var (_, loginSession) = await _factory.CreateAuthenticatedClientAsync("charlie_reader", "Password@123");
        Assert.Equal("LibraryMember", loginSession.User.Role);
    }

    [Fact]
    public async Task CategoryManagement_CreateUpdateDeleteAndBlockWhenInUse_Succeeds()
    {
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");

        // Create empty category
        var createResp = await librarianClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = "Philosophy",
            Description = "Classical and modern philosophy"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var cat = await createResp.Content.ReadFromJsonAsync<CategoryDto>(LmsWebApplicationFactory.JsonOptions);

        // Update category
        var updateResp = await librarianClient.PutAsJsonAsync($"/api/categories/{cat!.Id}", new UpdateCategoryRequest
        {
            Name = "Philosophy & Ethics",
            Description = "Updated description"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Delete empty category succeeds
        Assert.Equal(HttpStatusCode.NoContent, (await librarianClient.DeleteAsync($"/api/categories/{cat.Id}")).StatusCode);

        // Deleting seeded category containing books is blocked (409)
        var allCats = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);
        var usedCat = allCats!.First(c => c.BookCount > 0);
        Assert.Equal(HttpStatusCode.Conflict, (await librarianClient.DeleteAsync($"/api/categories/{usedCat.Id}")).StatusCode);
    }
}
