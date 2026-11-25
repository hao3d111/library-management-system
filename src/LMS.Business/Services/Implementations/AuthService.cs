using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Security;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LMS.Business.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors["username"] = new[] { "Username is required." };
        }
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Password is required." };
        }
        if (errors.Count > 0)
        {
            throw new ValidationException("Username and password are required.", errors);
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username!, ct);
        if (user is null)
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("This account is deactivated. Please contact an administrator.");
        }

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            RedirectUrl = GetRoleRedirectUrl(user.Role),
            User = MapToProfile(user)
        };
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User with ID {userId} was not found.");
        return MapToProfile(user);
    }

    private static string GetRoleRedirectUrl(UserRole role) => role switch
    {
        UserRole.Administrator => "/app.html#dashboard",
        UserRole.Librarian => "/app.html#books",
        UserRole.LibraryMember => "/app.html#catalog",
        _ => "/app.html#catalog"
    };

    private static UserProfileDto MapToProfile(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        MembershipCode = user.MembershipCode,
        Role = user.Role.ToString(),
        IsActive = user.IsActive
    };
}
