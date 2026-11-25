using LMS.Data.Entities;

namespace LMS.Data.Repositories.Interfaces;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetCurrentAsync(CancellationToken ct = default);
    Task AddAsync(SystemSetting setting, CancellationToken ct = default);
    void Update(SystemSetting setting);
}
