using LMS.Business.DTOs;
using LMS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator,Librarian")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var members = await _memberService.GetAllAsync(search, ct);
        return Ok(members);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MemberDto>> GetById(int id, CancellationToken ct)
    {
        var member = await _memberService.GetByIdAsync(id, ct);
        return Ok(member);
    }

    [HttpPost]
    public async Task<ActionResult<MemberDto>> Create([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        var created = await _memberService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MemberDto>> Update(int id, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        var updated = await _memberService.UpdateAsync(id, request, ct);
        return Ok(updated);
    }
}
