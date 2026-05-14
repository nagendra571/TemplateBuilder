using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Web.Models;
using TemplateBuilder.Web.ViewModels;

namespace TemplateBuilder.Web.Controllers;

public record ErrorResult(string Code, string Message);

public class TemplatesController : Controller
{
    private const int MaxPreviewJsonBytes = 64 * 1024;
    private const int PreviewTimeoutSeconds = 5;

    private readonly ITemplateRepository _repository;
    private readonly ISqlViewDiscoveryService _viewDiscovery;
    private readonly ITemplateEngine _engine;

    public TemplatesController(ITemplateRepository repository, ISqlViewDiscoveryService viewDiscovery, ITemplateEngine engine)
    {
        _repository = repository;
        _viewDiscovery = viewDiscovery;
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? type, CancellationToken ct = default)
    {
        var templates = await _repository.GetAllAsync(ct);
        var filtered = templates.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type))
            filtered = filtered.Where(t => t.TemplateType == type);

        return View(new TemplateListViewModel
        {
            Templates = filtered.ToList(),
            Search = search,
            TypeFilter = type
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var views = await _viewDiscovery.GetViewNamesAsync(ct);
        return View("Edit", new TemplateEditorViewModel { AvailableViews = views.ToList() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TemplateEditorViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableViews = (await _viewDiscovery.GetViewNamesAsync(ct)).ToList();
            return View("Edit", model);
        }
        try
        {
            var template = await _repository.CreateAsync(new Template
            {
                Name = model.Name.Trim(),
                TemplateType = model.TemplateType,
                Description = model.Description
            }, ct);
            return RedirectToAction(nameof(Edit), new { id = template.Id });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ErrorResult("VALIDATION_ERROR", $"A template named '{model.Name.Trim()}' already exists."));
        }
    }

    [HttpGet("Templates/{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound();
        var views = await _viewDiscovery.GetViewNamesAsync(ct);
        return View(new TemplateEditorViewModel
        {
            Id = template.Id,
            Name = template.Name,
            TemplateType = template.TemplateType,
            Description = template.Description,
            Body = template.CurrentVersion?.Body ?? string.Empty,
            CurrentVersionId = template.CurrentVersionId,
            CurrentVersionNumber = template.CurrentVersion?.VersionNumber ?? 0,
            AvailableViews = views.ToList()
        });
    }

    [HttpPost("Templates/{id:int}/SaveVersion"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVersion(int id, [FromBody] SaveVersionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResult("VALIDATION_ERROR", "Template name is required."));
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        try
        {
            template.Name = request.Name.Trim();
            template.TemplateType = request.TemplateType;
            template.Description = request.Description;
            await _repository.UpdateTemplateAsync(template, ct);
            var nextNumber = await _repository.GetNextVersionNumberAsync(id, ct);
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = request.Body,
                ChangeComment = request.ChangeComment
            }, ct);
            return Ok(new { versionId = version.Id, versionNumber = version.VersionNumber });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ErrorResult("CONFLICT", "This template was modified by another user while you were editing. Please refresh and try again."));
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ErrorResult("VALIDATION_ERROR", $"A template named '{request.Name.Trim()}' already exists."));
        }
    }

    [HttpGet("Templates/{id:int}/Versions")]
    public async Task<IActionResult> GetVersionHistory(int id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound();
        var versions = await _repository.GetVersionHistoryAsync(id, ct);
        return PartialView("_VersionHistory", (versions.ToList(), template.CurrentVersionId));
    }

    [HttpPost("Templates/{id:int}/Restore/{versionId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreVersion(int id, int versionId, CancellationToken ct = default)
    {
        try
        {
            var oldBody = await _repository.GetVersionBodyAsync(versionId, ct);
            if (oldBody is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Version {versionId} not found."));
            var nextNumber = await _repository.GetNextVersionNumberAsync(id, ct);
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = oldBody,
                ChangeComment = $"Restored from v{versionId}"
            }, ct);
            return Ok(new { versionId = version.Id, versionNumber = version.VersionNumber });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ErrorResult("CONFLICT", "This template was modified by another user while you were editing. Please refresh and try again."));
        }
    }

    [HttpGet("Templates/Api/Views/{viewName}/Columns")]
    public async Task<IActionResult> GetViewColumns(string viewName, CancellationToken ct = default)
    {
        var columns = await _viewDiscovery.GetViewColumnsAsync(viewName, ct);
        return Json(columns);
    }

    [HttpPost("Templates/{id:int}/Preview"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Preview(int id, [FromBody] PreviewRequest request, CancellationToken ct = default)
    {
        if (request.ModelJson is not null &&
            System.Text.Encoding.UTF8.GetByteCount(request.ModelJson) > MaxPreviewJsonBytes)
            return BadRequest(new ErrorResult("PREVIEW_JSON_TOO_LARGE", "Preview JSON payload exceeds the 64 KB limit."));

        Dictionary<string, JsonElement>? modelDict = null;
        if (request.ModelJson is not null)
        {
            try { modelDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.ModelJson); }
            catch (JsonException ex)
            {
                return BadRequest(new ErrorResult("PREVIEW_JSON_INVALID", $"Invalid JSON: {ex.Message}"));
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(PreviewTimeoutSeconds));
        try
        {
            var model = (object?)modelDict ?? new { };
            var html = await _engine.RenderBodyAsync(request.Body, model, cts.Token);
            return Ok(new { html });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(408, new ErrorResult("PREVIEW_TIMEOUT", "Preview timed out. Simplify the template or model and try again."));
        }
        catch (TemplateRenderException)
        {
            return BadRequest(new ErrorResult("TEMPLATE_RENDER_ERROR", "Template rendering failed. Check template syntax."));
        }
    }

    [HttpPost("Templates/{id:int}/ToggleActive"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        template.IsActive = !template.IsActive;
        await _repository.UpdateTemplateAsync(template, ct);
        return Ok(new { isActive = template.IsActive });
    }

    [HttpPost("Templates/{id:int}/Duplicate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateRequest request, CancellationToken ct = default)
    {
        var source = await _repository.GetByIdAsync(id, ct);
        if (source is null) return NotFound();

        var body = source.CurrentVersion?.Body ?? string.Empty;
        try
        {
            var newTemplate = await _repository.CreateAsync(new Template
            {
                Name = request.NewName.Trim(),
                TemplateType = source.TemplateType,
                Description = source.Description
            }, ct);

            var version = await _repository.PublishVersionAsync(newTemplate.Id, new TemplateVersion
            {
                TemplateId = newTemplate.Id,
                VersionNumber = 1,
                Body = body,
                ChangeComment = $"Duplicated from '{source.Name}'"
            }, ct);

            return Ok(new { id = newTemplate.Id });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ErrorResult("VALIDATION_ERROR", $"A template named '{request.NewName.Trim()}' already exists."));
        }
    }
}
