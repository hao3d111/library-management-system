using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LMS.Business.DTOs;
using LMS.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.Tests.Infrastructure;

public class LmsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string TestDatabaseName { get; } = $"LibraryManagementDb_Test_{Guid.NewGuid():N}";
    public string ConnectionString =>
        $"Server=.\\SQLEXPRESS;Database={TestDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            });
        });
    }

    public Task InitializeAsync()
    {
        // Trigger host build, migrations, and seeding
        using var client = CreateClient();
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Ignore cleanup errors if host was never initialized
        }

        await base.DisposeAsync();
    }

    public async Task<(HttpClient Client, LoginResponse Session)> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Login response was null.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return (client, session);
    }
}

[CollectionDefinition("LmsIntegration")]
public class LmsIntegrationCollection : ICollectionFixture<LmsWebApplicationFactory>
{
}
