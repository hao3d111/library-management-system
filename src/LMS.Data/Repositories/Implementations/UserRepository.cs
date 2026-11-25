using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Enums;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly LibraryDbContext _context;

    public UserRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLower();
        return _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, ct);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public Task<User?> GetByMembershipCodeAsync(string membershipCode, CancellationToken ct = default)
    {
        var normalized = membershipCode.Trim().ToLower();
        return _context.Users.FirstOrDefaultAsync(
            u => u.MembershipCode != null && u.MembershipCode.ToLower() == normalized, ct);
    }

    public async Task<List<User>> GetAllAsync(UserRole? roleFilter = null, string? search = null, CancellationToken ct = default)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

        if (roleFilter.HasValue)
        {
            query = query.Where(u => u.Role == roleFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.MembershipCode != null && u.MembershipCode.ToLower().Contains(term)) ||
                (u.Phone != null && u.Phone.ToLower().Contains(term)));
        }

        return await query.OrderBy(u => u.FullName).ToListAsync(ct);
    }

    public Task<int> CountByRoleAsync(UserRole role, CancellationToken ct = default)
    {
        return _context.Users.AsNoTracking().CountAsync(u => u.Role == role, ct);
    }

    public Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLower();
        return _context.Users.AsNoTracking().AnyAsync(
            u => u.Username.ToLower() == normalized && (!excludeId.HasValue || u.Id != excludeId.Value), ct);
    }

    public Task<bool> ExistsByEmailAsync(string email, int? excludeId = null, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return _context.Users.AsNoTracking().AnyAsync(
            u => u.Email.ToLower() == normalized && (!excludeId.HasValue || u.Id != excludeId.Value), ct);
    }

    public Task<bool> ExistsByMembershipCodeAsync(string membershipCode, int? excludeId = null, CancellationToken ct = default)
    {
        var normalized = membershipCode.Trim().ToLower();
        return _context.Users.AsNoTracking().AnyAsync(
            u => u.MembershipCode != null && u.MembershipCode.ToLower() == normalized && (!excludeId.HasValue || u.Id != excludeId.Value), ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
    }
}
