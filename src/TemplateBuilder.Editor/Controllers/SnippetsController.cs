using Microsoft.AspNetCore.Mvc;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Controllers;

public class SnippetsController : Controller
{
    private readonly ISnippetRepository _snippets;

    public SnippetsController(ISnippetRepository snippets) => _snippets = snippets;

    [HttpGet("/Templates/Api/Snippets")]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var snippets = await _snippets.GetAllAsync(ct);
        return Json(snippets.Select(s => new { s.Id, s.Name, s.Description, s.Body }));
    }

    [HttpPost("/Templates/Api/Snippets")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateSnippetRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResult("INVALID_NAME", "Snippet name is required."));

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new ErrorResult("INVALID_BODY", "Snippet content cannot be empty."));

        var snippet = new Snippet
        {
            Name        = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Body        = request.Body
        };

        try
        {
            var created = await _snippets.CreateAsync(snippet, ct);
            return Ok(new { id = created.Id, created.Name });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new ErrorResult("DUPLICATE_NAME", $"A snippet named '{request.Name.Trim()}' already exists."));
        }
    }

    [HttpDelete("/Templates/Api/Snippets/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var snippet = await _snippets.GetByIdAsync(id, ct);
        if (snippet is null) return NotFound(new ErrorResult("NOT_FOUND", "Snippet not found."));
        await _snippets.DeleteAsync(id, ct);
        return NoContent();
    }
}
