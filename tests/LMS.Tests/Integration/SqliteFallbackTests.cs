using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Data;
using LMS.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.Tests.Integration;

public class SqliteFallbackTests
{
    [Fact]
    public async Task AutoProvider_WhenSqlServerUnavailable_FallsBackToPortableSqliteAndRunsFullCycle()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lms_fallback_{Guid.NewGuid():N}.db");

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DatabaseProvider", "Auto");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=.\\NONEXISTENT_LMS_INSTANCE_9999;Database=MissingDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=1;");
            builder.UseSetting("Sqlite:FilePath", tempDbPath);
        });

        try
        {
            using var client = factory.CreateClient();

            // Verify fallback was selected and SQLite file was created
            var dbInfo = factory.Services.GetRequiredService<DatabaseConfigurationInfo>();
            Assert.Equal(DatabaseProviderType.Sqlite, dbInfo.Provider);
            Assert.True(dbInfo.UsedSqliteFallback);
            Assert.True(File.Exists(tempDbPath));

            // Verify login & seeded data work on SQLite
            var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = "librarian",
                Password = "Librarian@123"
            });
            Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
            var session = await loginResp.Content.ReadFromJsonAsync<LoginResponse>(LmsWebApplicationFactory.JsonOptions);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);

            // Verify book search works on SQLite
            var books = await client.GetFromJsonAsync<List<BookDto>>("/api/books?q=Dune", LmsWebApplicationFactory.JsonOptions);
            var dune = Assert.Single(books!);
            var startStock = dune.AvailableQuantity;

            // Verify transactional borrow + return works on SQLite
            var members = await client.GetFromJsonAsync<List<MemberDto>>("/api/members?search=Alice", LmsWebApplicationFactory.JsonOptions);
            var alice = members!.First();

            var borrowResp = await client.PostAsJsonAsync("/api/borrowings", new CreateBorrowingRequest
            {
                MemberId = alice.Id,
                BookId = dune.Id
            });
            Assert.Equal(HttpStatusCode.Created, borrowResp.StatusCode);
            var tx = await borrowResp.Content.ReadFromJsonAsync<BorrowingDto>(LmsWebApplicationFactory.JsonOptions);

            var afterBorrow = await client.GetFromJsonAsync<BookDto>($"/api/books/{dune.Id}", LmsWebApplicationFactory.JsonOptions);
            Assert.Equal(startStock - 1, afterBorrow!.AvailableQuantity);

            var returnResp = await client.PutAsync($"/api/borrowings/{tx!.Id}/return", null);
            Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);

            var afterReturn = await client.GetFromJsonAsync<BookDto>($"/api/books/{dune.Id}", LmsWebApplicationFactory.JsonOptions);
            Assert.Equal(startStock, afterReturn!.AvailableQuantity);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
