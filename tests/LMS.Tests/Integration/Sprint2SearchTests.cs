using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Tests.Infrastructure;

namespace LMS.Tests.Integration;

[Collection("LmsIntegration")]
public class Sprint2SearchTests
{
    private readonly LmsWebApplicationFactory _factory;

    public Sprint2SearchTests(LmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TC20_SearchBooksByTitle_ReturnsMatchesWithinPerformanceTarget()
    {
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var sw = Stopwatch.StartNew();
        var response = await memberClient.GetAsync("/api/books?title=Designing%20Data");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Search took {sw.ElapsedMilliseconds}ms, exceeding 3000ms target.");

        var books = await response.Content.ReadFromJsonAsync<List<BookDto>>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(books);
        Assert.Single(books!);
        Assert.Equal("Designing Data-Intensive Applications", books[0].Title);
    }

    [Fact]
    public async Task TC21_SearchBooksByAuthor_ReturnsMatches()
    {
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var books = await memberClient.GetFromJsonAsync<List<BookDto>>("/api/books?author=Frank%20Herbert", LmsWebApplicationFactory.JsonOptions);

        Assert.NotNull(books);
        Assert.Single(books!);
        Assert.Equal("Dune", books[0].Title);
    }

    [Fact]
    public async Task TC22_SearchBooksByIsbn_ReturnsMatchingBook()
    {
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        // Works both with hyphens and without hyphens
        var books = await memberClient.GetFromJsonAsync<List<BookDto>>("/api/books?isbn=9780262046305", LmsWebApplicationFactory.JsonOptions);

        Assert.NotNull(books);
        Assert.Single(books!);
        Assert.Equal("Introduction to Algorithms", books[0].Title);
    }

    [Fact]
    public async Task TC23_SearchBooksWithUnmatchedKeyword_ReturnsEmptyListCleanly()
    {
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var response = await memberClient.GetAsync("/api/books?q=NonExistentBookXYZ999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var books = await response.Content.ReadFromJsonAsync<List<BookDto>>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(books);
        Assert.Empty(books!);
    }

    [Fact]
    public async Task TC24_EmptySearchReturnsFullCatalog_AndBookDetailsReflectRealTimeAvailability()
    {
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        var catalog = await memberClient.GetFromJsonAsync<List<BookDto>>("/api/books?q=", LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(catalog);
        Assert.True(catalog!.Count >= 8);

        var target = catalog.First(b => b.ISBN == "978-0132350884");
        var detailResp = await memberClient.GetAsync($"/api/books/{target.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);

        var detail = await detailResp.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(target.AvailableQuantity, detail!.AvailableQuantity);
        Assert.Equal(target.TotalQuantity, detail.TotalQuantity);
        Assert.False(string.IsNullOrWhiteSpace(detail.CategoryName));
    }
}
