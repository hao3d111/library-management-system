using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Repositories.Implementations;

public class CategoryRepository : ICategoryRepository
{
    private readonly LibraryDbContext _context;

    public CategoryRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.Categories.Include(c => c.Books).FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public Task<List<Category>> GetAllWithBookCountsAsync(CancellationToken ct = default)
    {
        return _context.Categories
            .AsNoTracking()
            .Include(c => c.Books)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return _context.Categories.AsNoTracking().AnyAsync(
            c => c.Name.ToLower() == normalized && (!excludeId.HasValue || c.Id != excludeId.Value), ct);
    }

    public Task<bool> HasBooksAsync(int categoryId, CancellationToken ct = default)
    {
        return _context.Books.AsNoTracking().AnyAsync(b => b.CategoryId == categoryId, ct);
    }

    public async Task AddAsync(Category category, CancellationToken ct = default)
    {
        await _context.Categories.AddAsync(category, ct);
    }

    public void Update(Category category)
    {
        _context.Categories.Update(category);
    }

    public void Remove(Category category)
    {
        _context.Categories.Remove(category);
    }
}
