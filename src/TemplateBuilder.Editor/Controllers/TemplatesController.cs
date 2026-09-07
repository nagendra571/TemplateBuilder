using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Models;
using TemplateBuilder.Editor;

namespace TemplateBuilder.Editor.Controllers;

public record ErrorResult(string Code, string Message);

public class TemplatesController : Controller
{
    private const int MaxPreviewJsonBytes = 64 * 1024;
    private const int PreviewTimeoutSeconds = 5;
    private const long MaxImportFileBytes = 5 * 1024 * 1024;

    private readonly ITemplateRepository _repository;
    private readonly ISqlViewDiscoveryService _viewDiscovery;
    private readonly ITemplateEngine _engine;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly ISampleDataGenerator _sampleDataGenerator;
    private readonly ITemplatePromotionService _promotion;
    private readonly ITemplateHealthService _health;
    private readonly IAuditService _auditService;
    private readonly IAuditRepository _auditRepository;
    private readonly ActorResolverAccessor _actorResolver;

    public TemplatesController(ITemplateRepository repository, ISqlViewDiscoveryService viewDiscovery, ITemplateEngine engine, IHtmlSanitizerService sanitizer, ISampleDataGenerator sampleDataGenerator, ITemplatePromotionService promotion, ITemplateHealthService health, IAuditService auditService, IAuditRepository auditRepository, ActorResolverAccessor actorResolver)
    {
        _repository = repository;
        _viewDiscovery = viewDiscovery;
        _engine = engine;
        _sanitizer = sanitizer;
        _sampleDataGenerator = sampleDataGenerator;
        _promotion = promotion;
        _health = health;
        _auditService = auditService;
        _auditRepository = auditRepository;
        _actorResolver = actorResolver;
    }

    protected string CurrentActor => ActorResolverChain.Resolve(_actorResolver.Resolver, User?.Identity?.Name, HttpContext);

    // Matches TemplateEditorViewModel.Name's [StringLength(200)] and the Template.Name column
    // width. Enforced here (not just via DataAnnotations) because these actions bind from JSON
    // and never check ModelState — without this, an over-length name reaches the DB, throws a
    // generic DbUpdateException, and gets misreported by the catch blocks below as "already
    // exists" instead of the real problem.
    private const int MaxNameLength = 200;

    private IActionResult? ValidateTemplateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ErrorResult("VALIDATION_ERROR", "Template name is required."));
        if (name.Trim().Length > MaxNameLength)
            return BadRequest(new ErrorResult("VALIDATION_ERROR", $"Template name cannot exceed {MaxNameLength} characters."));
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? type, CancellationToken ct = default)
    {
        var templates = await _repository.GetAllIncludingInactiveAsync(ct);
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

    [HttpPost, ValidateAntiForgeryToken, ActionName("Create")]
    public async Task<IActionResult> CreateTemplateJson([FromBody] TemplateEditorViewModel model, CancellationToken ct = default)
    {
        if (ValidateTemplateName(model.Name) is { } invalidName) return invalidName;
        try
        {
            var sourceView = string.IsNullOrWhiteSpace(model.SourceView) ? null : model.SourceView.Trim();
            var template = await _repository.CreateAsync(new Template
            {
                Name = model.Name.Trim(),
                TemplateType = model.TemplateType,
                Description = model.Description,
                SourceView = sourceView,
                SourceViewSnapshot = sourceView is null ? null : await _health.BuildSnapshotJsonAsync(sourceView, ct)
            }, ct);

            if (!string.IsNullOrWhiteSpace(model.Body))
            {
                await _repository.PublishVersionAsync(template.Id, new TemplateVersion
                {
                    TemplateId = template.Id,
                    VersionNumber = 1,
                    Body = model.Body,
                    Subject = model.Subject,
                    ChangeComment = "Initial version",
                    CreatedBy = CurrentActor
                }, ct);
            }

            await _auditService.RecordAsync("Template", template.Id, AuditActions.Created, CurrentActor,
                afterState: JsonSerializer.Serialize(new { name = template.Name }), ct: ct);

            return Ok(new { templateId = template.Id });
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
            Subject = template.CurrentVersion?.Subject,
            CurrentVersionId = template.CurrentVersionId,
            CurrentVersionNumber = template.CurrentVersion?.VersionNumber ?? 0,
            LatestVersionIsActive = template.CurrentVersion?.IsActive ?? true,
            AvailableViews = views.ToList(),
            SampleData = template.SampleData,
            SourceView = template.SourceView
        });
    }

    [HttpPost("Templates/{id:int}/SaveVersion"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVersion(int id, [FromBody] SaveVersionRequest request, CancellationToken ct = default)
    {
        if (ValidateTemplateName(request.Name) is { } invalidName) return invalidName;
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        try
        {
            template.Name = request.Name.Trim();
            template.TemplateType = request.TemplateType;
            template.Description = request.Description;
            var previousSourceView = template.SourceView;
            template.SourceView = string.IsNullOrWhiteSpace(request.SourceView) ? null : request.SourceView.Trim();
            if (!string.Equals(previousSourceView, template.SourceView, StringComparison.OrdinalIgnoreCase))
                template.SourceViewSnapshot = template.SourceView is null
                    ? null
                    : await _health.BuildSnapshotJsonAsync(template.SourceView, ct);
            await _repository.UpdateTemplateAsync(template, ct);
            var nextNumber = await _repository.GetNextVersionNumberAsync(id, ct);
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = request.Body,
                Subject = request.Subject,
                ChangeComment = request.ChangeComment,
                IsActive = request.IsActive ?? true,
                CreatedBy = CurrentActor
            }, ct);

            await _auditService.RecordAsync("Template", id, version.IsActive ? AuditActions.Published : AuditActions.DraftSaved,
                CurrentActor, afterState: JsonSerializer.Serialize(new { versionNumber = version.VersionNumber, versionId = version.Id, isActive = version.IsActive }), ct: ct);

            return Ok(new { versionId = version.Id, versionNumber = version.VersionNumber, isActive = version.IsActive });
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

    [HttpGet("Templates/{id:int}/Versions/{versionId:int}/Body")]
    public async Task<IActionResult> GetVersionBody(int id, int versionId, CancellationToken ct = default)
    {
        var version = await _repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
        return Ok(new { subject = version.Subject, body = version.Body });
    }

    [HttpGet("Templates/{id:int}/Audit")]
    public async Task<IActionResult> GetAuditTimeline(int id, CancellationToken ct = default)
    {
        var rows = await _auditRepository.QueryAsync(new AuditQuery { EntityType = "Template", EntityId = id, PageSize = 100 }, ct);
        return Ok(rows.Select(a => new { id = a.Id, action = a.Action, actor = a.Actor, occurredAt = a.OccurredAt.ToString("o"), comment = a.Comment }));
    }

    [HttpPost("Templates/{id:int}/Restore/{versionId:int}/{sourceVersionNumber:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreVersion(int id, int versionId, int sourceVersionNumber, CancellationToken ct = default)
    {
        try
        {
            var source = await _repository.GetVersionAsync(versionId, ct);
            if (source is null) return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
            var nextNumber = await _repository.GetNextVersionNumberAsync(id, ct);
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = source.Body,
                Subject = source.Subject,
                ChangeComment = $"Restored from v{sourceVersionNumber}",
                IsActive = source.IsActive,
                CreatedBy = CurrentActor
            }, ct);

            await _auditService.RecordAsync("Template", id, AuditActions.Restored, CurrentActor,
                comment: $"Restored from v{sourceVersionNumber}", ct: ct);

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

    [HttpPost("Templates/{id:int}/Preview"), ValidateAntiForgeryToken]
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
            var subject = string.IsNullOrEmpty(request.Subject)
                ? string.Empty
                : await _engine.RenderBodyAsync(request.Subject, model, cts.Token);
            var html = _sanitizer.Sanitize(await _engine.RenderBodyAsync(request.Body, model, cts.Token));
            return Ok(new { subject, html });
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

        await _auditService.RecordAsync("Template", id, AuditActions.ToggledActive, CurrentActor,
            afterState: JsonSerializer.Serialize(new { isActive = template.IsActive }), ct: ct);

        return Ok(new { isActive = template.IsActive });
    }

    [HttpPost("Templates/{id:int}/Validate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validate(int id, [FromBody] ValidateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Body))
            return BadRequest(new { message = "Body is required." });

        if (System.Text.Encoding.UTF8.GetByteCount(request.Body) > 64 * 1024)
            return BadRequest(new { message = "Body exceeds size limit." });

        try
        {
            await _engine.RenderBodyAsync(request.Body, new { }, ct);
            return Ok(new { valid = true });
        }
        catch (Exception ex)
        {
            return Ok(new { valid = false, message = ex.Message });
        }
    }

    [HttpPost("Templates/Api/SampleData/Generate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSampleData([FromBody] GenerateSampleDataRequest request, CancellationToken ct = default)
    {
        var data = await _sampleDataGenerator.GenerateAsync(request?.ViewName, request?.TemplateBody, ct);
        return Ok(new { sampleData = data });
    }

    [HttpPut("Templates/{id:int}/SampleData"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSampleData(int id, [FromBody] SaveSampleDataRequest request, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        template.SampleData = string.IsNullOrWhiteSpace(request?.SampleData) ? null : request.SampleData;
        await _repository.UpdateTemplateAsync(template, ct);
        return Ok(new { saved = true });
    }

    [HttpPost("Templates/{id:int}/Duplicate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateRequest request, CancellationToken ct = default)
    {
        if (ValidateTemplateName(request.NewName) is { } invalidName) return invalidName;
        var source = await _repository.GetByIdAsync(id, ct);
        if (source is null) return NotFound();

        var body = source.CurrentVersion?.Body ?? string.Empty;
        var subject = source.CurrentVersion?.Subject;
        var isActive = source.CurrentVersion?.IsActive ?? true;
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
                Subject = subject,
                ChangeComment = $"Duplicated from '{source.Name}'",
                IsActive = isActive,
                CreatedBy = CurrentActor
            }, ct);

            await _auditService.RecordAsync("Template", newTemplate.Id, AuditActions.Duplicated, CurrentActor,
                comment: $"Duplicated from template {id}", ct: ct);

            return Ok(new { id = newTemplate.Id });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ErrorResult("VALIDATION_ERROR", $"A template named '{request.NewName.Trim()}' already exists."));
        }
    }

    [HttpGet("Templates/Export/{id:int}")]
    public async Task<IActionResult> ExportTemplate(int id, CancellationToken ct = default)
    {
        var doc = await _promotion.BuildExportAsync(id, ct);
        if (doc is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        var bytes = Encoding.UTF8.GetBytes(_promotion.SerializeExport(doc));
        var fileName = $"{_promotion.SanitizeFileName(doc.Template.Name)}.template.json";
        return File(bytes, "application/json", fileName);
    }

    [HttpPost("Templates/Import"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Import([FromForm] IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResult("NO_FILE", "No file selected."));
        if (file.Length > MaxImportFileBytes)
            return BadRequest(new ErrorResult("FILE_TOO_LARGE", $"Import file exceeds the {MaxImportFileBytes / (1024 * 1024)} MB limit."));
        if (!string.Equals(Path.GetExtension(file.FileName ?? string.Empty), ".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResult("INVALID_FILE_TYPE", "Only .template.json export files can be imported."));
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await _promotion.ImportAsync(ms.ToArray(), CurrentActor, ct);

        var entry = result.Created.Count == 1 ? result.Created[0]
            : result.Updated.Count == 1 ? result.Updated[0]
            : null;
        if (entry is not null)
        {
            await _auditService.RecordAsync("Template", entry.Id, AuditActions.Imported, CurrentActor,
                afterState: JsonSerializer.Serialize(new { file = file.FileName, externalKey = entry.ExternalKey, versionsImported = entry.VersionsAppended }), ct: ct);
        }

        return Ok(result);
    }

    [HttpPost("Templates/BulkActivate"), ValidateAntiForgeryToken]
    public Task<IActionResult> BulkActivate([FromBody] BulkIdsRequest request, CancellationToken ct = default)
        => BulkToggle(request.Ids, active: true, ct);

    [HttpPost("Templates/BulkDeactivate"), ValidateAntiForgeryToken]
    public Task<IActionResult> BulkDeactivate([FromBody] BulkIdsRequest request, CancellationToken ct = default)
        => BulkToggle(request.Ids, active: false, ct);

    private async Task<IActionResult> BulkToggle(IReadOnlyList<int> ids, bool active, CancellationToken ct)
    {
        var succeeded = new List<int>();
        var failed = new List<object>();
        foreach (var id in ids)
        {
            try
            {
                var t = await _repository.GetByIdAsync(id, ct);
                if (t is null) { failed.Add(new { id, reason = "NOT_FOUND" }); continue; }
                if (t.IsActive == active) { succeeded.Add(id); continue; }
                t.IsActive = active;
                await _repository.UpdateTemplateAsync(t, ct);
                succeeded.Add(id);
            }
            catch (Exception) { failed.Add(new { id, reason = "ERROR" }); }
        }
        return Ok(new { succeeded, failed });
    }

    [HttpPost("Templates/BulkExport"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkExport([FromBody] BulkIdsRequest request, CancellationToken ct = default)
    {
        var zip = await _promotion.BuildBulkZipAsync(request.Ids, ct);
        return File(zip, "application/zip", "template-builder-export.zip");
    }

    [HttpPost("Templates/BulkDelete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete([FromBody] BulkIdsRequest request, CancellationToken ct = default)
    {
        var actor = CurrentActor; // resolve/cache once; let a throwing resolver propagate, not get swallowed by the per-item audit-failure catch below
        var succeeded = new List<int>();
        var failed = new List<object>();
        foreach (var id in request.Ids)
        {
            try
            {
                var t = await _repository.GetByIdAsync(id, ct);
                if (t is null) { failed.Add(new { id, reason = "NOT_FOUND" }); continue; }
                var name = t.Name;
                if (await _repository.DeleteAsync(id, ct))
                {
                    succeeded.Add(id);
                    try
                    {
                        await _auditService.RecordAsync("Template", id, AuditActions.Deleted, actor,
                            beforeState: JsonSerializer.Serialize(new { name }), ct: ct);
                    }
                    catch (Exception) { /* delete already succeeded; an audit-write failure must not re-route it into failed */ }
                }
                else failed.Add(new { id, reason = "NOT_FOUND" });
            }
            catch (Exception) { failed.Add(new { id, reason = "ERROR" }); }
        }
        return Ok(new { succeeded, failed });
    }

    [HttpGet("Templates/{id:int}/Health")]
    public async Task<IActionResult> GetHealth(int id, CancellationToken ct = default)
    {
        try
        {
            var report = await _health.CheckAsync(id, ct);
            return Ok(report);
        }
        catch (TemplateNotFoundException)
        {
            return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        }
    }
}
