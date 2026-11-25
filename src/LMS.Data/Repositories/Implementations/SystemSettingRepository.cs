using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Repositories.Implementations;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly LibraryDbContext _context;

    public SystemSettingRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public Task<SystemSetting?> GetCurrentAsync(CancellationToken ct = default)
    {
        return _context.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(SystemSetting setting, CancellationToken ct = default)
    {
        await _context.SystemSettings.AddAsync(setting, ct);
    }

    public void Update(SystemSetting setting)
    {
        _context.SystemSettings.Update(setting);
    }
}
