using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Services;

public class TemplatePromotionService : ITemplatePromotionService
{
    private static readonly JsonSerializerOptions CamelJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly Regex InvalidFileNameChars = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    private readonly ITemplateRepository _templateRepository;
    private readonly ITemplatePromotionRepository _promotionRepository;

    public TemplatePromotionService(ITemplateRepository templateRepository, ITemplatePromotionRepository promotionRepository)
    {
        _templateRepository = templateRepository;
        _promotionRepository = promotionRepository;
    }

    public async Task<TemplateExportDocument?> BuildExportAsync(int templateId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct);
        if (template is null) return null;

        var versions = await _templateRepository.GetVersionHistoryAsync(templateId, ct);
        var ordered = versions.OrderBy(v => v.VersionNumber).ToList();

        return new TemplateExportDocument
        {
            SchemaVersion = 3,
            Exporter = new ExporterInfo(),
            ExportedAt = DateTime.UtcNow,
            Template = new TemplateExportTemplate
            {
                ExternalKey = template.ExternalKey,
                Name = template.Name,
                TemplateType = template.TemplateType,
                Description = template.Description,
                SampleData = template.SampleData,
                IsActive = template.IsActive,
                Versions = ordered.Select(v => new TemplateExportVersion
                {
                    VersionNumber = v.VersionNumber,
                    Body = v.Body,
                    Subject = v.Subject,
                    ChangeComment = v.ChangeComment,
                    CreatedAt = v.CreatedAt,
                    CreatedBy = v.CreatedBy,
                    IsActive = v.IsActive
                }).ToList()
            }
        };
    }

    public string SerializeExport(TemplateExportDocument document) =>
        JsonSerializer.Serialize(document, CamelJson);

    public string SanitizeFileName(string name) => InvalidFileNameChars.Replace(name, "_");

    public async Task<TemplateImportResult> ImportAsync(byte[] fileBytes, string actor, CancellationToken ct = default)
    {
        var result = new TemplateImportResult();

        TemplateExportDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<TemplateExportDocument>(Encoding.UTF8.GetString(fileBytes), CamelJson);
        }
        catch (JsonException ex)
        {
            result.Errors.Add(new TemplateImportEntry { Reason = $"Invalid import file: {ex.Message}" });
            return result;
        }

        if (doc is null)
        {
            result.Errors.Add(new TemplateImportEntry { Reason = "Import file is empty or invalid." });
            return result;
        }

        if (doc.SchemaVersion != 3)
        {
            result.Errors.Add(new TemplateImportEntry
            {
                Name = doc.Template?.Name,
                ExternalKey = doc.Template?.ExternalKey ?? Guid.Empty,
                Reason = $"Unsupported schemaVersion {doc.SchemaVersion}; expected 3."
            });
            return result;
        }

        if (doc.Template is null
            || string.IsNullOrWhiteSpace(doc.Template.Name)
            || string.IsNullOrWhiteSpace(doc.Template.TemplateType)
            || doc.Template.Versions is not { Count: > 0 })
        {
            result.Errors.Add(new TemplateImportEntry { Reason = "Import file is missing a template name, type, or versions." });
            return result;
        }

        var templateDto = doc.Template;

        foreach (var version in templateDto.Versions)
        {
            var parsed = Scriban.Template.Parse(version.Body);
            if (parsed.HasErrors)
            {
                result.Errors.Add(new TemplateImportEntry
                {
                    Name = templateDto.Name,
                    ExternalKey = templateDto.ExternalKey,
                    Reason = $"Version {version.VersionNumber}: {string.Join("; ", parsed.Messages.Select(m => m.Message))}"
                });
                return result;
            }
        }

        var existing = await _promotionRepository.GetByExternalKeyAsync(templateDto.ExternalKey, ct);

        if (existing is null)
        {
            var template = new Template
            {
                ExternalKey = templateDto.ExternalKey,
                Name = templateDto.Name,
                TemplateType = templateDto.TemplateType,
                Description = templateDto.Description,
                SampleData = templateDto.SampleData,
                IsActive = templateDto.IsActive
            };

            var versions = templateDto.Versions.Select(v => new TemplateVersion
            {
                VersionNumber = v.VersionNumber,
                Body = v.Body,
                Subject = v.Subject,
                ChangeComment = v.ChangeComment,
                CreatedAt = v.CreatedAt,
                CreatedBy = v.CreatedBy ?? actor,
                IsActive = v.IsActive
            }).ToList();

            var created = await _promotionRepository.AddWithVersionsAsync(template, versions, ct);
            result.Created.Add(new TemplateImportEntry
            {
                Id = created.Id,
                Name = created.Name,
                ExternalKey = created.ExternalKey,
                VersionsAppended = versions.Count
            });
        }
        else
        {
            existing.Name = templateDto.Name;
            existing.TemplateType = templateDto.TemplateType;
            existing.Description = templateDto.Description;
            existing.SampleData = templateDto.SampleData;
            existing.IsActive = templateDto.IsActive;

            var versions = templateDto.Versions.Select(v => new TemplateVersion
            {
                Body = v.Body,
                Subject = v.Subject,
                ChangeComment = v.ChangeComment,
                CreatedAt = v.CreatedAt,
                CreatedBy = v.CreatedBy ?? actor,
                IsActive = v.IsActive
            }).ToList();

            var assigned = await _promotionRepository.UpdateFromImportAsync(existing, versions, ct);
            result.Updated.Add(new TemplateImportEntry
            {
                Id = existing.Id,
                Name = existing.Name,
                ExternalKey = existing.ExternalKey,
                VersionsAppended = assigned.Count
            });
        }

        return result;
    }

    public async Task<byte[]> BuildBulkZipAsync(IReadOnlyList<int> templateIds, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        var summaryFiles = new List<object>();

        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var id in templateIds)
            {
                var doc = await BuildExportAsync(id, ct);
                if (doc is null)
                {
                    summaryFiles.Add(new { id, name = (string?)null, status = "not found" });
                    continue;
                }

                var fileName = $"{SanitizeFileName(doc.Template.Name)}.template.json";
                var entry = archive.CreateEntry(fileName);
                await using (var entryStream = entry.Open())
                await using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    await writer.WriteAsync(SerializeExport(doc));
                }

                summaryFiles.Add(new { id, name = doc.Template.Name, status = "exported" });
            }

            var summary = new
            {
                schemaVersion = 3,
                exportedAt = DateTime.UtcNow,
                files = summaryFiles
            };

            var summaryEntry = archive.CreateEntry("_summary.json");
            await using (var entryStream = summaryEntry.Open())
            await using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(summary, CamelJson));
            }
        }

        return ms.ToArray();
    }
}
