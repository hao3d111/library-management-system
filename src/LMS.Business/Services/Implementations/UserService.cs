using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LMS.Business.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IBorrowingRepository borrowingRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _borrowingRepository = borrowingRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserDto>> GetAllAsync(string? role = null, string? search = null, CancellationToken ct = default)
    {
        UserRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role.Trim(), ignoreCase: true, out var r))
            {
                throw new ValidationException($"Invalid role filter '{role}'. Valid values: Administrator, Librarian, LibraryMember.");
            }
            parsedRole = r;
        }

        var users = await _userRepository.GetAllAsync(parsedRole, search, ct);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"User with ID {id} was not found.");
        return MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
            errors["username"] = new[] { "Username is required and must be at least 3 characters." };
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            errors["password"] = new[] { "Password is required and must be at least 6 characters." };
        if (string.IsNullOrWhiteSpace(request.FullName))
            errors["fullName"] = new[] { "Full name is required." };
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            errors["email"] = new[] { "A valid email address is required." };
        if (string.IsNullOrWhiteSpace(request.Role) || !Enum.TryParse<UserRole>(request.Role.Trim(), true, out var role))
            errors["role"] = new[] { "Valid role is required (Administrator, Librarian, LibraryMember)." };
        else if (!Enum.IsDefined(typeof(UserRole), role))
            errors["role"] = new[] { "Valid role is required (Administrator, Librarian, LibraryMember)." };

        if (errors.Count > 0)
            throw new ValidationException("Invalid user registration data.", errors);

        var parsedRole = Enum.Parse<UserRole>(request.Role!.Trim(), true);
        var username = request.Username!.Trim();
        var email = request.Email!.Trim();

        if (await _userRepository.ExistsByUsernameAsync(username, ct: ct))
            throw new ConflictException($"Username '{username}' is already taken.");
        if (await _userRepository.ExistsByEmailAsync(email, ct: ct))
            throw new ConflictException($"Email '{email}' is already registered.");

        string? membershipCode = request.MembershipCode?.Trim();
        if (parsedRole == UserRole.LibraryMember && string.IsNullOrWhiteSpace(membershipCode))
        {
            membershipCode = await GenerateMembershipCodeAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(membershipCode) && await _userRepository.ExistsByMembershipCodeAsync(membershipCode, ct: ct))
        {
            throw new ConflictException($"Membership code '{membershipCode}' is already in use.");
        }

        var user = new User
        {
            Username = username,
            FullName = request.FullName!.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            Role = parsedRole,
            MembershipCode = parsedRole == UserRole.LibraryMember ? membershipCode : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password!);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, int currentAdminId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"User with ID {id} was not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (!email.Contains('@'))
                throw new ValidationException("A valid email address is required.");
            if (await _userRepository.ExistsByEmailAsync(email, excludeId: id, ct: ct))
                throw new ConflictException($"Email '{email}' is already registered.");
            user.Email = email;
        }

        if (request.Phone is not null)
            user.Phone = request.Phone.Trim();

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<UserRole>(request.Role.Trim(), true, out var newRole))
                throw new ValidationException("Invalid role specified.");
            if (id == currentAdminId && newRole != UserRole.Administrator)
                throw new ValidationException("You cannot demote your own administrator account.");
            user.Role = newRole;
            if (newRole == UserRole.LibraryMember && string.IsNullOrWhiteSpace(user.MembershipCode))
                user.MembershipCode = await GenerateMembershipCodeAsync(ct);
        }

        if (request.IsActive.HasValue)
        {
            if (id == currentAdminId && !request.IsActive.Value)
                throw new ValidationException("You cannot deactivate your own administrator account.");
            user.IsActive = request.IsActive.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 6)
                throw new ValidationException("Password must be at least 6 characters.");
            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(user);
    }

    public async Task DeleteAsync(int id, int currentAdminId, CancellationToken ct = default)
    {
        if (id == currentAdminId)
            throw new ValidationException("You cannot delete your own account.");

        var user = await _userRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"User with ID {id} was not found.");

        var activeBorrowings = await _borrowingRepository.CountActiveBorrowingsByMemberAsync(id, ct);
        if (activeBorrowings > 0)
            throw new ConflictException("Cannot delete a user who has active borrowing transactions.");

        var history = await _borrowingRepository.GetByMemberIdAsync(id, ct);
        if (history.Count > 0)
        {
            user.IsActive = false;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateMembershipCodeAsync(CancellationToken ct)
    {
        var count = await _userRepository.CountByRoleAsync(UserRole.LibraryMember, ct);
        for (int i = count + 1; i < count + 1000; i++)
        {
            var candidate = $"MEM-{i:D3}";
            if (!await _userRepository.ExistsByMembershipCodeAsync(candidate, ct: ct))
                return candidate;
        }
        return $"MEM-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        MembershipCode = user.MembershipCode,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
