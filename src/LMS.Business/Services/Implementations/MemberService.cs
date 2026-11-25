using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LMS.Business.Services.Implementations;

public class MemberService : IMemberService
{
    private readonly IUserRepository _userRepository;
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public MemberService(
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

    public async Task<List<MemberDto>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        var members = await _userRepository.GetAllAsync(UserRole.LibraryMember, search, ct);
        var result = new List<MemberDto>(members.Count);
        foreach (var member in members)
        {
            var activeCount = await _borrowingRepository.CountActiveBorrowingsByMemberAsync(member.Id, ct);
            result.Add(MapToDto(member, activeCount));
        }
        return result;
    }

    public async Task<MemberDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var member = await _userRepository.GetByIdAsync(id, ct);
        if (member is null || member.Role != UserRole.LibraryMember)
        {
            throw new NotFoundException($"Library member with ID {id} was not found.");
        }

        var activeCount = await _borrowingRepository.CountActiveBorrowingsByMemberAsync(member.Id, ct);
        return MapToDto(member, activeCount);
    }

    public async Task<MemberDto> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
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

        if (errors.Count > 0)
            throw new ValidationException("Member registration validation failed.", errors);

        var username = request.Username!.Trim();
        var email = request.Email!.Trim();

        if (await _userRepository.ExistsByUsernameAsync(username, ct: ct))
            throw new ConflictException($"Username '{username}' is already taken.");
        if (await _userRepository.ExistsByEmailAsync(email, ct: ct))
            throw new ConflictException($"Email '{email}' is already registered.");

        var code = string.IsNullOrWhiteSpace(request.MembershipCode)
            ? await GenerateMembershipCodeAsync(ct)
            : request.MembershipCode.Trim().ToUpperInvariant();

        if (await _userRepository.ExistsByMembershipCodeAsync(code, ct: ct))
            throw new ConflictException($"Membership code '{code}' is already in use.");

        var member = new User
        {
            Username = username,
            FullName = request.FullName!.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            MembershipCode = code,
            Role = UserRole.LibraryMember,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        member.PasswordHash = _passwordHasher.HashPassword(member, request.Password!);

        await _userRepository.AddAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(member, 0);
    }

    public async Task<MemberDto> UpdateAsync(int id, UpdateMemberRequest request, CancellationToken ct = default)
    {
        var member = await _userRepository.GetByIdAsync(id, ct);
        if (member is null || member.Role != UserRole.LibraryMember)
            throw new NotFoundException($"Library member with ID {id} was not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            member.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (!email.Contains('@'))
                throw new ValidationException("A valid email address is required.");
            if (await _userRepository.ExistsByEmailAsync(email, excludeId: id, ct: ct))
                throw new ConflictException($"Email '{email}' is already registered.");
            member.Email = email;
        }

        if (request.Phone is not null)
            member.Phone = request.Phone.Trim();

        if (request.IsActive.HasValue)
            member.IsActive = request.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 6)
                throw new ValidationException("Password must be at least 6 characters.");
            member.PasswordHash = _passwordHasher.HashPassword(member, request.NewPassword);
        }

        _userRepository.Update(member);
        await _unitOfWork.SaveChangesAsync(ct);

        var activeCount = await _borrowingRepository.CountActiveBorrowingsByMemberAsync(member.Id, ct);
        return MapToDto(member, activeCount);
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

    private static MemberDto MapToDto(User member, int activeCount) => new()
    {
        Id = member.Id,
        Username = member.Username,
        FullName = member.FullName,
        Email = member.Email,
        Phone = member.Phone,
        MembershipCode = member.MembershipCode ?? string.Empty,
        IsActive = member.IsActive,
        ActiveBorrowingsCount = activeCount,
        CreatedAt = member.CreatedAt
    };
}
