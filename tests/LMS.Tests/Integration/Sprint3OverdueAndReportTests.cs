using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.Tests.Integration;

[Collection("LmsIntegration")]
public class Sprint3OverdueAndReportTests
{
    private readonly LmsWebApplicationFactory _factory;

    public Sprint3OverdueAndReportTests(LmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OverdueMonitoring_FlagsPastDueTransactionsAndSupportsFiltering()
    {
        var (librarianClient, librarianSession) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (_, member2Session) = await _factory.CreateAuthenticatedClientAsync("member2", "Member@123");

        // Create a dedicated book & simulate a past-due active Borrowed transaction (4 days overdue)
        var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);
        var bookResp = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "Overdue Monitor Test Book",
            Author = "QA Author",
            ISBN = "978-0131103627",
            CategoryId = categories!.First().Id,
            TotalQuantity = 2
        });
        var book = await bookResp.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);

        int simulatedTxId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var dbBook = await db.Books.FirstAsync(b => b.Id == book!.Id);
            dbBook.AvailableQuantity -= 1;

            var tx = new BorrowingTransaction
            {
                MemberId = member2Session.User.Id,
                BookId = dbBook.Id,
                ProcessedByUserId = librarianSession.User.Id,
                BorrowDate = DateTime.UtcNow.AddDays(-18),
                DueDate = DateTime.UtcNow.AddDays(-4),
                Status = BorrowingStatus.Borrowed, // Not flagged yet!
                FineAmount = 0m
            };
            db.BorrowingTransactions.Add(tx);
            await db.SaveChangesAsync();
            simulatedTxId = tx.Id;
        }

        // Calling /api/borrowings/overdue must automatically flag it as Overdue and return member/book details
        var overdueList = await librarianClient.GetFromJsonAsync<List<BorrowingDto>>(
            "/api/borrowings/overdue?search=Overdue%20Monitor&minDaysOverdue=3",
            LmsWebApplicationFactory.JsonOptions);

        Assert.NotNull(overdueList);
        var match = Assert.Single(overdueList!, x => x.Id == simulatedTxId);
        Assert.Equal("Overdue", match.Status);
        Assert.Equal(4, match.DaysOverdue);
        Assert.Equal("Bob Smith", match.MemberName);
        Assert.Equal("Overdue Monitor Test Book", match.BookTitle);
        Assert.True(match.EstimatedFine > 0m);

        // Filtering with minDaysOverdue=10 excludes the 4-day overdue transaction
        var filteredOut = await librarianClient.GetFromJsonAsync<List<BorrowingDto>>(
            "/api/borrowings/overdue?search=Overdue%20Monitor&minDaysOverdue=10",
            LmsWebApplicationFactory.JsonOptions);
        Assert.Empty(filteredOut!);

        // Cleanup by returning the transaction
        var returnResp = await librarianClient.PutAsync($"/api/borrowings/{simulatedTxId}/return", null);
        Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
    }

    [Fact]
    public async Task ConfigurableFineRules_AdminUpdatesRate_AppliesToOverdueReturn_AndBlocksNonAdmins()
    {
        var (adminClient, _) = await _factory.CreateAuthenticatedClientAsync("admin", "Admin@123");
        var (librarianClient, librarianSession) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (memberClient, member2Session) = await _factory.CreateAuthenticatedClientAsync("member2", "Member@123");

        // Non-admins cannot modify system settings
        var libPut = await librarianClient.PutAsJsonAsync("/api/settings", new UpdateSystemSettingsRequest
        {
            MaxBorrowLimit = 5,
            LoanDurationDays = 14,
            DailyFineAmount = 3.00m
        });
        Assert.Equal(HttpStatusCode.Forbidden, libPut.StatusCode);

        var memPut = await memberClient.PutAsJsonAsync("/api/settings", new UpdateSystemSettingsRequest
        {
            MaxBorrowLimit = 5,
            LoanDurationDays = 14,
            DailyFineAmount = 3.00m
        });
        Assert.Equal(HttpStatusCode.Forbidden, memPut.StatusCode);

        // Admin updates DailyFineAmount to $2.50/day
        var updateResp = await adminClient.PutAsJsonAsync("/api/settings", new UpdateSystemSettingsRequest
        {
            MaxBorrowLimit = 5,
            LoanDurationDays = 14,
            DailyFineAmount = 2.50m
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        try
        {
            // Seed a 4-day overdue loan and return it -> fine should be 4 * $2.50 = $10.00
            var categories = await librarianClient.GetFromJsonAsync<List<CategoryDto>>("/api/categories", LmsWebApplicationFactory.JsonOptions);
            var bookResp = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest
            {
                Title = "Fine Calculation Rule Book",
                Author = "Policy Author",
                ISBN = "978-0201616224",
                CategoryId = categories!.First().Id,
                TotalQuantity = 2
            });
            var book = await bookResp.Content.ReadFromJsonAsync<BookDto>(LmsWebApplicationFactory.JsonOptions);

            int txId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                var dbBook = await db.Books.FirstAsync(b => b.Id == book!.Id);
                dbBook.AvailableQuantity -= 1;

                var tx = new BorrowingTransaction
                {
                    MemberId = member2Session.User.Id,
                    BookId = dbBook.Id,
                    ProcessedByUserId = librarianSession.User.Id,
                    BorrowDate = DateTime.UtcNow.AddDays(-18),
                    DueDate = DateTime.UtcNow.AddDays(-4),
                    Status = BorrowingStatus.Overdue,
                    FineAmount = 0m
                };
                db.BorrowingTransactions.Add(tx);
                await db.SaveChangesAsync();
                txId = tx.Id;
            }

            var returnResp = await librarianClient.PutAsync($"/api/borrowings/{txId}/return", null);
            Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
            var returned = await returnResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);
            Assert.Equal(10.00m, returned!.FineAmount);
        }
        finally
        {
            // Restore default policy ($1.00/day)
            await adminClient.PutAsJsonAsync("/api/settings", new UpdateSystemSettingsRequest
            {
                MaxBorrowLimit = 5,
                LoanDurationDays = 14,
                DailyFineAmount = 1.00m
            });
        }
    }

    [Fact]
    public async Task ReportsOverview_EnforcesAdminRbac_SupportsDateFiltering_AndMeetsPerformanceTarget()
    {
        var (adminClient, _) = await _factory.CreateAuthenticatedClientAsync("admin", "Admin@123");
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");

        // RBAC: Only Administrator can access /api/reports/overview
        Assert.Equal(HttpStatusCode.Forbidden, (await librarianClient.GetAsync("/api/reports/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/reports/overview")).StatusCode);

        // Full overview under 5 seconds
        var sw = Stopwatch.StartNew();
        var overviewResp = await adminClient.GetAsync("/api/reports/overview");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, overviewResp.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Report generation took {sw.ElapsedMilliseconds}ms, exceeding 5000ms target.");

        var report = await overviewResp.Content.ReadFromJsonAsync<ReportOverviewDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(report);
        Assert.True(report!.TotalBooks >= 8);
        Assert.True(report.TotalRegisteredMembers >= 2);
        Assert.NotEmpty(report.CategoryBreakdown);

        // Date-range filtering (future window -> 0 transactions in range)
        var futureStart = DateTime.UtcNow.AddYears(2).ToString("yyyy-MM-dd");
        var futureEnd = DateTime.UtcNow.AddYears(2).AddDays(7).ToString("yyyy-MM-dd");
        var filtered = await adminClient.GetFromJsonAsync<ReportOverviewDto>(
            $"/api/reports/overview?startDate={futureStart}&endDate={futureEnd}",
            LmsWebApplicationFactory.JsonOptions);
        Assert.Equal(0, filtered!.TotalTransactionsInRange);
        Assert.Equal(0, filtered.ActiveBorrowings);
        Assert.Equal(0, filtered.OverdueTransactions);
        Assert.True(filtered.TotalBooks >= 8);

        // Invalid date range (start > end) -> 400 Bad Request
        var badRange = await adminClient.GetAsync("/api/reports/overview?startDate=2026-12-31&endDate=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
    }
}
