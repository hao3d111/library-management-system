using LMS.Business.DTOs;
using LMS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISystemSettingService _settingService;

    public SettingsController(ISystemSettingService settingService)
    {
        _settingService = settingService;
    }

    [HttpGet]
    public async Task<ActionResult<SystemSettingsDto>> GetCurrent(CancellationToken ct)
    {
        var current = await _settingService.GetCurrentAsync(ct);
        return Ok(current);
    }

    [HttpPut]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SystemSettingsDto>> Update([FromBody] UpdateSystemSettingsRequest request, CancellationToken ct)
    {
        var updated = await _settingService.UpdateAsync(request, ct);
        return Ok(updated);
    }
}
