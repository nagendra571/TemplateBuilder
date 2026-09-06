# Email Subject Field Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional Subject field to Email-type templates, versioned with Body, rendered through the same Scriban pipeline, insertable via the existing field palette, and carried through Preview, Restore, Duplicate, Compare, and Template Promotion export/import.

**Architecture:** `Subject` is a new nullable column on `TemplateVersions` (not `Templates`), mirroring how `Body` is already versioned. It flows through the exact same code paths `Body` already uses at every layer — EF Core entity/config/migration, `TemplateRepository`, `TemplateEngine`, controller actions, promotion export/import — rather than introducing a parallel mechanism. The editor UI reuses the existing field palette, tracking which of (body, subject) last had focus to decide where an inserted `{{ model.X }}` token goes.

**Tech Stack:** ASP.NET Core (net8.0/net10.0), EF Core (SQL Server), Scriban, xunit + FluentAssertions + Moq, vanilla JS (no framework) for the editor.

**Spec:** This plan adapts `handoff/templatebuilder-editor-mvc5/docs/superpowers/specs/2026-09-02-email-subject-field-design.md` — the design spec written for and approved in the sibling `TemplateBuilder.Editor.Mvc5` (.NET Framework 4.8 / EF6) fork. Read it for the full "why" (storage decision, render-API shape, promotion schema-version policy). **This repo's mechanics differ substantially** — see Deviations below; do not follow the sibling repo's plan file mechanically, only its decisions.

## Deviations from the sibling repo's plan (read before starting)

- **`ITemplateRepository.GetVersionAsync(int versionId)` already exists** in this repo (`src/TemplateBuilder.Infrastructure/Repositories/TemplateRepository.cs:44`, already used by `RestoreVersion`). The sibling plan's Task 1/2 (add this method) is not needed here — Task 1 below only adds `Subject` to the entity and `RenderEmailAsync`/`RenderedEmail` to `ITemplateEngine`.
- **EF Core migrations are native** (`dotnet ef migrations add`) — no scaffolding console app, no `migrate.exe`, no `.Designer.cs`/`.resx` triad. One command, verified in-session (see Task 2).
- **The DBA-managed-database schema script already exists** in this repo (`src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql`, added for the DBA-managed-databases backport) with its own golden-file test (`SchemaScriptGenerationTests`, net10.0-only, in `tests/TemplateBuilder.Editor.Tests/`). The new migration in this plan **must** regenerate that script or the golden-file gate fails — folded into Task 2.
- **No RazorGenerator / Unity / packages.config** — this is a plain Razor Class Library consumed via `AddTemplateBuilderEditor()`; views are ordinary `.cshtml`, DI is `Microsoft.Extensions.DependencyInjection`.
- **Build/test tooling is `dotnet build` / `dotnet test`** throughout — no `MSBuild.exe`/`vstest.console.exe` paths.
- **`TemplatePromotionService`'s constructor takes `(ITemplateRepository, ITemplatePromotionRepository)` only** — no `IAuditService`. Audit recording for import happens in the controller (`TemplatesController.Import`), not the service. Do not add an audit parameter.
- **This repo has controller-level unit tests for `TemplatesController`** (`tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs`, 885 lines, extensive Moq-based coverage) — unlike the sibling repo, which had none and deferred all controller verification to a live E2E pass. Task 6 below adds/updates real unit tests instead of just "build and eyeball it live."
- **Two bugs the sibling repo's plan missed and had to fix in a later code-review pass** (commit `d96d276`, "Duplicate drops Subject, missing dirty-tracking") are folded into this plan from the start instead of being deferred to a review cycle:
  1. `Duplicate` must carry `Subject` forward from the source template's current version (the sibling plan's Task 7 never touched `Duplicate` at all).
  2. The `prop-subject` input must be included in both the dirty-tracking array **and** wired to `refreshUsedMarks` (typing a field token into Subject must mark the field palette's "used" checkmark, same as typing into the body).
- **This repo's `PreviewRequest` and `SaveVersionRequest` are C# `record`s consumed positionally by existing tests** (e.g. `new PreviewRequest("body", bigJson)`, `new SaveVersionRequest("A", "Email", null, "<p>x</p>", null)`). `Subject` is added as the **last** parameter with a `null` default on both records, specifically so no existing positional call site breaks.
- **Promotion schema version is currently `2`** in this repo too (not yet bumped past the sibling repo's old baseline) — this plan bumps it `2 → 3`, matching the sibling repo's final state and its "old files are rejected, not silently upgraded" precedent.
- **No live E2E via IIS Express/Docker SQL Server** — this repo runs directly via `dotnet run` against a local SQL Server instance already reachable at `localhost\SQLEXPRESS` (used earlier this session to verify UI changes). Task 10 uses that, plus a browser check if the browser-automation tooling cooperates (it did not reach `localhost` in an earlier attempt this session — fall back to `curl`/manual review if so, and say so rather than claiming an unverified visual check).

## Global Constraints

- `Subject` max length: 500 characters (matches `Description`/`ChangeComment` precedent in `TemplateVersionConfiguration`/`TemplateEditorViewModel`).
- `Subject` is nullable everywhere (optional field).
- No `TemplateType` validation inside `TemplateEngine.RenderEmailAsync` — it renders whatever `Subject` is stored, empty string if none, regardless of type. Type-gating (show/hide the input) is UI-only.
- Subject is never passed through `IHtmlSanitizerService` — it's plain text, not HTML.
- Every build/test claim in this plan must be verified by actually running the command and reading its output (per this session's own established convention — see `docs/status.md`'s verification notes on prior items).
- `dotnet build TemplateBuilder.slnx` / `dotnet test TemplateBuilder.slnx` for full-solution checks; scope to a single project with `--project`/direct `.csproj` path for faster per-task iteration.
- Kill any running `dotnet run` dev server before building/testing — a live process locks `TemplateBuilder.Editor.dll` and fails the build with `MSB3027`/`MSB3021` (hit and resolved earlier this session).

---

## Task 1: Domain — Subject on TemplateVersion, RenderEmailAsync on ITemplateEngine

**Files:**
- Modify: `src/TemplateBuilder.Domain/Entities/TemplateVersion.cs`
- Modify: `src/TemplateBuilder.Domain/Interfaces/ITemplateEngine.cs`

**Interfaces:**
- Produces: `TemplateVersion.Subject` (`string?`), `ITemplateEngine.RenderEmailAsync(int templateId, object model, CancellationToken ct = default) : Task<RenderedEmail>`, `RenderedEmail { string Subject, string Body }`.
- Consumes: nothing new — `ITemplateRepository.GetVersionAsync` already exists.

- [ ] **Step 1: Add `Subject` to the entity**

Edit `src/TemplateBuilder.Domain/Entities/TemplateVersion.cs` — add the property directly after `Body`:
```csharp
public class TemplateVersion
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? ChangeComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Template Template { get; set; } = null!;
}
```

- [ ] **Step 2: Add `RenderEmailAsync` + `RenderedEmail` to `ITemplateEngine`**

Edit `src/TemplateBuilder.Domain/Interfaces/ITemplateEngine.cs` — full file:
```csharp
namespace TemplateBuilder.Domain.Interfaces;

public interface ITemplateEngine
{
    Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);
    Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);
    Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default);
    Task<RenderedEmail> RenderEmailAsync(int templateId, object model, CancellationToken ct = default);
}

public class RenderedEmail
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Build Domain to confirm it compiles**

```bash
dotnet build src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj --nologo -v minimal
```
Expected: builds clean. `TemplateBuilder.Infrastructure`/`TemplateBuilder.Application` will NOT build yet — `TemplateRepository`/`TemplateEngine`/`TemplatePromotionService` don't reference the new `Subject` property or implement `RenderEmailAsync` yet. That's expected; those land in Tasks 2–4.

- [ ] **Step 4: Commit**
```bash
git add src/TemplateBuilder.Domain/Entities/TemplateVersion.cs src/TemplateBuilder.Domain/Interfaces/ITemplateEngine.cs
git commit -m "feat: add Subject to TemplateVersion and ITemplateEngine.RenderEmailAsync"
```

---

## Task 2: Infrastructure — EF Core mapping, migration, and DBA schema-script regeneration

**Files:**
- Modify: `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateVersionConfiguration.cs`
- Create: `src/TemplateBuilder.Infrastructure/Migrations/<timestamp>_AddSubjectToTemplateVersions.cs` (+ `.Designer.cs`, generated)
- Modify: `src/TemplateBuilder.Infrastructure/Migrations/TemplateBuilderDbContextModelSnapshot.cs` (generated)
- Test: `tests/TemplateBuilder.Infrastructure.Tests/Repositories/TemplateRepositoryTests.cs`
- Modify: `src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql` (regenerated content, same filename — no version bump in this plan)

**Interfaces:**
- Consumes: `TemplateVersion.Subject` (Task 1).
- Produces: `Subject` persists through EF Core; `TemplateRepository.GetVersionAsync`/`GetVersionBodyAsync` return it automatically (no repository code change needed — both are direct `TemplateVersion`/projection queries against the DbContext, and `Subject` flowing through requires only the entity + mapping change).

- [ ] **Step 1: Map `Subject` in `TemplateVersionConfiguration`**

Edit `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateVersionConfiguration.cs`:
```csharp
public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();
        builder.Property(v => v.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(v => v.Subject).HasMaxLength(500);
        builder.Property(v => v.ChangeComment).HasMaxLength(500);
        builder.Property(v => v.CreatedAt).HasColumnType("datetime2");
        builder.Property(v => v.CreatedBy).HasMaxLength(100);
    }
}
```

- [ ] **Step 2: Generate the migration**

```bash
dotnet ef migrations add AddSubjectToTemplateVersions --project src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --startup-project src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --context TemplateBuilderDbContext
```
Expected: creates `<timestamp>_AddSubjectToTemplateVersions.cs` + `.Designer.cs` under `src/TemplateBuilder.Infrastructure/Migrations/`, updates `TemplateBuilderDbContextModelSnapshot.cs`. Open the new `.cs` file and confirm `Up()` contains:
```csharp
migrationBuilder.AddColumn<string>(
    name: "Subject",
    table: "TemplateVersions",
    type: "nvarchar(500)",
    maxLength: 500,
    nullable: true);
```
and `Down()` contains `migrationBuilder.DropColumn(name: "Subject", table: "TemplateVersions");`. If `Up()` is empty, Step 1's mapping didn't land — fix that first, then delete the empty migration (`dotnet ef migrations remove ...`) and regenerate.

- [ ] **Step 3: Build Infrastructure to confirm the migration compiles**
```bash
dotnet build src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --nologo -v minimal
```
Expected: builds clean.

- [ ] **Step 4: Add a repository test confirming Subject round-trips through EF Core**

Edit `tests/TemplateBuilder.Infrastructure.Tests/Repositories/TemplateRepositoryTests.cs`, add right after the existing `GetVersionAsync_ReturnsSingleVersion` test:
```csharp
    [Fact]
    public async Task GetVersionAsync_ReturnsSubject()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "Subject Test", TemplateType = "Email" });
        var v = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>hi</p>",
            Subject = "Welcome, {{ model.Name }}!"
        });

        var result = await repo.GetVersionAsync(v.Id);

        result!.Subject.Should().Be("Welcome, {{ model.Name }}!");
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsNullSubject_WhenNotSet()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "No Subject Test", TemplateType = "Report" });
        var v = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>report</p>"
        });

        var result = await repo.GetVersionAsync(v.Id);

        result!.Subject.Should().BeNull();
    }
```

- [ ] **Step 5: Run the Infrastructure.Tests suite**
```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj --nologo -v minimal --filter "FullyQualifiedName~GetVersionAsync"
```
Expected: both new tests pass (they run against the EF Core InMemory provider — no live SQL Server needed).

Then the full suite for regressions:
```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj --nologo -v minimal
```
Expected: all pass.

- [ ] **Step 6: Regenerate the DBA-managed-database schema script (golden-file gate)**

The new migration changes the migration chain, so `src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql` — committed for the DBA-managed-databases feature (see `docs/status.md` item 6) — is now stale, and its golden-file test (`SchemaScriptGenerationTests`, net10.0-only) will fail. Regenerate it in place (same filename — no version bump in this plan):
```bash
TB_REGEN_SCHEMA=1 dotnet test tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj --nologo -v minimal -f net10.0 --filter "FullyQualifiedName~SchemaScriptGenerationTests"
```
Expected: `Passed! ... Total: 1` (the regeneration branch always "passes" — it writes the file and returns). Then confirm the gate itself is green on a normal run:
```bash
dotnet test tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj --nologo -v minimal -f net10.0 --filter "FullyQualifiedName~SchemaScriptGenerationTests"
```
Expected: passes (no `TB_REGEN_SCHEMA` this time — real comparison). Then inspect the regenerated script for the new column:
```bash
grep -n "Subject" src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql
```
Expected: at least one `ADD [Subject] [nvarchar](500) NULL` line and a new `__EFMigrationsHistory` insert row for `AddSubjectToTemplateVersions`.

- [ ] **Step 7: Commit**
```bash
git add src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateVersionConfiguration.cs src/TemplateBuilder.Infrastructure/Migrations/ tests/TemplateBuilder.Infrastructure.Tests/Repositories/TemplateRepositoryTests.cs src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql
git commit -m "feat: map TemplateVersion.Subject in EF Core, add migration, regenerate DBA schema script"
```

---

## Task 3: Application — TemplateEngine.RenderEmailAsync

**Files:**
- Modify: `src/TemplateBuilder.Application/Services/TemplateEngine.cs`
- Test: `tests/TemplateBuilder.Application.Tests/Services/TemplateEngineTests.cs`

**Interfaces:**
- Consumes: `ITemplateRepository.GetLastActiveVersionAsync` (existing), `TemplateVersion.Subject` (Task 1), `RenderedEmail` (Task 1), the existing private `GetBodyAsync(templateId, versionId, ct)` cache helper (reused for `Body`, same as `RenderAsync`).
- Produces: working `TemplateEngine.RenderEmailAsync`.

- [ ] **Step 1: Write the failing tests**

Edit `tests/TemplateBuilder.Application.Tests/Services/TemplateEngineTests.cs`, add near the other `RenderAsync_*` tests (reuse the existing private `CreateEngine(repo.Object)` helper at the top of the class — do not redefine it):
```csharp
    [Fact]
    public async Task RenderEmailAsync_RendersSubjectAndBody()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.Template { Id = 11, Name = "Invoice", IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion
            {
                Id = 20,
                VersionNumber = 1,
                Subject = "Invoice {{ model.InvoiceNumber }} is ready",
                Body = "<p>Hi {{ model.CustomerName }}</p>"
            });
        repo.Setup(r => r.GetVersionBodyAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Hi {{ model.CustomerName }}</p>");
        var engine = CreateEngine(repo.Object);

        var result = await engine.RenderEmailAsync(11, new { InvoiceNumber = "INV-42", CustomerName = "Bob" });

        result.Subject.Should().Be("Invoice INV-42 is ready");
        result.Body.Should().Be("<p>Hi Bob</p>");
    }

    [Fact]
    public async Task RenderEmailAsync_ReturnsEmptySubject_WhenNoneSet()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.Template { Id = 12, Name = "Report", IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 21, VersionNumber = 1, Body = "<p>no subject</p>" });
        repo.Setup(r => r.GetVersionBodyAsync(21, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>no subject</p>");
        var engine = CreateEngine(repo.Object);

        var result = await engine.RenderEmailAsync(12, new { });

        result.Subject.Should().BeEmpty();
        result.Body.Should().Be("<p>no subject</p>");
    }
```

- [ ] **Step 2: Run the tests to confirm they fail to compile**
```bash
dotnet build tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj --nologo -v minimal
```
Expected: `CS1061` — `'ITemplateEngine' does not contain a definition for 'RenderEmailAsync'` (confirms the test exists, implementation doesn't yet).

- [ ] **Step 3: Implement `RenderEmailAsync`**

In `src/TemplateBuilder.Application/Services/TemplateEngine.cs`, add right after `RenderByNameAsync` (before `RenderBodyAsync`):
```csharp
    public async Task<RenderedEmail> RenderEmailAsync(int templateId, object model, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(templateId, ct);
        if (template is null)
            throw new TemplateNotFoundException(templateId);
        if (!template.IsActive)
            throw new TemplateInactiveException(templateId);

        var activeVersion = await _repository.GetLastActiveVersionAsync(templateId, ct)
            ?? throw new NoActiveVersionException(templateId);

        var body = await GetBodyAsync(templateId, activeVersion.Id, ct);
        return new RenderedEmail
        {
            Subject = await RenderBodyAsync(activeVersion.Subject ?? string.Empty, model, ct),
            Body = await RenderBodyAsync(body, model, ct)
        };
    }
```
(Reuses the existing `GetBodyAsync` cache helper for `Body` — same as `RenderAsync` — so `RenderEmailAsync` benefits from the same version-tracked cache. `Subject` isn't cached separately; it's already in hand from `activeVersion`, and subject lines are short, so a second cache lookup would add nothing.)

- [ ] **Step 4: Build and run — verify the new tests pass**
```bash
dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj --nologo -v minimal --filter "FullyQualifiedName~RenderEmailAsync"
```
Expected: both PASS.

- [ ] **Step 5: Run the full Application.Tests suite for regressions**
```bash
dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj --nologo -v minimal
```
Expected: all pass except `TemplatePromotionServiceTests`/`TemplatePromotionImportTests` schema-version assertions, which are expected to still be green here (Task 4 changes their expected value from 2 to 3 — until then they still assert 2, which still matches the unchanged `TemplatePromotionService`).

- [ ] **Step 6: Commit**
```bash
git add src/TemplateBuilder.Application/Services/TemplateEngine.cs tests/TemplateBuilder.Application.Tests/Services/TemplateEngineTests.cs
git commit -m "feat: implement ITemplateEngine.RenderEmailAsync"
```

---

## Task 4: Application — Template Promotion export/import Subject + schema v2 → v3

**Files:**
- Modify: `src/TemplateBuilder.Application/Services/ITemplatePromotionService.cs`
- Modify: `src/TemplateBuilder.Application/Services/TemplatePromotionService.cs`
- Modify: `tests/TemplateBuilder.Application.Tests/Services/TemplatePromotionServiceTests.cs`
- Modify: `tests/TemplateBuilder.Application.Tests/Services/TemplatePromotionImportTests.cs`

**Interfaces:**
- Consumes: `TemplateVersion.Subject` (Task 1).
- Produces: `TemplateExportVersion.Subject`, `TemplateExportDocument.SchemaVersion` default `3`.

- [ ] **Step 1: Add `Subject` to the export DTO**

In `src/TemplateBuilder.Application/Services/ITemplatePromotionService.cs`, edit `TemplateExportVersion` — add `Subject` right after `Body`:
```csharp
public class TemplateExportVersion
{
    public int VersionNumber { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? ChangeComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; }
}
```

- [ ] **Step 2: Wire `Subject` through export and both import paths, bump the version gate**

In `src/TemplateBuilder.Application/Services/TemplatePromotionService.cs`:

`BuildExportAsync` (around line 39) — change `SchemaVersion = 2,` to `SchemaVersion = 3,` and add `Subject = v.Subject,` to the version projection:
```csharp
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
```

`ImportAsync`'s version gate (around line 89) — change both the comparison and the message:
```csharp
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
```

`ImportAsync`'s create-new-template path (around line 140) — add `Subject = v.Subject,`:
```csharp
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
```

`ImportAsync`'s update-existing-template path (around line 167) — add `Subject = v.Subject,`:
```csharp
            var versions = templateDto.Versions.Select(v => new TemplateVersion
            {
                Body = v.Body,
                Subject = v.Subject,
                ChangeComment = v.ChangeComment,
                CreatedAt = v.CreatedAt,
                CreatedBy = v.CreatedBy ?? actor,
                IsActive = v.IsActive
            }).ToList();
```

`BuildBulkZipAsync`'s manifest literal (around line 218) — change `schemaVersion = 2,` to `schemaVersion = 3,`.

- [ ] **Step 3: Update the existing test that hardcodes schema version 2**

In `tests/TemplateBuilder.Application.Tests/Services/TemplatePromotionServiceTests.cs`, change:
```csharp
        doc!.SchemaVersion.Should().Be(2);
```
to:
```csharp
        doc!.SchemaVersion.Should().Be(3);
```

- [ ] **Step 4: Add a Subject round-trip test to `TemplatePromotionServiceTests.cs`**

Add in the same class:
```csharp
    [Fact]
    public async Task BuildExportAsync_IncludesSubjectPerVersion()
    {
        var repo = new Mock<ITemplateRepository>();
        var promo = new Mock<ITemplatePromotionRepository>();
        repo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 8, Name = "Welcome", TemplateType = "Email", IsActive = true
        });
        repo.Setup(r => r.GetVersionHistoryAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TemplateVersion>
        {
            new() { VersionNumber = 1, Body = "<p>hi</p>", Subject = "Welcome aboard", IsActive = true }
        });
        var svc = new TemplatePromotionService(repo.Object, promo.Object);

        var doc = await svc.BuildExportAsync(8);

        doc!.Template.Versions.Single().Subject.Should().Be("Welcome aboard");
    }
```

- [ ] **Step 5: Update the existing v1-rejection test's expectation and add a Subject import round-trip test to `TemplatePromotionImportTests.cs`**

The existing `Import_RejectsNonSchema2File` test already asserts against `{"schemaVersion":1,...}`, which stays rejected against `!= 3` unchanged — no edit needed there. Add a new test in the same class (reuse the existing `Create(repo, promo)` helper):
```csharp
    [Fact]
    public async Task Import_RoundTripsSubject()
    {
        var promo = new Mock<ITemplatePromotionRepository>();
        var key = Guid.NewGuid();
        promo.Setup(p => p.GetByExternalKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        IReadOnlyList<TemplateVersion>? captured = null;
        promo.Setup(p => p.AddWithVersionsAsync(It.IsAny<Template>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, IReadOnlyList<TemplateVersion> vs, CancellationToken _) => { captured = vs; return t; });
        var svc = Create(promo: promo);
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = key, Name = "X", TemplateType = "Email", IsActive = true,
                Versions = { new() { VersionNumber = 1, Body = "<p>ok</p>", Subject = "Hi there", IsActive = true } }
            }
        };

        await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");

        captured!.Single().Subject.Should().Be("Hi there");
    }
```

- [ ] **Step 6: Build and run the full Application.Tests suite**
```bash
dotnet build tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj --nologo -v minimal
dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj --nologo -v minimal
```
Expected: every test passes — first point in this plan where the whole `TemplateBuilder.Application.Tests` suite is green again.

- [ ] **Step 7: Commit**
```bash
git add src/TemplateBuilder.Application/Services/ITemplatePromotionService.cs src/TemplateBuilder.Application/Services/TemplatePromotionService.cs tests/TemplateBuilder.Application.Tests/Services/TemplatePromotionServiceTests.cs tests/TemplateBuilder.Application.Tests/Services/TemplatePromotionImportTests.cs
git commit -m "feat: carry Subject through Template Promotion export/import, bump schema to v3"
```

---

## Task 5: Editor models — Subject on the request/view DTOs

**Files:**
- Modify: `src/TemplateBuilder.Editor/Models/TemplateEditorViewModel.cs`
- Modify: `src/TemplateBuilder.Editor/Models/SaveVersionRequest.cs`
- Modify: `src/TemplateBuilder.Editor/Models/PreviewRequest.cs`

**Interfaces:**
- Produces: `TemplateEditorViewModel.Subject`, `SaveVersionRequest.Subject`, `PreviewRequest.Subject` — all `string?`, consumed by Task 6's controller changes.
- **`Subject` is appended as the LAST parameter (with a `null` default) on both records** — several existing tests in `TemplatesControllerTests.cs` construct these positionally (e.g. `new SaveVersionRequest("A", "Email", null, "<p>x</p>", null)`, `new PreviewRequest("body", bigJson)`); inserting `Subject` anywhere earlier in the parameter list would silently shift those calls' argument bindings and break/mis-test them without a compile error in some cases.

- [ ] **Step 1: Add `Subject` to `TemplateEditorViewModel`**

In `src/TemplateBuilder.Editor/Models/TemplateEditorViewModel.cs`, add after `Body`:
```csharp
    public string? Body { get; set; }
    [StringLength(500)]
    public string? Subject { get; set; }
```

- [ ] **Step 2: Add `Subject` to `SaveVersionRequest`**

Full file — `src/TemplateBuilder.Editor/Models/SaveVersionRequest.cs`:
```csharp
namespace TemplateBuilder.Editor.Models;

public record SaveVersionRequest(
    string Name,
    string TemplateType,
    string? Description,
    string Body,
    string? ChangeComment,
    bool? IsActive = null,
    string? SourceView = null,
    string? Subject = null);
```

- [ ] **Step 3: Add `Subject` to `PreviewRequest`**

Full file — `src/TemplateBuilder.Editor/Models/PreviewRequest.cs`:
```csharp
namespace TemplateBuilder.Editor.Models;

public record PreviewRequest(string Body, string? ModelJson, string? Subject = null);

public record ValidateRequest(string Body);
```

- [ ] **Step 4: Build to confirm it compiles (nothing consumes these fields yet — that's Task 6)**
```bash
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj --nologo -v minimal
```
Expected: builds clean for both net8.0 and net10.0.

- [ ] **Step 5: Build the existing controller test suite to confirm no positional-argument breakage**
```bash
dotnet build tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj --nologo -v minimal
```
Expected: builds clean — confirms appending `Subject` last didn't shift any existing positional test call.

- [ ] **Step 6: Commit**
```bash
git add src/TemplateBuilder.Editor/Models/TemplateEditorViewModel.cs src/TemplateBuilder.Editor/Models/SaveVersionRequest.cs src/TemplateBuilder.Editor/Models/PreviewRequest.cs
git commit -m "feat: add Subject to the editor's request/view DTOs"
```

---

## Task 6: Editor controller — wire Subject through every action (including Duplicate)

**Files:**
- Modify: `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs`
- Modify: `tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs`

**Interfaces:**
- Consumes: `TemplateEditorViewModel.Subject`, `SaveVersionRequest.Subject`, `PreviewRequest.Subject` (Task 5), `ITemplateRepository.GetVersionAsync` (already exists).
- Produces: `Subject` flows through `CreateTemplateJson`, `Edit` GET, `SaveVersion`, `Preview` (response now `{ subject, html }`), `GetVersionBody` (switched from `GetVersionBodyAsync` to `GetVersionAsync`; response now `{ subject, body }`), `RestoreVersion`, and `Duplicate` (the gap the sibling repo's own plan missed).

- [ ] **Step 1: `CreateTemplateJson` — publish the initial version with Subject**

Find (around line 90):
```csharp
                await _repository.PublishVersionAsync(template.Id, new TemplateVersion
                {
                    TemplateId = template.Id,
                    VersionNumber = 1,
                    Body = model.Body,
                    ChangeComment = "Initial version",
                    CreatedBy = CurrentActor
                }, ct);
```
Add `Subject = model.Subject,` after `Body = model.Body,`.

- [ ] **Step 2: `Edit` GET — surface Subject to the view**

Find (around line 117):
```csharp
        return View(new TemplateEditorViewModel
        {
            Id = template.Id,
            Name = template.Name,
            TemplateType = template.TemplateType,
            Description = template.Description,
            Body = template.CurrentVersion?.Body ?? string.Empty,
```
Add `Subject = template.CurrentVersion?.Subject,` after that `Body = ...` line.

- [ ] **Step 3: `SaveVersion` — publish Subject with the new version**

Find (around line 153):
```csharp
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = request.Body,
                ChangeComment = request.ChangeComment,
                IsActive = request.IsActive ?? true,
                CreatedBy = CurrentActor
            }, ct);
```
Add `Subject = request.Subject,` after `Body = request.Body,`.

- [ ] **Step 4: `Preview` — render and return Subject, without sanitizing it**

Find (around line 260):
```csharp
            var model = (object?)modelDict ?? new { };
            var html = _sanitizer.Sanitize(await _engine.RenderBodyAsync(request.Body, model, cts.Token));
            return Ok(new { html });
```
Replace with:
```csharp
            var model = (object?)modelDict ?? new { };
            var subject = string.IsNullOrEmpty(request.Subject)
                ? string.Empty
                : await _engine.RenderBodyAsync(request.Subject, model, cts.Token);
            var html = _sanitizer.Sanitize(await _engine.RenderBodyAsync(request.Body, model, cts.Token));
            return Ok(new { subject, html });
```

- [ ] **Step 5: `GetVersionBody` — return Subject alongside Body, switching to `GetVersionAsync`**

Find (around line 187):
```csharp
    [HttpGet("Templates/{id:int}/Versions/{versionId:int}/Body")]
    public async Task<IActionResult> GetVersionBody(int id, int versionId, CancellationToken ct = default)
    {
        var body = await _repository.GetVersionBodyAsync(versionId, ct);
        if (body is null)
            return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
        return Ok(new { body });
    }
```
Replace with:
```csharp
    [HttpGet("Templates/{id:int}/Versions/{versionId:int}/Body")]
    public async Task<IActionResult> GetVersionBody(int id, int versionId, CancellationToken ct = default)
    {
        var version = await _repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
        return Ok(new { subject = version.Subject, body = version.Body });
    }
```
(The JS side consuming this response — the Compare view — is updated in Task 8; this is a response-shape change, not additive, so both sides need updating together.)

- [ ] **Step 6: `RestoreVersion` — Subject already carries forward automatically**

Find the existing block (around line 208):
```csharp
            var source = await _repository.GetVersionAsync(versionId, ct);
            if (source is null) return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
            var nextNumber = await _repository.GetNextVersionNumberAsync(id, ct);
            var version = await _repository.PublishVersionAsync(id, new TemplateVersion
            {
                TemplateId = id,
                VersionNumber = nextNumber,
                Body = source.Body,
                ChangeComment = $"Restored from v{sourceVersionNumber}",
                IsActive = source.IsActive,
                CreatedBy = CurrentActor
            }, ct);
```
Add `Subject = source.Subject,` after `Body = source.Body,`. (This action already used `GetVersionAsync`, unlike the sibling repo which had to switch from a body-only fetch — so `source` here is already the full entity; only the one field needs adding to the published version.)

- [ ] **Step 7: `Duplicate` — carry Subject forward too (the gap the sibling repo's own plan missed on its first pass)**

Find (around line 332):
```csharp
        var body = source.CurrentVersion?.Body ?? string.Empty;
        var isActive = source.CurrentVersion?.IsActive ?? true;
```
Replace with:
```csharp
        var body = source.CurrentVersion?.Body ?? string.Empty;
        var subject = source.CurrentVersion?.Subject;
        var isActive = source.CurrentVersion?.IsActive ?? true;
```
Then find:
```csharp
            var version = await _repository.PublishVersionAsync(newTemplate.Id, new TemplateVersion
            {
                TemplateId = newTemplate.Id,
                VersionNumber = 1,
                Body = body,
                ChangeComment = $"Duplicated from '{source.Name}'",
                IsActive = isActive,
                CreatedBy = CurrentActor
            }, ct);
```
Add `Subject = subject,` after `Body = body,`.

- [ ] **Step 8: Build**
```bash
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj --nologo -v minimal
```
Expected: builds clean.

- [ ] **Step 9: Update the `GetVersionBody` test for the new response shape and mock target**

In `tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs`, replace:
```csharp
    public async Task GetVersionBody_ExistingVersion_ReturnsBodyJson()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetVersionBodyAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Hello {{ model.Name }}</p>");
        var controller = CreateController(mockRepo.Object);

        var result = await controller.GetVersionBody(1, 42);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { body = "<p>Hello {{ model.Name }}</p>" });
    }
```
with:
```csharp
    public async Task GetVersionBody_ExistingVersion_ReturnsSubjectAndBodyJson()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetVersionAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion { Id = 42, Subject = "Hi {{ model.Name }}", Body = "<p>Hello {{ model.Name }}</p>" });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.GetVersionBody(1, 42);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { subject = "Hi {{ model.Name }}", body = "<p>Hello {{ model.Name }}</p>" });
    }
```
And replace the mock target in the not-found sibling test:
```csharp
    public async Task GetVersionBody_NonExistentVersion_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetVersionAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateVersion?)null);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.GetVersionBody(1, 99);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
```

- [ ] **Step 10: Add new tests for Subject wiring — CreateTemplateJson, SaveVersion, RestoreVersion, Duplicate, Preview**

Add in the same class:
```csharp
    [Fact]
    public async Task CreateTemplateJson_PublishesSubject()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 5; return t; });
        var controller = CreateController(mockRepo.Object);

        await controller.CreateTemplateJson(new TemplateEditorViewModel
        {
            Name = "Welcome Email",
            TemplateType = "Email",
            Body = "<p>Hi</p>",
            Subject = "Welcome, {{ model.Name }}!"
        });

        mockRepo.Verify(r => r.PublishVersionAsync(5,
            It.Is<TemplateVersion>(v => v.Subject == "Welcome, {{ model.Name }}!"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveVersion_PublishesSubject()
    {
        var repo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email" };
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var controller = CreateController(repo.Object);

        await controller.SaveVersion(1, new SaveVersionRequest("A", "Email", null, "<p>x</p>", null, Subject: "New subject"));

        captured!.Subject.Should().Be("New subject");
    }

    [Fact]
    public async Task RestoreVersion_CarriesSubjectForward()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetVersionAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion { Id = 5, VersionNumber = 1, Body = "<p>old</p>", Subject = "Old subject", IsActive = true });
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        var controller = CreateController(repo.Object);

        await controller.RestoreVersion(1, 5, 1);

        captured!.Subject.Should().Be("Old subject");
    }

    [Fact]
    public async Task Duplicate_CarriesSubjectForward()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        var source = new Template
        {
            Id = 1, Name = "Invoice Email", TemplateType = "Email",
            CurrentVersion = new TemplateVersion { Id = 10, Body = "<p>Hello</p>", Subject = "Your invoice", VersionNumber = 1 },
            CurrentVersionId = 10
        };
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 99; return t; });
        TemplateVersion? captured = null;
        mockRepo.Setup(r => r.PublishVersionAsync(99, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        var controller = CreateController(mockRepo.Object);

        await controller.Duplicate(1, new DuplicateRequest("Copy of Invoice Email"));

        captured!.Subject.Should().Be("Your invoice");
    }

    [Fact]
    public async Task Preview_RendersSubjectSeparatelyFromBody_WithoutSanitizing()
    {
        var mockEngine = new Mock<ITemplateEngine>();
        mockEngine.Setup(e => e.RenderBodyAsync("Hi {{ model.Name }}", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi Bob");
        mockEngine.Setup(e => e.RenderBodyAsync("<p>body</p>", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>body</p>");
        var mockSanitizer = new Mock<IHtmlSanitizerService>();
        mockSanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns((string s) => s);
        var controller = CreateController(engine: mockEngine.Object, sanitizer: mockSanitizer.Object);

        var result = await controller.Preview(1, new PreviewRequest("<p>body</p>", "{\"Name\":\"Bob\"}", "Hi {{ model.Name }}"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { subject = "Hi Bob", html = "<p>body</p>" });
        mockSanitizer.Verify(s => s.Sanitize("Hi Bob"), Times.Never);
    }
```

- [ ] **Step 11: Run the full Editor.Tests suite**
```bash
dotnet test tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj --nologo -v minimal
```
Expected: all pass for both net8.0 and net10.0.

- [ ] **Step 12: Commit**
```bash
git add src/TemplateBuilder.Editor/Controllers/TemplatesController.cs tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs
git commit -m "feat: wire Subject through Create, Edit, SaveVersion, Preview, GetVersionBody, Restore, Duplicate"
```

---

## Task 7: Editor view — Subject input, Preview modal, Compare modal

**Files:**
- Modify: `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml`

**Interfaces:**
- Consumes: `Model.Subject`, `Model.TemplateType` (existing).
- Produces: `#prop-subject-row` wrapper (for show/hide) + `#prop-subject` input, `#preview-subject-row`/`#preview-subject-text`, `#compare-subject-current`/`#compare-subject-old` — all consumed by Task 8's JS.

- [ ] **Step 1: Add the Subject input to the PROPERTIES panel**

Find (around line 91):
```html
                <div>
                    <label for="prop-name">Template Name</label>
                    <input asp-for="Name" id="prop-name" />
                    <span asp-validation-for="Name" class="tb-field-error"></span>
                </div>
```
Add directly after it:
```html
                <div id="prop-subject-row" style="@(Model.TemplateType == "Email" ? "" : "display:none;")">
                    <label for="prop-subject">Subject</label>
                    <input asp-for="Subject" id="prop-subject" maxlength="500"
                           placeholder="e.g. Your order {{ model.OrderNumber }} has shipped" />
                </div>
```

- [ ] **Step 2: Add the Subject line to the Preview modal**

Find (around line 269):
```html
            <button class="btn btn-primary btn-sm" id="btn-render">Render</button>
            <div id="preview-error" class="tb-error-msg" role="alert" style="display:none;"></div>
            <div class="tb-preview-frame-wrap" id="preview-frame-wrap" style="display:none;">
```
Replace with:
```html
            <button class="btn btn-primary btn-sm" id="btn-render">Render</button>
            <div id="preview-error" class="tb-error-msg" role="alert" style="display:none;"></div>
            <div id="preview-subject-row" class="tb-preview-subject" style="display:none;">
                <strong>Subject:</strong> <span id="preview-subject-text"></span>
            </div>
            <div class="tb-preview-frame-wrap" id="preview-frame-wrap" style="display:none;">
```

- [ ] **Step 3: Add Subject lines to both Compare modal panels**

Find (around line 203):
```html
            <div class="compare-panel">
                <div class="compare-panel-header">
                    <span id="compare-current-num" class="tb-version-num"></span>
                    <span class="tb-version-badge">Current</span>
                    <span class="compare-panel-meta"></span>
                </div>
                <div class="compare-iframe-wrap">
```
Replace with:
```html
            <div class="compare-panel">
                <div class="compare-panel-header">
                    <span id="compare-current-num" class="tb-version-num"></span>
                    <span class="tb-version-badge">Current</span>
                    <span class="compare-panel-meta"></span>
                </div>
                <div id="compare-subject-current" class="tb-compare-subject" style="display:none;"></div>
                <div class="compare-iframe-wrap">
```
And find (around line 218):
```html
            <div class="compare-panel">
                <div class="compare-panel-header">
                    <span id="compare-old-num" class="tb-version-num"></span>
                    <span id="compare-old-meta" class="compare-panel-meta"></span>
                </div>
                <div class="compare-iframe-wrap">
```
Replace with:
```html
            <div class="compare-panel">
                <div class="compare-panel-header">
                    <span id="compare-old-num" class="tb-version-num"></span>
                    <span id="compare-old-meta" class="compare-panel-meta"></span>
                </div>
                <div id="compare-subject-old" class="tb-compare-subject" style="display:none;"></div>
                <div class="compare-iframe-wrap">
```

- [ ] **Step 4: Build**
```bash
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj --nologo -v minimal
```
Expected: builds clean. (Full behavior verification happens once Task 8's JS exists — an unwired `#prop-subject` input isn't independently testable yet.)

- [ ] **Step 5: Commit**
```bash
git add src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml
git commit -m "feat: add Subject input and preview/compare subject rows to Edit.cshtml"
```

---

## Task 8: Editor JS — focus tracking, insertion, scans, payloads, modal rendering, dirty-tracking

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`

**Interfaces:**
- Consumes: `#prop-subject`, `#prop-subject-row`, `#preview-subject-row`/`#preview-subject-text`, `#compare-subject-current`/`#compare-subject-old` (Task 7); server response shapes `{ subject, html }` from Preview and `{ subject, body }` from GetVersionBody (Task 6).
- Produces: `_lastFocusInsertTarget` module-level variable, subject-aware field insertion, subject-aware used-field/sample-data scanning, subject included in `createTemplate`/`saveVersion` payloads, subject shown in Preview/Compare, subject field toggled by TemplateType, subject included in dirty-tracking and used-field refresh (closing both gaps the sibling repo's plan missed on its first pass).

- [ ] **Step 1: Add focus tracking**

Near the top of the file, right after the existing `function markDirty() { _isDirty = true; }` line (the "Unsaved-change tracking" section), add:
```javascript
// ── Field-insertion target tracking ─────────────────────────────────────────
// The field palette's Insert button can target either the body editor or the
// Subject input — track whichever was focused last so Insert knows where to
// put the token.
let _lastFocusInsertTarget = 'body';
document.getElementById('prop-subject')?.addEventListener('focus', () => { _lastFocusInsertTarget = 'subject'; });
document.addEventListener('focusin', (e) => {
    if (e.target.closest?.('.sun-editor-editable')) _lastFocusInsertTarget = 'body';
});
```
(Using `focusin` with `.closest('.sun-editor-editable')` rather than a single direct listener because the SunEditor iframe/editable area isn't guaranteed to exist at page-load-script-run time — it's created asynchronously by `SUNEDITOR.create()` later in this same file. `focusin` bubbles and is attached once at the document level, so it works regardless of when that element appears.)

- [ ] **Step 2: Make the palette Insert button subject-aware**

Find (around line 628):
```javascript
document.getElementById('field-palette')?.addEventListener('click', (e) => {
    const btn = e.target.closest('.palette-insert-btn');
    if (!btn || !_editor) return;
    e.stopPropagation();
    _editor.insertHTML(
        `<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(btn.dataset.field)} }}</span>&nbsp;`
    );
    document.querySelector('.sun-editor-editable')?.focus();
    markDirty();
});
```
Replace with:
```javascript
document.getElementById('field-palette')?.addEventListener('click', (e) => {
    const btn = e.target.closest('.palette-insert-btn');
    if (!btn) return;
    e.stopPropagation();
    const field = btn.dataset.field;
    if (_lastFocusInsertTarget === 'subject') {
        const input = document.getElementById('prop-subject');
        if (!input) return;
        const token = `{{ model.${field} }}`;
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? input.value.length;
        input.value = input.value.slice(0, start) + token + input.value.slice(end);
        input.selectionStart = input.selectionEnd = start + token.length;
        input.focus();
        markDirty();
        refreshUsedMarks();
        return;
    }
    if (!_editor) return;
    _editor.insertHTML(
        `<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(field)} }}</span>&nbsp;`
    );
    document.querySelector('.sun-editor-editable')?.focus();
    markDirty();
});
```
(Drag-and-drop into the editor stays body-only, unchanged — dragging a palette field onto a plain text input isn't part of this feature's scope; the Insert button is the only mechanism made subject-aware. The explicit `refreshUsedMarks()` call in the subject branch matters because the body branch's `onChange` handler already calls it via SunEditor's own change event — the subject `<input>` has no equivalent hook here, only the `input`/`change` listeners wired in Step 6 below, which is a separate, later concern from "insert via button click".)

- [ ] **Step 3: Include Subject in used-field tracking and sample-data auto-generation**

Find `refreshUsedMarks` (around line 1649):
```javascript
let _usedMarkTimer = null;
function refreshUsedMarks() {
    clearTimeout(_usedMarkTimer);
    _usedMarkTimer = setTimeout(() => {
        if (!_currentColumns.length) return;
        const used = _tbUsedFields(_editor ? _editor.getContents() : '');
```
Replace the last line with:
```javascript
        const subjectValue = document.getElementById('prop-subject')?.value ?? '';
        const used = _tbUsedFields((_editor ? _editor.getContents() : '') + ' ' + subjectValue);
```

Find `generateSampleData` (around line 529):
```javascript
async function generateSampleData(mode) {
    const ta = document.getElementById('preview-json');
    if (!ta) return;
    const viewName = document.getElementById('view-selector')?.value || null;
    const body = _editor ? _editor.getContents() : null;
```
Replace the last line with:
```javascript
    const subjectValue = document.getElementById('prop-subject')?.value ?? '';
    const body = _editor ? _editor.getContents() + ' ' + subjectValue : null;
```
(Both reuse the existing `_tbGenerateSampleFromHtml`/`_tbUsedFields` regex scans as-is — they already just look for `{{ model.X }}` patterns in whatever string they're given, so concatenating subject text in is enough; no change needed inside those two functions themselves.)

Also find `loadViewColumns`'s used-fields computation (around line 462), used when the field palette is first populated:
```javascript
        const used = _tbUsedFields(_editor ? _editor.getContents() : '');
```
Replace with:
```javascript
        const subjectValue = document.getElementById('prop-subject')?.value ?? '';
        const used = _tbUsedFields((_editor ? _editor.getContents() : '') + ' ' + subjectValue);
```
(Without this, a freshly loaded palette would show a field as unused even if it only appears in an already-saved Subject — only the periodic `refreshUsedMarks` timer would eventually correct it.)

- [ ] **Step 4: Include Subject in the `createTemplate` and `saveVersion` payloads**

Find (in `createTemplate`, around line 660):
```javascript
            body: JSON.stringify({
                name: document.getElementById('prop-name').value,
                templateType: document.getElementById('prop-type').value,
                description: document.getElementById('prop-desc').value,
                body
            })
```
Replace with:
```javascript
            body: JSON.stringify({
                name: document.getElementById('prop-name').value,
                templateType: document.getElementById('prop-type').value,
                description: document.getElementById('prop-desc').value,
                subject: document.getElementById('prop-subject')?.value ?? null,
                body
            })
```

Find (in `saveVersion`, around line 705):
```javascript
            body: JSON.stringify({
                name: document.getElementById('prop-name').value,
                templateType: document.getElementById('prop-type').value,
                description: document.getElementById('prop-desc').value,
                body,
                changeComment: document.getElementById('save-comment').value,
                isActive,
                sourceView: document.getElementById('prop-source-view')?.value || null,
            })
```
Replace with:
```javascript
            body: JSON.stringify({
                name: document.getElementById('prop-name').value,
                templateType: document.getElementById('prop-type').value,
                description: document.getElementById('prop-desc').value,
                body,
                subject: document.getElementById('prop-subject')?.value ?? null,
                changeComment: document.getElementById('save-comment').value,
                isActive,
                sourceView: document.getElementById('prop-source-view')?.value || null,
            })
```

- [ ] **Step 5: Show/hide the Subject field on TemplateType change**

Find the existing dirty-tracking wiring block (around line 1810):
```javascript
['prop-name', 'prop-type', 'prop-desc', 'save-comment'].forEach(id => {
    const el = document.getElementById(id);
    if (!el) return;
    el.addEventListener('input', markDirty);
    el.addEventListener('change', markDirty);
});
```
Replace with (adds `prop-subject` to dirty-tracking — this is the first of the two gaps the sibling repo's own plan missed on its first pass):
```javascript
['prop-name', 'prop-type', 'prop-desc', 'prop-subject', 'save-comment'].forEach(id => {
    const el = document.getElementById(id);
    if (!el) return;
    el.addEventListener('input', markDirty);
    el.addEventListener('change', markDirty);
});

// Typing a field token into Subject must refresh the palette's "used" checkmarks the
// same way typing into the body does (wired via SunEditor's own onChange elsewhere) —
// the second of the two gaps the sibling repo's plan missed on its first pass.
document.getElementById('prop-subject')?.addEventListener('input', refreshUsedMarks);

document.getElementById('prop-type')?.addEventListener('change', (e) => {
    const row = document.getElementById('prop-subject-row');
    if (row) row.style.display = e.target.value === 'Email' ? '' : 'none';
});
```

- [ ] **Step 6: Send Subject in the Preview request, render the response's Subject**

Find `renderPreview` (around line 917):
```javascript
    const body = _editor.getContents();
    const modelJson = document.getElementById('preview-json').value;
    try {
        const res = await fetch(`/Templates/${templateId ?? 0}/Preview`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _csrf
            },
            body: JSON.stringify({ body, modelJson })
        });
        if (res.ok) {
            const { html } = await res.json();
            document.getElementById('preview-frame').srcdoc = html;
            frameWrap.style.display = 'block';
```
Replace with:
```javascript
    const body = _editor.getContents();
    const subject = document.getElementById('prop-subject')?.value ?? '';
    const modelJson = document.getElementById('preview-json').value;
    try {
        const res = await fetch(`/Templates/${templateId ?? 0}/Preview`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _csrf
            },
            body: JSON.stringify({ body, subject, modelJson })
        });
        if (res.ok) {
            const { subject: renderedSubject, html } = await res.json();
            const subjectRow = document.getElementById('preview-subject-row');
            const subjectText = document.getElementById('preview-subject-text');
            const isEmail = document.getElementById('prop-type')?.value === 'Email';
            if (subjectRow && subjectText) {
                if (isEmail && renderedSubject) {
                    subjectText.textContent = renderedSubject;
                    subjectRow.style.display = '';
                } else {
                    subjectRow.style.display = 'none';
                }
            }
            document.getElementById('preview-frame').srcdoc = html;
            frameWrap.style.display = 'block';
```

- [ ] **Step 7: Render Subject in the Compare panels**

Find `_renderComparePanel` (around line 834):
```javascript
async function _renderComparePanel(side, body, versionId) {
    const loadingEl = document.getElementById(`compare-loading-${side}`);
    const iframeEl  = document.getElementById(`compare-iframe-${side}`);
    try {
        if (body === null) {
            const res = await fetch(`/Templates/${templateId}/Versions/${versionId}/Body`);
            if (!res.ok) { loadingEl.textContent = 'Failed to load version.'; return; }
            body = (await res.json()).body;
        }
        if (body == null) { loadingEl.textContent = 'Version body unavailable.'; return; }
        const modelJson = _tbGenerateSampleFromHtml(body);
        const previewRes = await fetch(`/Templates/${templateId}/Preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': _csrf },
            body: JSON.stringify({ body, modelJson })
        });
        if (!previewRes.ok) {
            const err = await previewRes.json().catch(() => null);
            loadingEl.textContent = `Preview failed: ${err?.message ?? previewRes.status}`;
            return;
        }
        iframeEl.srcdoc = (await previewRes.json()).html;
        loadingEl.style.display = 'none';
    } catch {
        loadingEl.textContent = 'Network error loading preview.';
    }
}
```
Replace with:
```javascript
async function _renderComparePanel(side, body, versionId) {
    const loadingEl = document.getElementById(`compare-loading-${side}`);
    const iframeEl  = document.getElementById(`compare-iframe-${side}`);
    const subjectEl = document.getElementById(`compare-subject-${side}`);
    try {
        let subject = side === 'current' ? (document.getElementById('prop-subject')?.value ?? '') : null;
        if (body === null) {
            const res = await fetch(`/Templates/${templateId}/Versions/${versionId}/Body`);
            if (!res.ok) { loadingEl.textContent = 'Failed to load version.'; return; }
            const versionData = await res.json();
            body = versionData.body;
            subject = versionData.subject;
        }
        if (body == null) { loadingEl.textContent = 'Version body unavailable.'; return; }
        const modelJson = _tbGenerateSampleFromHtml(body + ' ' + (subject ?? ''));
        const previewRes = await fetch(`/Templates/${templateId}/Preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': _csrf },
            body: JSON.stringify({ body, subject, modelJson })
        });
        if (!previewRes.ok) {
            const err = await previewRes.json().catch(() => null);
            loadingEl.textContent = `Preview failed: ${err?.message ?? previewRes.status}`;
            return;
        }
        const previewData = await previewRes.json();
        if (subjectEl) {
            const isEmail = document.getElementById('prop-type')?.value === 'Email';
            if (isEmail && previewData.subject) {
                subjectEl.textContent = `Subject: ${previewData.subject}`;
                subjectEl.style.display = '';
            } else {
                subjectEl.style.display = 'none';
            }
        }
        iframeEl.srcdoc = previewData.html;
        loadingEl.style.display = 'none';
    } catch {
        loadingEl.textContent = 'Network error loading preview.';
    }
}
```

- [ ] **Step 8: Syntax-check and build**
```bash
node --check src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj --nologo -v minimal
```
Expected: both clean (this is a static JS asset served via `wwwroot`, not embedded/compiled — `node --check` is the real syntax gate here; the `dotnet build` just confirms nothing else broke).

- [ ] **Step 9: Commit**
```bash
git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
git commit -m "feat: wire Subject through field insertion, previews, compare, and dirty-tracking in the editor JS"
```

---

## Task 9: Editor CSS — subject row styling

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css`

**Interfaces:**
- Consumes: `.tb-preview-subject`, `.tb-compare-subject` (Task 7's markup).

- [ ] **Step 1: Add styling for the new preview/compare subject rows**

Find the `.tb-preview-frame-wrap` rule (around line 671):
```css
/* Preview modal */
#tb-editor-host .tb-preview-frame-wrap {
    margin-top: 12px;
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    overflow: hidden;
}
```
Add directly before it:
```css
#tb-editor-host .tb-preview-subject {
    font-size: 13px;
    color: var(--text);
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 8px 10px;
    margin: 8px 0;
    word-break: break-word;
}

/* Preview modal */
#tb-editor-host .tb-preview-frame-wrap {
    margin-top: 12px;
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    overflow: hidden;
}
```

Find the `.compare-panel-meta` rule (around line 1335):
```css
#tb-editor-host .compare-panel-meta {
    font-size: .8em;
    color: var(--text-muted);
    flex: 1;
}
```
Add directly after it:
```css
#tb-editor-host .tb-compare-subject {
    font-size: 12px;
    color: var(--text-muted);
    padding: 4px .75em 8px;
    word-break: break-word;
}
```
(Reuses existing design tokens — `--text`, `--text-muted`, `--surface2`, `--border`, `--radius` — already defined at the top of this file and used throughout, so this stays visually consistent and theme-aware (dark/light) without introducing new tokens.)

- [ ] **Step 2: Build**
```bash
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj --nologo -v minimal
```
Expected: builds clean (CSS is a static asset — no compile step to fail, this just confirms nothing else regressed).

- [ ] **Step 3: Commit**
```bash
git add src/TemplateBuilder.Editor/wwwroot/css/template-editor.css
git commit -m "feat: style the Subject rows in Preview and Compare"
```

---

## Task 10: Full regression, live verification, README changelog

**Files:**
- Modify: `src/TemplateBuilder.Editor/README.md` (changelog entry)
- Modify: `docs/status.md` (mark item 7 done)

- [ ] **Step 1: Run every test suite in the solution**
```bash
dotnet test TemplateBuilder.slnx --nologo -v minimal
```
Expected: every project's suite green, zero failures — including the `SchemaScriptGenerationTests` golden-file gate (regenerated in Task 2) and the `ApplyMigrationsOptionTests`/rest of `TemplateBuilder.Editor.Tests` from the earlier DBA-managed-databases work.

- [ ] **Step 2: Full solution build**
```bash
dotnet build TemplateBuilder.slnx --nologo -v minimal
```
Expected: `Build succeeded`, 0 errors (same pre-existing NU1510 warnings as every prior task in this session).

- [ ] **Step 3: Live verification — stand up the sample host**

Check for and stop any already-running dev server first (a stale one from an earlier session will lock the build output):
```bash
# If a dotnet process is already listening on 5299 from an earlier session, stop it first.
dotnet run --project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj --urls http://localhost:5299 > /tmp/tbweb-subject.log 2>&1 &
```
Wait a few seconds, then smoke-test with `curl`:
```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/Templates
```
Expected: `200`.

- [ ] **Step 4: Verify Subject end-to-end via HTTP (curl) — create an Email template with a subject, confirm it round-trips**

```bash
curl -s -c /tmp/tb-cookies.txt http://localhost:5299/Templates/Create > /tmp/create-page.html
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' /tmp/create-page.html | sed -E 's/.*value="([^"]*)".*/\1/')
curl -s -b /tmp/tb-cookies.txt -c /tmp/tb-cookies.txt \
  -H "Content-Type: application/json" -H "RequestVerificationToken: $TOKEN" \
  -d '{"name":"Subject E2E","templateType":"Email","description":"e2e","subject":"Order {{ model.OrderId }} shipped","body":"<p>Hi {{ model.Name }}</p>"}' \
  http://localhost:5299/Templates/Create
```
Expected: JSON response with a `templateId`. Then fetch its Edit page and confirm the Subject value is present:
```bash
curl -s -b /tmp/tb-cookies.txt "http://localhost:5299/Templates/<templateId>/Edit" | grep -o 'id="prop-subject"[^>]*value="[^"]*"'
```
Expected: contains `Order {{ model.OrderId }} shipped` (HTML-encoded as needed).

- [ ] **Step 5: Try a browser check; fall back to curl-only verification if the browser tooling can't reach localhost**

If browser automation is available and can reach `http://localhost:5299` (it could not in an earlier attempt this session — a Chrome-extension connectivity issue unrelated to the app), open the Edit page for the template created in Step 4, confirm the Subject input renders with its value and the field toggles visibility when Type is switched away from Email and back. If it can't reach the app, don't claim visual verification — state plainly that only the `curl`-based HTTP round-trip in Step 4 was confirmed, matching how the DBA-managed-databases item was handled earlier this session.

- [ ] **Step 6: Tear down the dev server**
```bash
# stop the process started in Step 3
```

- [ ] **Step 7: Add a `What's New` entry to the README**

In `src/TemplateBuilder.Editor/README.md`, under the still-unreleased `### v2.3.0` entry (the same one items 1, 4, 5, and 6 from `docs/status.md` were added to earlier this session), add:
```markdown
- **Subject field for Email templates** — an optional `Subject` on `TemplateVersion`, versioned
  alongside `Body`. Shown only when a template's Type is `Email`; supports the same
  `{{ model.X }}` Scriban syntax and field-palette insertion as the body. Rendered via the new
  `ITemplateEngine.RenderEmailAsync(templateId, model)` (returns `RenderedEmail { Subject, Body }`);
  never passed through the HTML sanitizer (it's plain text). Carried through Preview, Restore,
  Duplicate, Compare, and Template Promotion export/import (`TemplateExportDocument.SchemaVersion`
  bumped `2 → 3` — **promotion files exported before this release are now rejected on import**,
  matching this package's existing precedent for schema-version changes, not silently upgraded).
```

- [ ] **Step 8: Update `docs/status.md`**

Mark item 7 `done` with a summary note (implementation complete per this plan; version bump/packaging/publish are a separate follow-up decision, matching how items 1/4/5/6 were left un-bumped at `v2.3.0` pending the user's call on when to release).

- [ ] **Step 9: Commit**
```bash
git add src/TemplateBuilder.Editor/README.md docs/status.md
git commit -m "docs: Subject field changelog entry, mark backport item 7 done"
```

---

## Self-Review Notes (from writing this plan)

- **Spec coverage:** every section of the sibling repo's design spec maps to a task here — data model/migration (Task 2), rendering/preview (Tasks 3, 6 Step 4), editor UI/controllers (Tasks 5–8), promotion export/import (Task 4), testing (folded into each task's own test steps plus Task 10's full-suite pass).
- **Type/name consistency checked:** `RenderedEmail{Subject,Body}` (Task 1) is exactly what Task 3's `RenderEmailAsync` returns. `TemplateExportVersion.Subject` (Task 4) is the exact property Task 4's own `BuildExportAsync`/`ImportAsync` edits reference. Every JS DOM id introduced in Task 7 (`prop-subject`, `prop-subject-row`, `preview-subject-row`, `preview-subject-text`, `compare-subject-current`, `compare-subject-old`) is exactly what Task 8's JS queries.
- **Placeholder scan:** no TBD/"add appropriate handling"/elided code — every step has literal, runnable code or an exact shell command.
- **Deviation from the sibling repo, called out on purpose:** `Subject` is appended as the *last* positional parameter on both `SaveVersionRequest` and `PreviewRequest` (not inserted mid-list like the sibling repo's plan did for its own, differently-shaped MVC5 model classes), specifically to avoid silently breaking this repo's existing positional test call sites.
- **Gap this plan explicitly does not close:** promoting a template exported before this feature (schema v2) will be rejected on import once this ships, with no upgrade path — matches this repo's own existing behavior for prior schema-version bumps, and was surfaced explicitly in the sibling repo's spec as a deliberate, not accidental, decision.
- **Two real bugs from the sibling repo's own review cycle are prevented here instead of discovered later:** `Duplicate` carrying `Subject` forward (Task 6 Step 7) and `prop-subject` being included in dirty-tracking + wired to `refreshUsedMarks` (Task 8 Step 5) — both were fixed in the sibling repo only after a whole-branch code review caught them (commit `d96d276`).
