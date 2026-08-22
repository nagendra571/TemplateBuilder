using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Controllers;

public class SnippetsController : Controller
{
    private readonly ISnippetRepository _snippets;
    private readonly IAuditService _auditService;

    public SnippetsController(ISnippetRepository snippets, IAuditService auditService)
    {
        _snippets = snippets;
        _auditService = auditService;
    }

    protected string CurrentActor => User?.Identity?.Name ?? "anonymous";

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

            await _auditService.RecordAsync("Snippet", created.Id, AuditActions.SnippetCreated, CurrentActor,
                afterState: JsonSerializer.Serialize(new { name = created.Name }), ct: ct);

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
        var name = snippet.Name;
        await _snippets.DeleteAsync(id, ct);

        await _auditService.RecordAsync("Snippet", id, AuditActions.SnippetDeleted, CurrentActor,
            beforeState: JsonSerializer.Serialize(new { name }), ct: ct);

        return NoContent();
    }
}
