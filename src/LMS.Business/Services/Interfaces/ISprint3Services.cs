using LMS.Business.DTOs;

namespace LMS.Business.Services.Interfaces;

public interface ISystemSettingService
{
    Task<SystemSettingsDto> GetCurrentAsync(CancellationToken ct = default);
    Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportOverviewDto> GetOverviewAsync(ReportFilterQuery query, CancellationToken ct = default);
}
