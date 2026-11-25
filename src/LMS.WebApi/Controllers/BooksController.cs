using LMS.Business.DTOs;
using LMS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? title,
        [FromQuery] string? author,
        [FromQuery] string? isbn,
        [FromQuery] int? categoryId,
        CancellationToken ct)
    {
        var books = await _bookService.SearchAsync(new BookSearchQuery
        {
            Q = q,
            Title = title,
            Author = author,
            ISBN = isbn,
            CategoryId = categoryId
        }, ct);
        return Ok(books);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookDto>> GetById(int id, CancellationToken ct)
    {
        var book = await _bookService.GetByIdAsync(id, ct);
        return Ok(book);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<BookDto>> Create([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var created = await _bookService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<ActionResult<BookDto>> Update(int id, [FromBody] UpdateBookRequest request, CancellationToken ct)
    {
        var updated = await _bookService.UpdateAsync(id, request, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrator,Librarian")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _bookService.DeleteAsync(id, ct);
        return NoContent();
    }
}
