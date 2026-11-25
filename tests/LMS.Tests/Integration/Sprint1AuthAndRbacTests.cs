using System.Net;
using System.Net.Http.Json;
using LMS.Business.DTOs;
using LMS.Tests.Infrastructure;

namespace LMS.Tests.Integration;

[Collection("LmsIntegration")]
public class Sprint1AuthAndRbacTests
{
    private readonly LmsWebApplicationFactory _factory;

    public Sprint1AuthAndRbacTests(LmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("admin", "Admin@123", "Administrator", "/app.html#dashboard")]
    [InlineData("librarian", "Librarian@123", "Librarian", "/app.html#books")]
    [InlineData("member1", "Member@123", "LibraryMember", "/app.html#catalog")]
    public async Task TC01_ValidLogin_ReturnsJwtRoleAndRedirect(
        string username, string password, string expectedRole, string expectedRedirect)
    {
        var (client, session) = await _factory.CreateAuthenticatedClientAsync(username, password);

        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.Equal(expectedRole, session.User.Role);
        Assert.Equal(expectedRedirect, session.RedirectUrl);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileDto>(LmsWebApplicationFactory.JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal(username, profile!.Username);
        Assert.Equal(expectedRole, profile.Role);
    }

    [Theory]
    [InlineData("admin", "WrongPassword!")]
    [InlineData("nonexistent_user", "Admin@123")]
    public async Task TC02_InvalidCredentials_ReturnsUnauthorizedWithClearMessage(string username, string password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "Admin@123")]
    [InlineData("admin", "")]
    [InlineData("   ", "   ")]
    public async Task TC03_BlankCredentials_ReturnsBadRequestWithValidationErrors(string username, string password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("required", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TC04_ProtectedEndpoints_RejectUnauthenticatedAndUnauthorizedRoles()
    {
        // Unauthenticated requests -> 401
        using var anonClient = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/books")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/users")).StatusCode);

        // LibraryMember attempting staff/admin endpoints -> 403
        var (memberClient, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");
        var createBookResp = await memberClient.PostAsJsonAsync("/api/books", new CreateBookRequest
        {
            Title = "Unauthorized Book",
            Author = "Nobody",
            ISBN = "978-0201633610",
            CategoryId = 1,
            TotalQuantity = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, createBookResp.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/users")).StatusCode);

        // Librarian attempting Admin-only user management -> 403
        var (librarianClient, _) = await _factory.CreateAuthenticatedClientAsync("librarian", "Librarian@123");
        Assert.Equal(HttpStatusCode.Forbidden, (await librarianClient.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task TC05_MemberRoleIsolation_BlocksAccessToOtherMembersAndNeverExposesPasswordHashes()
    {
        var (member1Client, _) = await _factory.CreateAuthenticatedClientAsync("member1", "Member@123");
        var (_, member2Session) = await _factory.CreateAuthenticatedClientAsync("member2", "Member@123");

        // Member 1 cannot access Member 2's member record
        var otherMemberResp = await member1Client.GetAsync($"/api/members/{member2Session.User.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherMemberResp.StatusCode);

        // Admin user listing never exposes PasswordHash
        var (adminClient, _) = await _factory.CreateAuthenticatedClientAsync("admin", "Admin@123");
        var usersRaw = await adminClient.GetStringAsync("/api/users");
        Assert.DoesNotContain("passwordHash", usersRaw, StringComparison.OrdinalIgnoreCase);
    }
}
