using LMS.Business.DTOs;

namespace LMS.Business.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default);
}

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(string? role = null, string? search = null, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, int currentAdminId, CancellationToken ct = default);
    Task DeleteAsync(int id, int currentAdminId, CancellationToken ct = default);
}

public interface IMemberService
{
    Task<List<MemberDto>> GetAllAsync(string? search = null, CancellationToken ct = default);
    Task<MemberDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MemberDto> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);
    Task<MemberDto> UpdateAsync(int id, UpdateMemberRequest request, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IBookService
{
    Task<List<BookDto>> SearchAsync(BookSearchQuery query, CancellationToken ct = default);
    Task<BookDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<BookDto> CreateAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<BookDto> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
