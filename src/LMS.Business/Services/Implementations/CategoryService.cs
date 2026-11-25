using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;

namespace LMS.Business.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _categoryRepository.GetAllWithBookCountsAsync(ct);
        return categories.Select(MapToDto).ToList();
    }

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category with ID {id} was not found.");
        return MapToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Category name is required.", new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Category name is required." }
            });
        }

        var name = request.Name.Trim();
        if (await _categoryRepository.ExistsByNameAsync(name, ct: ct))
        {
            throw new ConflictException($"Category '{name}' already exists.");
        }

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim()
        };

        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category with ID {id} was not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Category name is required.", new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Category name is required." }
            });
        }

        var name = request.Name.Trim();
        if (await _categoryRepository.ExistsByNameAsync(name, excludeId: id, ct: ct))
        {
            throw new ConflictException($"Category '{name}' already exists.");
        }

        category.Name = name;
        category.Description = request.Description?.Trim();

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(category);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category with ID {id} was not found.");

        if (await _categoryRepository.HasBooksAsync(id, ct))
        {
            throw new ConflictException("Cannot delete a category that still has books assigned to it.");
        }

        _categoryRepository.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static CategoryDto MapToDto(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        BookCount = category.Books?.Count ?? 0
    };
}
