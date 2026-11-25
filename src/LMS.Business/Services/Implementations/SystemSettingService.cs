using LMS.Business.DTOs;
using LMS.Business.Exceptions;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;

namespace LMS.Business.Services.Implementations;

public class SystemSettingService : ISystemSettingService
{
    private readonly ISystemSettingRepository _settingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SystemSettingService(ISystemSettingRepository settingRepository, IUnitOfWork unitOfWork)
    {
        _settingRepository = settingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SystemSettingsDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var current = await _settingRepository.GetCurrentAsync(ct);
        if (current is null)
        {
            current = new SystemSetting
            {
                MaxBorrowLimit = 5,
                LoanDurationDays = 14,
                DailyFineAmount = 1.00m,
                UpdatedAt = DateTime.UtcNow
            };
            await _settingRepository.AddAsync(current, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return MapToDto(current);
    }

    public async Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.MaxBorrowLimit < 1 || request.MaxBorrowLimit > 50)
            errors["maxBorrowLimit"] = new[] { "Max borrow limit must be between 1 and 50." };
        if (request.LoanDurationDays < 1 || request.LoanDurationDays > 365)
            errors["loanDurationDays"] = new[] { "Loan duration must be between 1 and 365 days." };
        if (request.DailyFineAmount < 0m || request.DailyFineAmount > 1000m)
            errors["dailyFineAmount"] = new[] { "Daily fine amount must be between 0.00 and 1000.00." };

        if (errors.Count > 0)
            throw new ValidationException("Invalid system settings.", errors);

        var current = await _settingRepository.GetCurrentAsync(ct);
        if (current is null)
        {
            current = new SystemSetting
            {
                MaxBorrowLimit = request.MaxBorrowLimit,
                LoanDurationDays = request.LoanDurationDays,
                DailyFineAmount = Math.Round(request.DailyFineAmount, 2, MidpointRounding.AwayFromZero),
                UpdatedAt = DateTime.UtcNow
            };
            await _settingRepository.AddAsync(current, ct);
        }
        else
        {
            current.MaxBorrowLimit = request.MaxBorrowLimit;
            current.LoanDurationDays = request.LoanDurationDays;
            current.DailyFineAmount = Math.Round(request.DailyFineAmount, 2, MidpointRounding.AwayFromZero);
            current.UpdatedAt = DateTime.UtcNow;
            _settingRepository.Update(current);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(current);
    }

    private static SystemSettingsDto MapToDto(SystemSetting s) => new()
    {
        Id = s.Id,
        MaxBorrowLimit = s.MaxBorrowLimit,
        LoanDurationDays = s.LoanDurationDays,
        DailyFineAmount = s.DailyFineAmount,
        UpdatedAt = s.UpdatedAt
    };
}
