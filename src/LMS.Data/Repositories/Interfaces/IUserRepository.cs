using LMS.Data.Entities;
using LMS.Data.Enums;

namespace LMS.Data.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByMembershipCodeAsync(string membershipCode, CancellationToken ct = default);
    Task<List<User>> GetAllAsync(UserRole? roleFilter = null, string? search = null, CancellationToken ct = default);
    Task<int> CountByRoleAsync(UserRole role, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, int? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsByMembershipCodeAsync(string membershipCode, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
    void Remove(User user);
}
