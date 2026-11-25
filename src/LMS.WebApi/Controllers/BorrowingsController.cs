using LMS.Business.DTOs;
using LMS.Business.Services.Interfaces;
using LMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BorrowingsController : ControllerBase
{
    private readonly IBorrowingService _borrowingService;

    public BorrowingsController(IBorrowingService borrowingService)
    {
        _borrowingService = borrowingService;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<List<BorrowingDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? memberId,
        [FromQuery] int? bookId,
        CancellationToken ct)
    {
        var items = await _borrowingService.GetAllAsync(status, memberId, bookId, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<BorrowingDto>> GetById(int id, CancellationToken ct)
    {
        var item = await _borrowingService.GetByIdAsync(id, ct);
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<BorrowingDto>> Borrow([FromBody] CreateBorrowingRequest request, CancellationToken ct)
    {
        var created = await _borrowingService.BorrowBookAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}/return")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<BorrowingDto>> Return(int id, CancellationToken ct)
    {
        var returned = await _borrowingService.ReturnBookAsync(id, ct);
        return Ok(returned);
    }

    [HttpGet("my-history")]
    [Authorize(Roles = "LibraryMember")]
    public async Task<ActionResult<List<BorrowingDto>>> MyHistory(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var history = await _borrowingService.GetMemberHistoryAsync(userId, userId, User.GetUserRole(), ct);
        return Ok(history);
    }

    [HttpGet("member/{memberId:int}")]
    [Authorize]
    public async Task<ActionResult<List<BorrowingDto>>> MemberHistory(int memberId, CancellationToken ct)
    {
        var history = await _borrowingService.GetMemberHistoryAsync(memberId, User.GetUserId(), User.GetUserRole(), ct);
        return Ok(history);
    }

    [HttpGet("overdue")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<List<BorrowingDto>>> Overdue(
        [FromQuery] string? search,
        [FromQuery] int? minDaysOverdue,
        CancellationToken ct)
    {
        var overdue = await _borrowingService.GetOverdueAsync(search, minDaysOverdue, ct);
        return Ok(overdue);
    }

    [HttpPost("sync-overdue")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<object>> SyncOverdue(CancellationToken ct)
    {
        var newlyFlagged = await _borrowingService.SyncOverdueStatusesAsync(ct);
        return Ok(new { newlyFlagged });
    }
}
