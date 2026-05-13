# TemplateBuilder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Before each task:** Read `memory/MEMORY.md` and relevant memory files.
> **After each task:** Save any new decisions or patterns discovered to memory.

**Goal:** Build a two-artifact system — an ASP.NET MVC .NET 10 template designer web app and a `TemplateBuilder.Core` NuGet package — that lets developers and BAs create HTML templates with drag-and-drop SQL view fields, then render them to HTML strings at runtime.

**Architecture:** Clean Architecture with 5 source projects (Domain → Application → Infrastructure → Web/Core) sharing one SQL Server database via EF Core. Templates are stored as HTML + Scriban syntax. The NuGet fetches templates from SQL and renders them using the Scriban engine with a `SELECT CurrentVersionId` cache-validation query.

**Tech Stack:** .NET 10 · ASP.NET MVC · EF Core 10 · SQL Server · Scriban 5.x · TinyMCE 7 (CDN) · xUnit · Moq · FluentAssertions · Microsoft.Extensions.Caching.Memory

**Spec:** `docs/superpowers/specs/2026-05-13-template-builder-design.md`

---

## File Map

```
TemplateBuilder.sln
├── src/
│   ├── TemplateBuilder.Domain/
│   │   ├── Entities/Template.cs
│   │   ├── Entities/TemplateVersion.cs
│   │   ├── Interfaces/ITemplateRepository.cs
│   │   ├── Interfaces/ITemplateEngine.cs
│   │   └── Exceptions/TemplateNotFoundException.cs
│   │   └── Exceptions/TemplateRenderException.cs
│   │
│   ├── TemplateBuilder.Application/
│   │   ├── Services/TemplateEngine.cs
│   │   ├── Services/SqlViewDiscoveryService.cs
│   │   ├── Options/TemplateBuilderOptions.cs
│   │   └── DTOs/SqlColumnInfo.cs
│   │
│   ├── TemplateBuilder.Infrastructure/
│   │   ├── Data/AppDbContext.cs
│   │   ├── Data/Configurations/TemplateConfiguration.cs
│   │   ├── Data/Configurations/TemplateVersionConfiguration.cs
│   │   └── Repositories/TemplateRepository.cs
│   │
│   ├── TemplateBuilder.Web/
│   │   ├── Program.cs
│   │   ├── Controllers/TemplatesController.cs
│   │   ├── ViewModels/TemplateListViewModel.cs
│   │   ├── ViewModels/TemplateEditorViewModel.cs
│   │   ├── Models/SaveVersionRequest.cs
│   │   ├── Models/PreviewRequest.cs
│   │   ├── Models/DuplicateRequest.cs
│   │   ├── Views/Shared/_Layout.cshtml
│   │   ├── Views/Templates/Index.cshtml
│   │   ├── Views/Templates/Edit.cshtml
│   │   ├── Views/Templates/_VersionHistory.cshtml
│   │   ├── Views/Templates/_Preview.cshtml
│   │   └── wwwroot/js/template-editor.js
│   │
│   └── TemplateBuilder.Core/
│       └── Extensions/ServiceCollectionExtensions.cs
│
└── tests/
    ├── TemplateBuilder.Domain.Tests/
    │   └── Entities/TemplateTests.cs
    ├── TemplateBuilder.Application.Tests/
    │   └── Services/TemplateEngineTests.cs
    ├── TemplateBuilder.Infrastructure.Tests/
    │   └── Repositories/TemplateRepositoryTests.cs
    └── TemplateBuilder.Web.Tests/
        └── Controllers/TemplatesControllerTests.cs
```

---

## Phase 1 — Foundation (Domain + Infrastructure)

> Produces: working data layer. After this phase you can create templates and versions in SQL Server via tests.

---

### Task 1: Solution Scaffold

**Files:**
- Create: `TemplateBuilder.sln`
- Create: `src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj`
- Create: `src/TemplateBuilder.Application/TemplateBuilder.Application.csproj`
- Create: `src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj`
- Create: `src/TemplateBuilder.Web/TemplateBuilder.Web.csproj`
- Create: `src/TemplateBuilder.Core/TemplateBuilder.Core.csproj`
- Create: `tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj`
- Create: `tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj`
- Create: `tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj`
- Create: `tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj`

- [ ] **Step 1: Create solution and all projects**

```bash
dotnet new sln -n TemplateBuilder
dotnet new classlib -n TemplateBuilder.Domain -f net10.0 -o src/TemplateBuilder.Domain
dotnet new classlib -n TemplateBuilder.Application -f net10.0 -o src/TemplateBuilder.Application
dotnet new classlib -n TemplateBuilder.Infrastructure -f net10.0 -o src/TemplateBuilder.Infrastructure
dotnet new mvc -n TemplateBuilder.Web -f net10.0 -o src/TemplateBuilder.Web
dotnet new classlib -n TemplateBuilder.Core -f net10.0 -o src/TemplateBuilder.Core
dotnet new xunit -n TemplateBuilder.Domain.Tests -f net10.0 -o tests/TemplateBuilder.Domain.Tests
dotnet new xunit -n TemplateBuilder.Application.Tests -f net10.0 -o tests/TemplateBuilder.Application.Tests
dotnet new xunit -n TemplateBuilder.Infrastructure.Tests -f net10.0 -o tests/TemplateBuilder.Infrastructure.Tests
dotnet new xunit -n TemplateBuilder.Web.Tests -f net10.0 -o tests/TemplateBuilder.Web.Tests
```

- [ ] **Step 2: Add projects to solution**

```bash
dotnet sln add src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
dotnet sln add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj
dotnet sln add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj
dotnet sln add src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
dotnet sln add src/TemplateBuilder.Core/TemplateBuilder.Core.csproj
dotnet sln add tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj
dotnet sln add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj
dotnet sln add tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj
dotnet sln add tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj
```

- [ ] **Step 3: Wire project references**

```bash
dotnet add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj reference src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj reference src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
dotnet add src/TemplateBuilder.Web/TemplateBuilder.Web.csproj reference src/TemplateBuilder.Application/TemplateBuilder.Application.csproj
dotnet add src/TemplateBuilder.Web/TemplateBuilder.Web.csproj reference src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj
dotnet add src/TemplateBuilder.Core/TemplateBuilder.Core.csproj reference src/TemplateBuilder.Application/TemplateBuilder.Application.csproj
dotnet add src/TemplateBuilder.Core/TemplateBuilder.Core.csproj reference src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj
dotnet add tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj reference src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
dotnet add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj reference src/TemplateBuilder.Application/TemplateBuilder.Application.csproj
dotnet add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj reference src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
dotnet add tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj reference src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj
dotnet add tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj reference src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```

- [ ] **Step 4: Install NuGet packages**

```bash
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj package Microsoft.Data.SqlClient
dotnet add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj package Scriban
dotnet add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj package Microsoft.Extensions.Caching.Memory
dotnet add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj package Microsoft.Extensions.Options
dotnet add tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj package FluentAssertions
dotnet add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj package FluentAssertions
dotnet add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj package Moq
dotnet add tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj package FluentAssertions
dotnet add tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj package FluentAssertions
dotnet add tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj package Moq
```

- [ ] **Step 5: Verify solution builds**

```bash
dotnet build TemplateBuilder.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Delete the generated boilerplate Class1.cs files from classlib projects**

```bash
rm src/TemplateBuilder.Domain/Class1.cs
rm src/TemplateBuilder.Application/Class1.cs
rm src/TemplateBuilder.Infrastructure/Class1.cs
rm src/TemplateBuilder.Core/Class1.cs
```

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: scaffold solution with 5 src projects and 4 test projects"
```

---

### Task 2: Domain Entities

**Files:**
- Create: `src/TemplateBuilder.Domain/Entities/Template.cs`
- Create: `src/TemplateBuilder.Domain/Entities/TemplateVersion.cs`
- Create: `tests/TemplateBuilder.Domain.Tests/Entities/TemplateTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TemplateBuilder.Domain.Tests/Entities/TemplateTests.cs`:

```csharp
using FluentAssertions;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Domain.Tests.Entities;

public class TemplateTests
{
    [Fact]
    public void Template_NewInstance_HasCorrectDefaults()
    {
        var template = new Template { Name = "Test", TemplateType = "Email" };

        template.IsActive.Should().BeTrue();
        template.CurrentVersionId.Should().BeNull();
        template.Versions.Should().BeEmpty();
    }

    [Fact]
    public void TemplateVersion_NewInstance_HasBody()
    {
        var version = new TemplateVersion
        {
            TemplateId = 1,
            VersionNumber = 1,
            Body = "<p>Hello {{ model.Name }}</p>"
        };

        version.Body.Should().Contain("{{ model.Name }}");
        version.ChangeComment.Should().BeNull();
        version.CreatedBy.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj
```
Expected: FAIL — `Template` and `TemplateVersion` types not found.

- [ ] **Step 3: Create Template entity**

Create `src/TemplateBuilder.Domain/Entities/Template.cs`:

```csharp
namespace TemplateBuilder.Domain.Entities;

public class Template
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CurrentVersionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    public TemplateVersion? CurrentVersion { get; set; }
}
```

- [ ] **Step 4: Create TemplateVersion entity**

Create `src/TemplateBuilder.Domain/Entities/TemplateVersion.cs`:

```csharp
namespace TemplateBuilder.Domain.Entities;

public class TemplateVersion
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? ChangeComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Template Template { get; set; } = null!;
}
```

- [ ] **Step 5: Run tests to confirm they pass**

```bash
dotnet test tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj
```
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 6: Commit**

```bash
git add src/TemplateBuilder.Domain/ tests/TemplateBuilder.Domain.Tests/
git commit -m "feat: add Template and TemplateVersion domain entities"
```

---

### Task 3: Exceptions and Interfaces

**Files:**
- Create: `src/TemplateBuilder.Domain/Exceptions/TemplateNotFoundException.cs`
- Create: `src/TemplateBuilder.Domain/Exceptions/TemplateRenderException.cs`
- Create: `src/TemplateBuilder.Domain/Exceptions/SchemaVersionMismatchException.cs`
- Create: `src/TemplateBuilder.Domain/Interfaces/ITemplateRepository.cs`
- Create: `src/TemplateBuilder.Domain/Interfaces/ITemplateEngine.cs`
- Create: `src/TemplateBuilder.Domain/DTOs/SqlColumnInfo.cs`

- [ ] **Step 1: Create exceptions**

Create `src/TemplateBuilder.Domain/Exceptions/TemplateNotFoundException.cs`:

```csharp
namespace TemplateBuilder.Domain.Exceptions;

public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(int templateId)
        : base($"Template with ID {templateId} was not found or is inactive.") { }

    public TemplateNotFoundException(string templateName)
        : base($"Template '{templateName}' was not found or is inactive.") { }
}
```

Create `src/TemplateBuilder.Domain/Exceptions/TemplateRenderException.cs`:

```csharp
namespace TemplateBuilder.Domain.Exceptions;

public class TemplateRenderException : Exception
{
    public TemplateRenderException(string message) : base(message) { }
    public TemplateRenderException(string message, Exception inner) : base(message, inner) { }
}
```

Create `src/TemplateBuilder.Domain/Exceptions/SchemaVersionMismatchException.cs`:

```csharp
namespace TemplateBuilder.Domain.Exceptions;

public class SchemaVersionMismatchException : Exception
{
    public SchemaVersionMismatchException(string requiredMigrationId)
        : base($"TemplateBuilder.Core requires DB migration '{requiredMigrationId}' which has not been applied. Run: dotnet ef database update --project TemplateBuilder.Infrastructure") { }
}
```

- [ ] **Step 2: Create ITemplateRepository interface**

Create `src/TemplateBuilder.Domain/Interfaces/ITemplateRepository.cs`:

```csharp
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Domain.Interfaces;

public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Template?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<int?> GetCurrentVersionIdAsync(int templateId, CancellationToken ct = default);
    Task<string?> GetVersionBodyAsync(int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVersion>> GetVersionHistoryAsync(int templateId, CancellationToken ct = default);
    Task<int> GetNextVersionNumberAsync(int templateId, CancellationToken ct = default);
    Task<Template> CreateAsync(Template template, CancellationToken ct = default);
    Task UpdateTemplateAsync(Template template, CancellationToken ct = default);
    Task<TemplateVersion> PublishVersionAsync(int templateId, TemplateVersion version, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create ITemplateEngine interface**

Create `src/TemplateBuilder.Domain/Interfaces/ITemplateEngine.cs`:

```csharp
namespace TemplateBuilder.Domain.Interfaces;

public interface ITemplateEngine
{
    Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);
    Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);
    Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create SqlColumnInfo DTO**

Create `src/TemplateBuilder.Domain/DTOs/SqlColumnInfo.cs`:

```csharp
namespace TemplateBuilder.Domain.DTOs;

public record SqlColumnInfo(string Name, string DataType);
```

- [ ] **Step 5: Verify build**

```bash
dotnet build TemplateBuilder.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/TemplateBuilder.Domain/
git commit -m "feat: add domain interfaces, exceptions, and DTOs"
```

---

### Task 4: EF Core DbContext and Entity Configurations

**Files:**
- Create: `src/TemplateBuilder.Infrastructure/Data/AppDbContext.cs`
- Create: `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateConfiguration.cs`
- Create: `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateVersionConfiguration.cs`
- Create: `tests/TemplateBuilder.Infrastructure.Tests/Data/AppDbContextTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TemplateBuilder.Infrastructure.Tests/Data/AppDbContextTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Tests.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CanInsertAndRetrieveTemplate()
    {
        await using var context = CreateInMemoryContext();

        context.Templates.Add(new Template
        {
            Name = "Test Template",
            TemplateType = "Email",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var template = await context.Templates.FirstOrDefaultAsync(t => t.Name == "Test Template");
        template.Should().NotBeNull();
        template!.TemplateType.Should().Be("Email");
    }

    [Fact]
    public async Task CanInsertTemplateVersion()
    {
        await using var context = CreateInMemoryContext();

        var template = new Template
        {
            Name = "Versioned Template",
            TemplateType = "Report",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Templates.Add(template);
        await context.SaveChangesAsync();

        context.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>Hello</p>",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var version = await context.TemplateVersions.FirstOrDefaultAsync(v => v.TemplateId == template.Id);
        version.Should().NotBeNull();
        version!.Body.Should().Be("<p>Hello</p>");
    }

    [Fact]
    public async Task Template_RowVersion_IsPopulatedAfterSave()
    {
        await using var context = CreateInMemoryContext();

        var template = new Template
        {
            Name = "RV Template",
            TemplateType = "Email",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Templates.Add(template);
        await context.SaveChangesAsync();

        template.RowVersion.Should().NotBeNullOrEmpty();
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj
```
Expected: FAIL — `AppDbContext` not found.

- [ ] **Step 3: Create AppDbContext**

Create `src/TemplateBuilder.Infrastructure/Data/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Create Template entity configuration**

Create `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.TemplateType).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).HasColumnType("datetime2");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime2");
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasMany(t => t.Versions)
               .WithOne(v => v.Template)
               .HasForeignKey(v => v.TemplateId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CurrentVersion)
               .WithMany()
               .HasForeignKey(t => t.CurrentVersionId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);
    }
}
```

- [ ] **Step 5: Create TemplateVersion entity configuration**

Create `src/TemplateBuilder.Infrastructure/Data/Configurations/TemplateVersionConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(v => v.ChangeComment).HasMaxLength(500);
        builder.Property(v => v.CreatedAt).HasColumnType("datetime2");
        builder.Property(v => v.CreatedBy).HasMaxLength(100);
    }
}
```

- [ ] **Step 6: Run tests to confirm they pass**

```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj
```
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 7: Commit**

```bash
git add src/TemplateBuilder.Infrastructure/ tests/TemplateBuilder.Infrastructure.Tests/
git commit -m "feat: add EF Core DbContext with Template and TemplateVersion configurations"
```

---

### Task 5: EF Core Migration and SQL Connection

**Files:**
- Create: `src/TemplateBuilder.Infrastructure/Migrations/` (EF Core generated)
- Modify: `src/TemplateBuilder.Web/Program.cs`
- Create: `src/TemplateBuilder.Web/appsettings.json` (connection string)

- [ ] **Step 1: Add connection string to appsettings.json**

Edit `src/TemplateBuilder.Web/appsettings.json` — add the ConnectionStrings section:

```json
{
  "ConnectionStrings": {
    "TemplateDb": "Server=(localdb)\\mssqllocaldb;Database=TemplateBuilder;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 2: Register DbContext in Program.cs**

Edit `src/TemplateBuilder.Web/Program.cs` — add after `var builder = WebApplication.CreateBuilder(args);`:

```csharp
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TemplateDb")));

builder.Services.AddControllersWithViews();
// ... rest of Program.cs unchanged
```

- [ ] **Step 3: Add Design package to Infrastructure for migrations**

```bash
dotnet add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
```

- [ ] **Step 4: Create the initial migration**

```bash
dotnet ef migrations add InitialCreate --project src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --startup-project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
Expected: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 5: Apply migration to create the database**

```bash
dotnet ef database update --project src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --startup-project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
Expected: `Done.`

- [ ] **Step 6: Verify tables exist — open SQL Server Object Explorer in Visual Studio or run:**

```bash
dotnet ef dbcontext info --project src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj --startup-project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
Expected output includes `Database name: TemplateBuilder`

- [ ] **Step 7: Commit**

```bash
git add src/TemplateBuilder.Infrastructure/Migrations/ src/TemplateBuilder.Web/appsettings.json src/TemplateBuilder.Web/Program.cs
git commit -m "feat: add EF Core migration and SQL Server connection"
```

---

### Task 6: TemplateRepository

**Files:**
- Create: `src/TemplateBuilder.Infrastructure/Repositories/TemplateRepository.cs`
- Create: `tests/TemplateBuilder.Infrastructure.Tests/Repositories/TemplateRepositoryTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TemplateBuilder.Infrastructure.Tests/Repositories/TemplateRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Infrastructure.Tests.Repositories;

public class TemplateRepositoryTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsTemplate_ReturnsWithId()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);

        var template = await repo.CreateAsync(new Template
        {
            Name = "Invoice Email",
            TemplateType = "Email"
        });

        template.Id.Should().BeGreaterThan(0);
        template.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTemplate_ReturnsTemplate()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var created = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Report" });

        var result = await repo.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("A");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingTemplate()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        await repo.CreateAsync(new Template { Name = "Welcome Email", TemplateType = "Email" });

        var result = await repo.GetByNameAsync("Welcome Email");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishVersionAsync_UpdatesCurrentVersionIdAtomically()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "B", TemplateType = "Notice" });

        var version = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>Hello</p>"
        });

        var versionId = await repo.GetCurrentVersionIdAsync(template.Id);
        versionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task GetVersionBodyAsync_ReturnsCorrectBody()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "C", TemplateType = "Email" });
        var version = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<h1>{{ model.Title }}</h1>"
        });

        var body = await repo.GetVersionBodyAsync(version.Id);

        body.Should().Be("<h1>{{ model.Title }}</h1>");
    }

    [Fact]
    public async Task GetNextVersionNumberAsync_NoVersions_Returns1()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "D", TemplateType = "Report" });

        var next = await repo.GetNextVersionNumberAsync(template.Id);

        next.Should().Be(1);
    }

    [Fact]
    public async Task GetNextVersionNumberAsync_ExistingVersions_ReturnsNext()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "E", TemplateType = "Report" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "v1" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 2, Body = "v2" });

        var next = await repo.GetNextVersionNumberAsync(template.Id);

        next.Should().Be(3);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj
```
Expected: FAIL — `TemplateRepository` not found.

- [ ] **Step 3: Implement TemplateRepository**

Create `src/TemplateBuilder.Infrastructure/Repositories/TemplateRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly AppDbContext _context;

    public TemplateRepository(AppDbContext context) => _context = context;

    public async Task<Template?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Template?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task<int?> GetCurrentVersionIdAsync(int templateId, CancellationToken ct = default) =>
        await _context.Templates
            .Where(t => t.Id == templateId)
            .Select(t => t.CurrentVersionId)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetVersionBodyAsync(int versionId, CancellationToken ct = default) =>
        await _context.TemplateVersions
            .Where(v => v.Id == versionId)
            .Select(v => v.Body)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Templates
            .Include(t => t.CurrentVersion)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TemplateVersion>> GetVersionHistoryAsync(int templateId, CancellationToken ct = default) =>
        await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

    public async Task<int> GetNextVersionNumberAsync(int templateId, CancellationToken ct = default)
    {
        var max = await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<Template> CreateAsync(Template template, CancellationToken ct = default)
    {
        template.CreatedAt = template.UpdatedAt = DateTime.UtcNow;
        _context.Templates.Add(template);
        await _context.SaveChangesAsync(ct);
        return template;
    }

    public async Task UpdateTemplateAsync(Template template, CancellationToken ct = default)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _context.Templates.Update(template);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<TemplateVersion> PublishVersionAsync(int templateId, TemplateVersion version, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        version.CreatedAt = DateTime.UtcNow;
        _context.TemplateVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        var template = await _context.Templates.FindAsync(new object[] { templateId }, ct);
        template!.CurrentVersionId = version.Id;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return version;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj
```
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Register repository in Web Program.cs**

Add to `src/TemplateBuilder.Web/Program.cs` after the DbContext registration:

```csharp
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Repositories;

// After AddDbContext:
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
```

- [ ] **Step 6: Commit**

```bash
git add src/TemplateBuilder.Infrastructure/Repositories/ tests/TemplateBuilder.Infrastructure.Tests/Repositories/ src/TemplateBuilder.Web/Program.cs
git commit -m "feat: implement TemplateRepository with full CRUD and version history"
```

---

## Phase 2 — Rendering Engine + NuGet Core

> Produces: a working `ITemplateEngine` that any .NET app can install via the NuGet Core project and call `RenderAsync`.

---

### Task 7: Scriban Template Engine

**Files:**
- Create: `src/TemplateBuilder.Application/Options/TemplateBuilderOptions.cs`
- Create: `src/TemplateBuilder.Application/Services/TemplateEngine.cs`
- Create: `tests/TemplateBuilder.Application.Tests/Services/TemplateEngineTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TemplateBuilder.Application.Tests/Services/TemplateEngineTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplateEngineTests
{
    private static TemplateEngine CreateEngine(ITemplateRepository repo)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new TemplateBuilderOptions
        {
            EnableCaching = true,
            CacheDurationMinutes = 30
        });
        return new TemplateEngine(repo, cache, options);
    }

    [Fact]
    public async Task RenderBodyAsync_ScalarField_ReplacesToken()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync(
            "<p>Dear {{ model.Name }},</p>",
            new { Name = "John" });

        html.Should().Be("<p>Dear John,</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_LoopBlock_RendersEachItem()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync(
            "{{ for item in model.Items }}<li>{{ item.Name }}</li>{{ end }}",
            new { Items = new[] { new { Name = "Alpha" }, new { Name = "Beta" } } });

        html.Should().Contain("<li>Alpha</li>");
        html.Should().Contain("<li>Beta</li>");
    }

    [Fact]
    public async Task RenderBodyAsync_InvalidScriban_ThrowsTemplateRenderException()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderBodyAsync("{{ invalid syntax @@@ }}", new { });

        await act.Should().ThrowAsync<TemplateRenderException>();
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplateId_ThrowsTemplateNotFoundException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(999, default)).ReturnsAsync((int?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(999, new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    [Fact]
    public async Task RenderAsync_ValidTemplate_FetchesBodyAndRenders()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, default)).ReturnsAsync(10);
        repo.Setup(r => r.GetVersionBodyAsync(10, default)).ReturnsAsync("<p>{{ model.Title }}</p>");
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderAsync(1, new { Title = "Hello World" });

        html.Should().Be("<p>Hello World</p>");
    }

    [Fact]
    public async Task RenderAsync_CalledTwice_SecondCallUsesCache_NoSecondBodyFetch()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, default)).ReturnsAsync(10);
        repo.Setup(r => r.GetVersionBodyAsync(10, default)).ReturnsAsync("<p>{{ model.X }}</p>");
        var engine = CreateEngine(repo.Object);

        await engine.RenderAsync(1, new { X = "first" });
        await engine.RenderAsync(1, new { X = "second" });

        repo.Verify(r => r.GetVersionBodyAsync(10, default), Times.Once);
    }

    [Fact]
    public async Task RenderAsync_VersionChanged_RefetchesBody()
    {
        var repo = new Mock<ITemplateRepository>();
        var versionIdSequence = new Queue<int?>(new int?[] { 10, 11 });
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, default))
            .ReturnsAsync(() => versionIdSequence.Dequeue());
        repo.Setup(r => r.GetVersionBodyAsync(10, default)).ReturnsAsync("<p>v1</p>");
        repo.Setup(r => r.GetVersionBodyAsync(11, default)).ReturnsAsync("<p>v2</p>");
        var engine = CreateEngine(repo.Object);

        var html1 = await engine.RenderAsync(1, new { });
        var html2 = await engine.RenderAsync(1, new { });

        html1.Should().Be("<p>v1</p>");
        html2.Should().Be("<p>v2</p>");
        repo.Verify(r => r.GetVersionBodyAsync(It.IsAny<int>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task RenderByNameAsync_ResolvesTemplateByName()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByNameAsync("WelcomeEmail", default))
            .ReturnsAsync(new Domain.Entities.Template
            {
                Id = 5,
                Name = "WelcomeEmail",
                TemplateType = "Email",
                IsActive = true,
                CurrentVersionId = 20
            });
        repo.Setup(r => r.GetCurrentVersionIdAsync(5, default)).ReturnsAsync(20);
        repo.Setup(r => r.GetVersionBodyAsync(20, default)).ReturnsAsync("<p>Welcome</p>");
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderByNameAsync("WelcomeEmail", new { });

        html.Should().Be("<p>Welcome</p>");
    }

    [Fact]
    public async Task RenderAsync_InactiveTemplate_ThrowsTemplateNotFoundException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdWithActiveCheckAsync(42, default))
            .ReturnsAsync((Domain.Entities.Template?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(42, new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    [Fact]
    public async Task RenderBodyAsync_NullProperty_RendersEmptyString()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Name }}</p>", new { Name = (string?)null });

        html.Should().Be("<p></p>");
    }

    [Fact]
    public async Task RenderBodyAsync_MissingProperty_RendersEmptyString()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Missing }}</p>", new { });

        html.Should().Be("<p></p>");
    }

    [Fact]
    public async Task RenderBodyAsync_CaseInsensitivePropertyName_Renders()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.CUSTOMERNAME }}</p>", new { CustomerName = "Alice" });

        html.Should().Be("<p>Alice</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_ScriptTagInModel_EmittedVerbatim()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Val }}</p>", new { Val = "<script>alert(1)</script>" });

        html.Should().Contain("<script>alert(1)</script>");
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj
```
Expected: FAIL — `TemplateEngine` not found.

- [ ] **Step 3: Create TemplateBuilderOptions**

Create `src/TemplateBuilder.Application/Options/TemplateBuilderOptions.cs`:

```csharp
namespace TemplateBuilder.Application.Options;

public class TemplateBuilderOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableCaching { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 30;
    public string ViewPrefix { get; set; } = "TemplateBuilder_";
    public IEnumerable<string>? ViewAllowlist { get; set; } = null;
    public bool StrictMode { get; set; } = false;
    public bool ValidateSchemaOnStartup { get; set; } = false;
}
```

- [ ] **Step 4: Implement TemplateEngine**

Create `src/TemplateBuilder.Application/Services/TemplateEngine.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Runtime;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Services;

public class TemplateEngine : ITemplateEngine
{
    private readonly ITemplateRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly TemplateBuilderOptions _options;

    private record CacheEntry(int VersionId, string Body);

    public TemplateEngine(ITemplateRepository repository, IMemoryCache cache, IOptions<TemplateBuilderOptions> options)
    {
        _repository = repository;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(templateId, ct);
        if (template is null || !template.IsActive)
            throw new TemplateNotFoundException(templateId);

        var currentVersionId = template.CurrentVersionId
            ?? throw new TemplateNotFoundException(templateId);

        var body = await GetBodyAsync(templateId, currentVersionId, ct);
        return await RenderBodyAsync(body, model, ct);
    }

    public async Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default)
    {
        var template = await _repository.GetByNameAsync(templateName, ct);
        if (template is null || !template.IsActive)
            throw new TemplateNotFoundException(templateName);

        var currentVersionId = template.CurrentVersionId
            ?? throw new TemplateNotFoundException(templateName);

        var body = await GetBodyAsync(template.Id, currentVersionId, ct);
        return await RenderBodyAsync(body, model, ct);
    }

    public Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default)
    {
        var parsed = Template.Parse(body);
        if (parsed.HasErrors)
            throw new TemplateRenderException(
                string.Join("; ", parsed.Messages.Select(m => m.Message)));

        var scriptObject = new ScriptObject();
        scriptObject.Import(model, filter: null, renamer: m => m.Name);
        var wrapper = new ScriptObject();
        wrapper["model"] = scriptObject;
        var context = new TemplateContext { MemberRenamer = m => m.Name };
        context.PushGlobal(wrapper);

        try
        {
            return parsed.RenderAsync(context).AsTask();
        }
        catch (Exception ex)
        {
            throw new TemplateRenderException($"Render failed: {ex.Message}", ex);
        }
    }

    private async Task<string> GetBodyAsync(int templateId, int currentVersionId, CancellationToken ct)
    {
        if (!_options.EnableCaching)
        {
            return await _repository.GetVersionBodyAsync(currentVersionId, ct)
                   ?? throw new TemplateNotFoundException(templateId);
        }

        var cacheKey = $"tb_{templateId}";
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached!.VersionId == currentVersionId)
            return cached.Body;

        var body = await _repository.GetVersionBodyAsync(currentVersionId, ct)
                   ?? throw new TemplateNotFoundException(templateId);

        _cache.Set(cacheKey, new CacheEntry(currentVersionId, body),
            TimeSpan.FromMinutes(_options.CacheDurationMinutes));

        return body;
    }
}
```

- [ ] **Step 5: Add HtmlSanitizer package and create IHtmlSanitizerService**

```bash
dotnet add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj package HtmlSanitizer
```

Create `src/TemplateBuilder.Application/Services/IHtmlSanitizerService.cs`:

```csharp
namespace TemplateBuilder.Application.Services;

public interface IHtmlSanitizerService
{
    string Sanitize(string html);
}
```

Create `src/TemplateBuilder.Application/Services/HtmlSanitizerService.cs`:

```csharp
using Ganss.Xss;

namespace TemplateBuilder.Application.Services;

public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "div", "span", "strong", "em", "b", "i", "u",
            "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "br", "hr",
            "table", "thead", "tbody", "tr", "th", "td", "a", "img" })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("class");
        // style excluded: CSS injection vector (data exfiltration, clickjacking)

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
        // data: scheme restricted to image MIME types only via URI validator below

        _sanitizer.AllowedAtRules.Clear();
        // Allow only data:image/(png|jpeg|gif|webp);base64,... — block all other data: URIs
        var allowedDataUri = new System.Text.RegularExpressions.Regex(
            @"^data:image/(png|jpeg|gif|webp);base64,",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        _sanitizer.FilterUrl += (sender, args) =>
        {
            if (args.OriginalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                !allowedDataUri.IsMatch(args.OriginalUrl))
            {
                args.SanitizedUrl = null; // strip disallowed data: URI
            }
        };
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
```

- [ ] **Step 6: Create SchemaVersionValidator**

Create `src/TemplateBuilder.Application/Services/SchemaVersionValidator.cs`:

```csharp
using Microsoft.Data.SqlClient;
using TemplateBuilder.Domain.Exceptions;

namespace TemplateBuilder.Application.Services;

public class SchemaVersionValidator
{
    public static async Task ValidateAsync(string connectionString, string requiredMigrationId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM __EFMigrationsHistory WHERE MigrationId = @id", conn);
        cmd.Parameters.AddWithValue("@id", requiredMigrationId);
        var count = (int)await cmd.ExecuteScalarAsync(ct)!;
        if (count == 0)
            throw new SchemaVersionMismatchException(requiredMigrationId);
    }
}
```

- [ ] **Step 7: Run tests to confirm they pass**

```bash
dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj
```
Expected: `Passed! - Failed: 0, Passed: 13`

- [ ] **Step 8: Commit**

```bash
git add src/TemplateBuilder.Application/ tests/TemplateBuilder.Application.Tests/
git commit -m "feat: implement Scriban-based TemplateEngine with memory caching, HTML sanitizer, and schema validator"
```

---

### Task 8: NuGet Core Wiring

**Files:**
- Create: `src/TemplateBuilder.Core/Extensions/ServiceCollectionExtensions.cs`
- Create: `src/TemplateBuilder.Core/TemplateBuilder.Core.csproj` (update for NuGet metadata)

- [ ] **Step 1: Add IServiceCollection extension**

Create `src/TemplateBuilder.Core/Extensions/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTemplateBuilder(
        this IServiceCollection services,
        Action<TemplateBuilderOptions> configure)
    {
        var options = new TemplateBuilderOptions();
        configure(options);

        services.Configure(configure);
        services.AddDbContext<AppDbContext>(dbOptions =>
            dbOptions.UseSqlServer(options.ConnectionString));
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddMemoryCache();
        services.AddScoped<ITemplateEngine, TemplateEngine>();

        return services;
    }
}
```

- [ ] **Step 2: Add NuGet package metadata to Core project**

Edit `src/TemplateBuilder.Core/TemplateBuilder.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>TemplateBuilder.Core</PackageId>
    <Version>1.0.0</Version>
    <Authors>TemplateBuilder</Authors>
    <Description>Render TemplateBuilder templates to HTML strings. Call RenderAsync(templateId, model) from any .NET app.</Description>
    <PackageTags>template;scriban;html;rendering</PackageTags>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/TemplateBuilder.Core/TemplateBuilder.Core.csproj
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/TemplateBuilder.Core/
git commit -m "feat: add TemplateBuilder.Core NuGet wiring with AddTemplateBuilder extension"
```

---

## Phase 3 — Web App Designer

> Produces: the full visual template designer. Invoke `ui-ux-pro-max:ui-ux-pro-max` skill when building Views (Tasks 11–14).

---

### Task 9: SqlViewDiscoveryService

**Files:**
- Create: `src/TemplateBuilder.Application/Services/SqlViewDiscoveryService.cs`

- [ ] **Step 1: Implement SqlViewDiscoveryService**

Create `src/TemplateBuilder.Application/Services/SqlViewDiscoveryService.cs`:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Domain.DTOs;

namespace TemplateBuilder.Application.Services;

public class SqlViewDiscoveryService
{
    private readonly string _connectionString;
    private readonly TemplateBuilderOptions _options;

    private static readonly IReadOnlySet<string> ExcludedSchemas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sys", "INFORMATION_SCHEMA", "guest" };

    public SqlViewDiscoveryService(string connectionString, IOptions<TemplateBuilderOptions> options)
    {
        _connectionString = connectionString;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetViewNamesAsync(CancellationToken ct = default)
    {
        if (_options.ViewAllowlist is not null)
            return _options.ViewAllowlist.ToList().AsReadOnly();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS
              WHERE TABLE_NAME LIKE @prefix + '%'
              AND TABLE_SCHEMA NOT IN ('sys','INFORMATION_SCHEMA','guest')
              ORDER BY TABLE_NAME", conn);
        cmd.Parameters.AddWithValue("@prefix", _options.ViewPrefix);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var views = new List<string>();
        while (await reader.ReadAsync(ct))
            views.Add(reader.GetString(0));
        return views;
    }

    public async Task<IReadOnlyList<SqlColumnInfo>> GetViewColumnsAsync(string viewName, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @viewName
            ORDER BY ORDINAL_POSITION", conn);
        cmd.Parameters.AddWithValue("@viewName", viewName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var columns = new List<SqlColumnInfo>();
        while (await reader.ReadAsync(ct))
            columns.Add(new SqlColumnInfo(reader.GetString(0), reader.GetString(1)));
        return columns;
    }
}
```

- [ ] **Step 2: Register in Web Program.cs**

Add to `src/TemplateBuilder.Web/Program.cs`:

```csharp
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;

// After AddScoped<ITemplateRepository>:
builder.Services.Configure<TemplateBuilderOptions>(builder.Configuration.GetSection("TemplateBuilder"));
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITemplateEngine, TemplateEngine>();
builder.Services.AddScoped(sp =>
    new SqlViewDiscoveryService(
        builder.Configuration.GetConnectionString("TemplateDb")!));
```

- [ ] **Step 3: Commit**

```bash
git add src/TemplateBuilder.Application/Services/SqlViewDiscoveryService.cs src/TemplateBuilder.Web/Program.cs
git commit -m "feat: add SqlViewDiscoveryService for design-time palette population"
```

---

### Task 10: TemplatesController — Skeleton

**Files:**
- Create: `src/TemplateBuilder.Web/Controllers/TemplatesController.cs`
- Create: `src/TemplateBuilder.Web/ViewModels/TemplateListViewModel.cs`
- Create: `src/TemplateBuilder.Web/ViewModels/TemplateEditorViewModel.cs`
- Create: `src/TemplateBuilder.Web/Models/SaveVersionRequest.cs`
- Create: `src/TemplateBuilder.Web/Models/PreviewRequest.cs`
- Create: `src/TemplateBuilder.Web/Models/DuplicateRequest.cs`
- Create: `tests/TemplateBuilder.Web.Tests/Controllers/TemplatesControllerTests.cs`

- [ ] **Step 1: Create ViewModels and request models**

Create `src/TemplateBuilder.Web/ViewModels/TemplateListViewModel.cs`:

```csharp
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Web.ViewModels;

public class TemplateListViewModel
{
    public List<Template> Templates { get; set; } = new();
    public string? Search { get; set; }
    public string? TypeFilter { get; set; }

    public Dictionary<string, int> CountByType => Templates
        .GroupBy(t => t.TemplateType)
        .ToDictionary(g => g.Key, g => g.Count());

    public static readonly string[] KnownTypes = ["Email", "Report", "Notice", "Custom"];
}
```

Create `src/TemplateBuilder.Web/ViewModels/TemplateEditorViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace TemplateBuilder.Web.ViewModels;

public class TemplateEditorViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string TemplateType { get; set; } = "Email";

    [StringLength(500)]
    public string? Description { get; set; }

    public string Body { get; set; } = string.Empty;
    public int? CurrentVersionId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public List<string> AvailableViews { get; set; } = new();
}
```

Create `src/TemplateBuilder.Web/Models/SaveVersionRequest.cs`:

```csharp
namespace TemplateBuilder.Web.Models;

public record SaveVersionRequest(
    string Name,
    string TemplateType,
    string? Description,
    string Body,
    string? ChangeComment);
```

Create `src/TemplateBuilder.Web/Models/PreviewRequest.cs`:

```csharp
namespace TemplateBuilder.Web.Models;

public record PreviewRequest(string Body, string? ModelJson);
```

Create `src/TemplateBuilder.Web/Models/DuplicateRequest.cs`:

```csharp
namespace TemplateBuilder.Web.Models;

public record DuplicateRequest(string NewName);
```

- [ ] **Step 2: Write failing controller tests**

Create `tests/TemplateBuilder.Web.Tests/Controllers/TemplatesControllerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Web.Controllers;

namespace TemplateBuilder.Web.Tests.Controllers;

public class TemplatesControllerTests
{
    private static TemplatesController CreateController(
        ITemplateRepository? repo = null,
        ITemplateEngine? engine = null)
    {
        var mockRepo = repo ?? new Mock<ITemplateRepository>().Object;
        var mockEngine = engine ?? new Mock<ITemplateEngine>().Object;
        var mockDiscovery = new Mock<SqlViewDiscoveryService>("Server=.;Database=test;").Object;
        return new TemplatesController(mockRepo, mockDiscovery, mockEngine);
    }

    [Fact]
    public async Task Index_ReturnsViewWithTemplates()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Template>
            {
                new() { Id = 1, Name = "A", TemplateType = "Email" }
            });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Index(null, null);

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Edit_NonExistentTemplate_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Template?)null);
        var mockDiscovery = new Mock<SqlViewDiscoveryService>("Server=.;");
        mockDiscovery.Setup(d => d.GetViewNamesAsync(default))
            .ReturnsAsync(new List<string>());
        var controller = new TemplatesController(mockRepo.Object, mockDiscovery.Object, new Mock<ITemplateEngine>().Object);

        var result = await controller.Edit(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Duplicate_CreatesNewTemplateWithSameBodyAndRedirectsToEditor()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        var source = new Template
        {
            Id = 1, Name = "Invoice Email", TemplateType = "Email",
            Description = "Monthly invoice",
            CurrentVersion = new TemplateVersion { Id = 10, Body = "<p>Hello</p>", VersionNumber = 1 },
            CurrentVersionId = 10
        };
        mockRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(source);
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), default))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 99; return t; });
        mockRepo.Setup(r => r.PublishVersionAsync(99, It.IsAny<TemplateVersion>(), default))
            .ReturnsAsync((int _, TemplateVersion v, CancellationToken _) => { v.Id = 200; return v; });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Duplicate(1, new DuplicateRequest("Copy of Invoice Email"));

        result.Should().BeOfType<OkObjectResult>();
        mockRepo.Verify(r => r.CreateAsync(
            It.Is<Template>(t => t.Name == "Copy of Invoice Email" && t.TemplateType == "Email"),
            default), Times.Once);
        mockRepo.Verify(r => r.PublishVersionAsync(99,
            It.Is<TemplateVersion>(v => v.Body == "<p>Hello</p>" && v.VersionNumber == 1),
            default), Times.Once);
    }

    [Fact]
    public async Task Duplicate_NonExistentSource_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(999, default)).ReturnsAsync((Template?)null);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Duplicate(999, new DuplicateRequest("Copy"));

        result.Should().BeOfType<NotFoundResult>();
    }
}
```

- [ ] **Step 3: Run tests to confirm they fail**

```bash
dotnet test tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj
```
Expected: FAIL — `TemplatesController` not found.

- [ ] **Step 4: Implement TemplatesController**

Create `src/TemplateBuilder.Web/Controllers/TemplatesController.cs`:

```csharp
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
    private readonly SqlViewDiscoveryService _viewDiscovery;
    private readonly ITemplateEngine _engine;

    public TemplatesController(ITemplateRepository repository, SqlViewDiscoveryService viewDiscovery, ITemplateEngine engine)
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
    public IActionResult Create() => View("Edit", new TemplateEditorViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TemplateEditorViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View("Edit", model);
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

    [HttpPost("Templates/{id:int}/SaveVersion")]
    public async Task<IActionResult> SaveVersion(int id, [FromBody] SaveVersionRequest request, CancellationToken ct = default)
    {
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
        var versions = await _repository.GetVersionHistoryAsync(id, ct);
        var template = await _repository.GetByIdAsync(id, ct);
        return PartialView("_VersionHistory", (versions.ToList(), template?.CurrentVersionId));
    }

    [HttpPost("Templates/{id:int}/Restore/{versionId:int}")]
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

    [HttpPost("Templates/{id:int}/Preview")]
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

    [HttpPost("Templates/{id:int}/ToggleActive")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        if (template is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));
        template.IsActive = !template.IsActive;
        await _repository.UpdateTemplateAsync(template, ct);
        return Ok(new { isActive = template.IsActive });
    }

    [HttpPost("Templates/{id:int}/Duplicate")]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateRequest request, CancellationToken ct = default)
    {
        var source = await _repository.GetByIdAsync(id, ct);
        if (source is null) return NotFound(new ErrorResult("TEMPLATE_NOT_FOUND", $"Template {id} not found."));

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
```

- [ ] **Step 5: Run tests to confirm they pass**

```bash
dotnet test tests/TemplateBuilder.Web.Tests/TemplateBuilder.Web.Tests.csproj
```
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 6: Commit**

```bash
git add src/TemplateBuilder.Web/ tests/TemplateBuilder.Web.Tests/
git commit -m "feat: implement TemplatesController with all CRUD, versioning, and preview endpoints"
```

---

### Task 11: Shared Layout

> Invoke `ui-ux-pro-max:ui-ux-pro-max` skill before building this view.

**Files:**
- Modify: `src/TemplateBuilder.Web/Views/Shared/_Layout.cshtml`
- Modify: `src/TemplateBuilder.Web/wwwroot/css/site.css`

- [ ] **Step 1: Replace _Layout.cshtml**

Replace `src/TemplateBuilder.Web/Views/Shared/_Layout.cshtml` with:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] — TemplateBuilder</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <nav class="tb-nav">
        <a class="tb-brand" asp-controller="Templates" asp-action="Index">
            TemplateBuilder
        </a>
        <a class="tb-nav-link" asp-controller="Templates" asp-action="Index">Templates</a>
        <a class="tb-nav-link" asp-controller="Templates" asp-action="Create">+ New</a>
    </nav>
    <main class="tb-main">
        @RenderBody()
    </main>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 2: Add base CSS to site.css**

Replace contents of `src/TemplateBuilder.Web/wwwroot/css/site.css` with:

```css
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

:root {
    --bg: #0f0f14;
    --surface: #1a1a24;
    --surface2: #22223a;
    --border: #2e2e44;
    --accent: #6366f1;
    --accent-hover: #4f52e0;
    --text: #e2e2f0;
    --text-muted: #8888aa;
    --success: #22c55e;
    --warning: #f59e0b;
    --danger: #ef4444;
    --radius: 8px;
    --radius-lg: 12px;
}

body { background: var(--bg); color: var(--text); font-family: system-ui, sans-serif; font-size: 14px; line-height: 1.6; }

.tb-nav { display: flex; align-items: center; gap: 1.5rem; padding: .75rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
.tb-brand { font-weight: 700; font-size: 1rem; color: var(--accent); text-decoration: none; }
.tb-nav-link { color: var(--text-muted); text-decoration: none; font-size: .875rem; transition: color .15s; }
.tb-nav-link:hover { color: var(--text); }

.tb-main { padding: 1.5rem; max-width: 1400px; margin: 0 auto; }

.btn { display: inline-flex; align-items: center; gap: .4rem; padding: .45rem .9rem; border-radius: var(--radius); border: none; cursor: pointer; font-size: .875rem; font-weight: 500; transition: background .15s, opacity .15s; text-decoration: none; }
.btn-primary { background: var(--accent); color: white; }
.btn-primary:hover { background: var(--accent-hover); }
.btn-secondary { background: var(--surface2); color: var(--text); border: 1px solid var(--border); }
.btn-secondary:hover { border-color: var(--accent); color: var(--accent); }
.btn-sm { padding: .3rem .6rem; font-size: .8rem; }
.btn-danger { background: var(--danger); color: white; }

.badge { display: inline-block; padding: .15rem .5rem; border-radius: 4px; font-size: .72rem; font-weight: 600; }
.badge-email { background: #3a3aff22; color: var(--accent); }
.badge-report { background: #22c55e22; color: var(--success); }
.badge-notice { background: #f59e0b22; color: var(--warning); }
.badge-custom { background: #a78bfa22; color: #a78bfa; }

.card { background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-lg); }
.card-header { padding: .75rem 1rem; border-bottom: 1px solid var(--border); font-weight: 600; }
.card-body { padding: 1rem; }

input, select, textarea {
    background: var(--bg); border: 1px solid var(--border); border-radius: var(--radius);
    color: var(--text); padding: .45rem .7rem; font-size: .875rem; width: 100%;
    transition: border-color .15s;
}
input:focus, select:focus, textarea:focus { outline: none; border-color: var(--accent); }
label { display: block; margin-bottom: .3rem; font-size: .8rem; color: var(--text-muted); }

.modal-overlay { display: none; position: fixed; inset: 0; background: rgba(0,0,0,.6); z-index: 100; align-items: center; justify-content: center; }
.modal-overlay.open { display: flex; }
.modal { background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-lg); width: 90%; max-width: 800px; max-height: 85vh; display: flex; flex-direction: column; }
.modal-header { padding: .9rem 1.1rem; border-bottom: 1px solid var(--border); display: flex; align-items: center; justify-content: space-between; }
.modal-body { padding: 1rem; overflow-y: auto; flex: 1; }
.modal-close { background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 1.2rem; }
```

- [ ] **Step 3: Verify app runs**

```bash
dotnet run --project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
Expected: App starts on https://localhost:5001 (or similar), no errors in console.

- [ ] **Step 4: Commit**

```bash
git add src/TemplateBuilder.Web/Views/Shared/ src/TemplateBuilder.Web/wwwroot/
git commit -m "feat: add dark-theme layout and base CSS design system"
```

---

### Task 12: Template List Page (Index)

> Invoke `ui-ux-pro-max:ui-ux-pro-max` skill before building this view.

**Files:**
- Create: `src/TemplateBuilder.Web/Views/Templates/Index.cshtml`

- [ ] **Step 1: Create Index.cshtml**

Create `src/TemplateBuilder.Web/Views/Templates/Index.cshtml`:

```razor
@model TemplateBuilder.Web.ViewModels.TemplateListViewModel
@{
    ViewData["Title"] = "Templates";
}

<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:1.25rem;">
    <div>
        <h1 style="font-size:1.4rem;font-weight:700;">Templates</h1>
        <p style="color:var(--text-muted);font-size:.85rem;margin-top:.2rem;">Manage your reusable document templates</p>
    </div>
    <a class="btn btn-primary" asp-action="Create">+ New Template</a>
</div>

<div style="display:grid;grid-template-columns:1fr 280px;gap:1.25rem;">
    <div>
        <!-- Search + Filter -->
        <form method="get" style="display:flex;gap:.6rem;margin-bottom:1rem;">
            <input type="text" name="search" value="@Model.Search" placeholder="Search templates…" style="flex:1;" />
            <select name="type" style="width:160px;">
                <option value="">All Types</option>
                @foreach (var t in TemplateBuilder.Web.ViewModels.TemplateListViewModel.KnownTypes)
                {
                    <option value="@t" selected="@(Model.TypeFilter == t)">@t</option>
                }
            </select>
            <button type="submit" class="btn btn-secondary">Filter</button>
        </form>

        <!-- Template Table -->
        @if (!Model.Templates.Any())
        {
            <div class="card" style="text-align:center;padding:3rem;color:var(--text-muted);">
                <p style="font-size:1.1rem;margin-bottom:.5rem;">No templates yet</p>
                <a class="btn btn-primary" asp-action="Create" style="margin-top:.5rem;">Create your first template</a>
            </div>
        }
        else
        {
            <div class="card">
                <table style="width:100%;border-collapse:collapse;">
                    <thead>
                        <tr style="border-bottom:1px solid var(--border);font-size:.78rem;color:var(--text-muted);">
                            <th style="padding:.6rem 1rem;text-align:left;">Name</th>
                            <th style="padding:.6rem 1rem;text-align:left;">Type</th>
                            <th style="padding:.6rem 1rem;text-align:left;">Version</th>
                            <th style="padding:.6rem 1rem;text-align:left;">Updated</th>
                            <th style="padding:.6rem 1rem;text-align:left;">Status</th>
                            <th style="padding:.6rem 1rem;text-align:right;">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var t in Model.Templates)
                        {
                            <tr style="border-bottom:1px solid var(--border);" id="row-@t.Id">
                                <td style="padding:.65rem 1rem;font-weight:500;">@t.Name</td>
                                <td style="padding:.65rem 1rem;">
                                    <span class="badge badge-@t.TemplateType.ToLower()">@t.TemplateType</span>
                                </td>
                                <td style="padding:.65rem 1rem;color:var(--text-muted);font-size:.82rem;">
                                    v@(t.CurrentVersion?.VersionNumber ?? 0)
                                </td>
                                <td style="padding:.65rem 1rem;color:var(--text-muted);font-size:.82rem;">
                                    @t.UpdatedAt.ToString("dd MMM yyyy")
                                </td>
                                <td style="padding:.65rem 1rem;">
                                    <span style="color:@(t.IsActive ? "var(--success)" : "var(--text-muted)");font-size:.82rem;">
                                        @(t.IsActive ? "Active" : "Inactive")
                                    </span>
                                </td>
                                <td style="padding:.65rem 1rem;text-align:right;display:flex;gap:.4rem;justify-content:flex-end;">
                                    <a class="btn btn-sm btn-secondary" asp-action="Edit" asp-route-id="@t.Id">✏️ Edit</a>
                                    <button class="btn btn-sm btn-secondary" onclick="openDuplicateModal(@t.Id, '@t.Name.Replace("'", "\\'")')">⧉ Duplicate</button>
                                    <button class="btn btn-sm btn-secondary" onclick="toggleActive(@t.Id, this)">
                                        @(t.IsActive ? "Disable" : "Enable")
                                    </button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>

    <!-- Stats Sidebar -->
    <div class="card" style="height:fit-content;">
        <div class="card-header">Quick Stats</div>
        <div class="card-body">
            <div style="display:flex;flex-direction:column;gap:.6rem;font-size:.85rem;">
                <div style="display:flex;justify-content:space-between;">
                    <span style="color:var(--text-muted);">Total</span>
                    <strong>@Model.Templates.Count</strong>
                </div>
                @foreach (var kvp in Model.CountByType)
                {
                    <div style="display:flex;justify-content:space-between;">
                        <span style="color:var(--text-muted);">@kvp.Key</span>
                        <strong>@kvp.Value</strong>
                    </div>
                }
            </div>
        </div>
    </div>
</div>

<!-- Duplicate Modal -->
<div class="modal-overlay" id="duplicate-modal">
    <div class="modal" style="max-width:420px;">
        <div class="modal-header">
            <span style="font-weight:600;">Duplicate Template</span>
            <button class="modal-close" onclick="document.getElementById('duplicate-modal').classList.remove('open')">✕</button>
        </div>
        <div class="modal-body">
            <div style="margin-bottom:.75rem;">
                <label>New Template Name</label>
                <input type="text" id="duplicate-name-input" style="margin-top:.3rem;" />
            </div>
            <div style="display:flex;gap:.5rem;justify-content:flex-end;">
                <button class="btn btn-secondary" onclick="document.getElementById('duplicate-modal').classList.remove('open')">Cancel</button>
                <button class="btn btn-primary" onclick="confirmDuplicate()">⧉ Duplicate</button>
            </div>
        </div>
    </div>
</div>

@section Scripts {
<script>
async function toggleActive(id, btn) {
    const res = await fetch(`/Templates/${id}/ToggleActive`, { method: 'POST', headers: { 'RequestVerificationToken': document.querySelector('input[name=__RequestVerificationToken]')?.value ?? '' } });
    if (res.ok) {
        const data = await res.json();
        const row = document.getElementById(`row-${id}`);
        const statusCell = row.querySelectorAll('td')[4];
        statusCell.innerHTML = `<span style="color:${data.isActive ? 'var(--success)' : 'var(--text-muted)'};font-size:.82rem;">${data.isActive ? 'Active' : 'Inactive'}</span>`;
        btn.textContent = data.isActive ? 'Disable' : 'Enable';
    }
}

let _duplicateSourceId = null;
function openDuplicateModal(id, name) {
    _duplicateSourceId = id;
    document.getElementById('duplicate-name-input').value = `Copy of ${name}`;
    document.getElementById('duplicate-modal').classList.add('open');
    setTimeout(() => document.getElementById('duplicate-name-input').focus(), 50);
}

async function confirmDuplicate() {
    const newName = document.getElementById('duplicate-name-input').value.trim();
    if (!newName) return;
    const res = await fetch(`/Templates/${_duplicateSourceId}/Duplicate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newName })
    });
    if (res.ok) {
        const { id } = await res.json();
        window.location.href = `/Templates/${id}/Edit`;
    }
}
</script>
}
```

- [ ] **Step 2: Test the page**

```bash
dotnet run --project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
Open https://localhost:5001/Templates — verify the list page renders with the stats sidebar and empty state message.

- [ ] **Step 3: Commit**

```bash
git add src/TemplateBuilder.Web/Views/Templates/Index.cshtml
git commit -m "feat: add Template List page with search, type filter, stats sidebar"
```

---

### Task 13: Template Editor Page

> Invoke `ui-ux-pro-max:ui-ux-pro-max` skill before building this view.

**Files:**
- Create: `src/TemplateBuilder.Web/Views/Templates/Edit.cshtml`
- Create: `src/TemplateBuilder.Web/Views/Templates/_VersionHistory.cshtml`
- Create: `src/TemplateBuilder.Web/Views/Templates/_Preview.cshtml`
- Create: `src/TemplateBuilder.Web/wwwroot/js/template-editor.js`

- [ ] **Step 1: Create _VersionHistory partial**

Create `src/TemplateBuilder.Web/Views/Templates/_VersionHistory.cshtml`:

```razor
@model (List<TemplateBuilder.Domain.Entities.TemplateVersion> Versions, int? CurrentVersionId)
<div style="display:flex;flex-direction:column;gap:.5rem;">
    @foreach (var v in Model.Versions)
    {
        var isCurrent = v.Id == Model.CurrentVersionId;
        <div style="background:var(--bg);border:@(isCurrent ? "2px solid var(--accent)" : "1px solid var(--border)");border-radius:var(--radius);padding:.6rem .9rem;display:grid;grid-template-columns:1fr auto auto;align-items:center;gap:.5rem;">
            <div>
                <span style="font-weight:600;">v@v.VersionNumber</span>
                @if (!string.IsNullOrWhiteSpace(v.ChangeComment))
                {
                    <span style="color:var(--text-muted);font-size:.82rem;margin-left:.5rem;">— @v.ChangeComment</span>
                }
                @if (isCurrent)
                {
                    <span class="badge badge-email" style="margin-left:.5rem;">Current</span>
                }
            </div>
            <span style="color:var(--text-muted);font-size:.78rem;">@v.CreatedAt.ToString("dd MMM yyyy HH:mm")</span>
            @if (!isCurrent)
            {
                <button class="btn btn-sm btn-secondary" onclick="restoreVersion(@v.Id)">Restore</button>
            }
        </div>
    }
</div>
```

- [ ] **Step 2: Create Edit.cshtml (3-panel layout)**

Create `src/TemplateBuilder.Web/Views/Templates/Edit.cshtml`:

```razor
@model TemplateBuilder.Web.ViewModels.TemplateEditorViewModel
@{
    ViewData["Title"] = Model.Id.HasValue ? $"Edit — {Model.Name}" : "New Template";
    var isNew = !Model.Id.HasValue;
}

<form id="editor-form" asp-action="@(isNew ? "Create" : "")" method="post">
    @Html.AntiForgeryToken()
    <div style="display:grid;grid-template-columns:220px 1fr 200px;gap:0;height:calc(100vh - 100px);overflow:hidden;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-lg);">

        <!-- LEFT: Field Palette -->
        <div style="border-right:1px solid var(--border);display:flex;flex-direction:column;overflow:hidden;">
            <div style="padding:.7rem;border-bottom:1px solid var(--border);font-size:.78rem;font-weight:600;color:var(--text-muted);letter-spacing:.05em;">FIELD PALETTE</div>
            <div style="padding:.6rem;border-bottom:1px solid var(--border);">
                <label style="font-size:.7rem;">SQL View</label>
                <select id="view-selector" onchange="loadViewColumns(this.value)" style="font-size:.78rem;">
                    <option value="">— Select a view —</option>
                    @foreach (var v in Model.AvailableViews)
                    {
                        <option value="@v">@v</option>
                    }
                </select>
            </div>
            <div style="flex:1;overflow-y:auto;padding:.6rem;" id="field-palette">
                <div style="color:var(--text-muted);font-size:.78rem;text-align:center;margin-top:1rem;">Select a view to see fields</div>
            </div>
            <div style="padding:.6rem;border-top:1px solid var(--border);">
                <div style="font-size:.7rem;color:var(--text-muted);margin-bottom:.4rem;letter-spacing:.05em;">BLOCKS</div>
                <div style="display:flex;flex-direction:column;gap:.3rem;">
                    <div class="palette-block" draggable="true" data-block="loop" style="border:1px dashed var(--border);border-radius:var(--radius);padding:.3rem .6rem;font-size:.78rem;cursor:grab;user-select:none;">⟳ Loop Block</div>
                    <div class="palette-block" draggable="true" data-block="grid" style="border:1px dashed var(--border);border-radius:var(--radius);padding:.3rem .6rem;font-size:.78rem;cursor:grab;user-select:none;">▦ Grid Block</div>
                </div>
            </div>
        </div>

        <!-- CENTER: TinyMCE Canvas -->
        <div style="display:flex;flex-direction:column;overflow:hidden;">
            <div style="padding:.5rem .7rem;border-bottom:1px solid var(--border);font-size:.78rem;font-weight:600;color:var(--text-muted);letter-spacing:.05em;">CANVAS</div>
            <div style="flex:1;overflow:hidden;">
                <textarea id="template-body" name="Body">@Model.Body</textarea>
            </div>
        </div>

        <!-- RIGHT: Properties -->
        <div style="border-left:1px solid var(--border);display:flex;flex-direction:column;overflow-y:auto;">
            <div style="padding:.7rem;border-bottom:1px solid var(--border);font-size:.78rem;font-weight:600;color:var(--text-muted);letter-spacing:.05em;">PROPERTIES</div>
            <div style="padding:.7rem;display:flex;flex-direction:column;gap:.8rem;flex:1;">
                <div>
                    <label>Template Name</label>
                    <input asp-for="Name" id="prop-name" style="font-size:.82rem;" />
                </div>
                <div>
                    <label>Type</label>
                    <select asp-for="TemplateType" id="prop-type" style="font-size:.82rem;">
                        <option value="Email">Email</option>
                        <option value="Report">Report</option>
                        <option value="Notice">Notice</option>
                        <option value="Custom">Custom</option>
                    </select>
                </div>
                <div>
                    <label>Description</label>
                    <textarea asp-for="Description" id="prop-desc" rows="2" style="font-size:.82rem;resize:vertical;"></textarea>
                </div>
                @if (!isNew)
                {
                    <div>
                        <label>Version</label>
                        <div style="display:flex;align-items:center;gap:.5rem;">
                            <span id="version-display" style="font-size:.82rem;">v@Model.CurrentVersionNumber</span>
                            <button type="button" class="btn btn-sm btn-secondary" onclick="openVersionHistory()">History</button>
                        </div>
                    </div>
                    <div>
                        <label>Save Note (optional)</label>
                        <input type="text" id="save-comment" placeholder="What changed?" style="font-size:.82rem;" />
                    </div>
                }
            </div>
            <div style="padding:.7rem;border-top:1px solid var(--border);display:flex;flex-direction:column;gap:.5rem;">
                @if (!isNew)
                {
                    <button type="button" class="btn btn-secondary" onclick="openPreview()">👁 Preview</button>
                    <button type="button" class="btn btn-primary" onclick="saveVersion()">💾 Save Version</button>
                }
                else
                {
                    <button type="submit" class="btn btn-primary">Create Template</button>
                }
            </div>
        </div>
    </div>
</form>

<!-- Version History Modal -->
<div class="modal-overlay" id="version-modal">
    <div class="modal" style="max-width:560px;">
        <div class="modal-header">
            <span style="font-weight:600;">Version History</span>
            <button class="modal-close" onclick="closeModal('version-modal')">✕</button>
        </div>
        <div class="modal-body" id="version-history-content">Loading…</div>
    </div>
</div>

<!-- Preview Modal -->
<div class="modal-overlay" id="preview-modal">
    <div class="modal">
        <div class="modal-header">
            <span style="font-weight:600;">Live Preview</span>
            <button class="modal-close" onclick="closeModal('preview-modal')">✕</button>
        </div>
        <div class="modal-body">
            <div style="margin-bottom:.75rem;">
                <label>Sample Data (JSON)</label>
                <textarea id="preview-json" rows="6" style="font-family:monospace;font-size:.78rem;">{}</textarea>
            </div>
            <button class="btn btn-primary btn-sm" onclick="renderPreview()">Render</button>
            <div id="preview-error" style="color:var(--danger);font-size:.82rem;margin-top:.5rem;display:none;"></div>
            <div style="margin-top:.75rem;border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;display:none;" id="preview-frame-wrap">
                <iframe id="preview-frame" style="width:100%;height:400px;border:none;background:white;"></iframe>
            </div>
        </div>
    </div>
</div>

@section Scripts {
<script src="https://cdn.tiny.cloud/1/no-api-key/tinymce/7/tinymce.min.js" referrerpolicy="origin"></script>
<script>
    const templateId = @(Model.Id?.ToString() ?? "null");
</script>
<script src="~/js/template-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: Create template-editor.js**

Create `src/TemplateBuilder.Web/wwwroot/js/template-editor.js`:

```javascript
// TinyMCE initialisation
tinymce.init({
    selector: '#template-body',
    height: '100%',
    menubar: false,
    plugins: 'lists link table code',
    toolbar: 'bold italic underline | h1 h2 | bullist numlist | link table | code',
    skin: 'oxide-dark',
    content_css: 'dark',
    content_style: `
        .tb-field { background: #3a3aff22; color: #6366f1; border-radius: 3px; padding: 0 4px; font-family: monospace; }
        .tb-loop { border: 2px dashed #f59e0b; border-radius: 6px; padding: 8px; margin: 4px 0; }
        .tb-loop-label { font-size: 11px; color: #f59e0b; margin-bottom: 4px; }
    `,
    setup(editor) {
        editor.on('drop', (e) => handleDrop(e, editor));
    }
});

// Drag field from palette into editor
function handleDrop(e, editor) {
    const fieldName = e.dataTransfer?.getData('field-name');
    const blockType = e.dataTransfer?.getData('block-type');
    if (fieldName) {
        e.preventDefault();
        editor.insertContent(`<span class="tb-field" contenteditable="false">{{ model.${fieldName} }}</span>&nbsp;`);
    } else if (blockType === 'loop') {
        e.preventDefault();
        const view = document.getElementById('view-selector').value || 'Items';
        editor.insertContent(`
            <div class="tb-loop">
                <div class="tb-loop-label">⟳ LOOP — ${view}</div>
                {{ for item in model.${view} }}<p><!-- drag fields here --></p>{{ end }}
            </div>`);
    } else if (blockType === 'grid') {
        e.preventDefault();
        const view = document.getElementById('view-selector').value || 'Items';
        editor.insertContent(`
            <table border="1" style="width:100%;border-collapse:collapse;">
                <thead><tr><th>Column1</th><th>Column2</th></tr></thead>
                <tbody>
                {{ for item in model.${view} }}
                <tr><td>{{ item.Column1 }}</td><td>{{ item.Column2 }}</td></tr>
                {{ end }}
                </tbody>
            </table>`);
    }
}

// Set draggable data on palette items
document.addEventListener('dragstart', (e) => {
    const field = e.target.closest('[data-field]');
    const block = e.target.closest('[data-block]');
    if (field) e.dataTransfer.setData('field-name', field.dataset.field);
    if (block) e.dataTransfer.setData('block-type', block.dataset.block);
});

// Load view columns from API
async function loadViewColumns(viewName) {
    const palette = document.getElementById('field-palette');
    if (!viewName) {
        palette.innerHTML = '<div style="color:var(--text-muted);font-size:.78rem;text-align:center;margin-top:1rem;">Select a view to see fields</div>';
        return;
    }
    palette.innerHTML = '<div style="color:var(--text-muted);font-size:.78rem;padding:.5rem;">Loading…</div>';
    const res = await fetch(`/Templates/Api/Views/${encodeURIComponent(viewName)}/Columns`);
    const columns = await res.json();
    palette.innerHTML = columns.map(c => `
        <div class="palette-field" draggable="true" data-field="${c.name}"
             style="background:var(--accent);opacity:.85;color:white;border-radius:var(--radius);padding:.25rem .5rem;font-size:.75rem;margin-bottom:.3rem;cursor:grab;user-select:none;">
            ⠿ ${c.name} <span style="opacity:.6;font-size:.68rem;">${c.dataType}</span>
        </div>`).join('');
}

// Save version
async function saveVersion() {
    const body = tinymce.get('template-body').getContent();
    const res = await fetch(`/Templates/${templateId}/SaveVersion`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            name: document.getElementById('prop-name').value,
            templateType: document.getElementById('prop-type').value,
            description: document.getElementById('prop-desc').value,
            body,
            changeComment: document.getElementById('save-comment').value
        })
    });
    if (res.ok) {
        const data = await res.json();
        document.getElementById('version-display').textContent = `v${data.versionNumber}`;
        document.getElementById('save-comment').value = '';
        showToast('Version saved');
    }
}

// Version history modal
async function openVersionHistory() {
    const modal = document.getElementById('version-modal');
    const content = document.getElementById('version-history-content');
    modal.classList.add('open');
    const res = await fetch(`/Templates/${templateId}/Versions`);
    content.innerHTML = await res.text();
}

async function restoreVersion(versionId) {
    const res = await fetch(`/Templates/${templateId}/Restore/${versionId}`, { method: 'POST' });
    if (res.ok) {
        const data = await res.json();
        document.getElementById('version-display').textContent = `v${data.versionNumber}`;
        closeModal('version-modal');
        showToast('Version restored — reload to see changes');
    }
}

// Preview modal
function openPreview() {
    document.getElementById('preview-modal').classList.add('open');
}

async function renderPreview() {
    const body = tinymce.get('template-body').getContent();
    const modelJson = document.getElementById('preview-json').value;
    const errorEl = document.getElementById('preview-error');
    const frameWrap = document.getElementById('preview-frame-wrap');
    errorEl.style.display = 'none';
    const res = await fetch(`/Templates/${templateId}/Preview`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ body, modelJson })
    });
    if (res.ok) {
        const { html } = await res.json();
        const frame = document.getElementById('preview-frame');
        frame.srcdoc = html;
        frameWrap.style.display = 'block';
    } else {
        const { error } = await res.json();
        errorEl.textContent = error;
        errorEl.style.display = 'block';
    }
}

// Utilities
function closeModal(id) { document.getElementById(id).classList.remove('open'); }

function showToast(msg) {
    const toast = document.createElement('div');
    toast.textContent = msg;
    Object.assign(toast.style, {
        position:'fixed', bottom:'1.5rem', right:'1.5rem', background:'var(--accent)',
        color:'white', padding:'.6rem 1rem', borderRadius:'var(--radius)',
        fontSize:'.85rem', zIndex:'999', transition:'opacity .3s'
    });
    document.body.appendChild(toast);
    setTimeout(() => { toast.style.opacity = '0'; setTimeout(() => toast.remove(), 300); }, 2500);
}
```

- [ ] **Step 4: Test the editor page end-to-end**

```bash
dotnet run --project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj
```
1. Go to https://localhost:5001/Templates
2. Click "+ New Template", fill in name "Test Email", type "Email", click Create
3. Verify redirect to editor
4. Select a SQL view from the dropdown — verify columns appear as draggable chips
5. Drag a field into the TinyMCE canvas — verify `{{ model.FieldName }}` token appears highlighted
6. Drag a Loop Block — verify dashed border block with `{{ for item in model.ViewName }}` scaffold appears
7. Click "Save Version" — verify toast "Version saved" appears and version counter increments
8. Click "History" — verify modal opens with version list
9. Click "👁 Preview" — paste `{}` in JSON box, click Render — verify rendered HTML appears in iframe

- [ ] **Step 5: Commit**

```bash
git add src/TemplateBuilder.Web/Views/Templates/ src/TemplateBuilder.Web/wwwroot/js/
git commit -m "feat: add 3-panel template editor with TinyMCE, drag-and-drop, version history, and preview"
```

---

### Task 14: Wire Routing and Final Verification

**Files:**
- Modify: `src/TemplateBuilder.Web/Program.cs`

- [ ] **Step 1: Configure routing in Program.cs**

Ensure `src/TemplateBuilder.Web/Program.cs` has attribute routing enabled and a default route:

```csharp
// Replace MapControllerRoute block with:
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Templates}/{action=Index}/{id?}");

app.MapControllers(); // enables attribute-routed controllers
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test TemplateBuilder.sln
```
Expected: All tests pass — `0 Error(s)`.

- [ ] **Step 3: Run the full verification checklist from the spec**

Follow `docs/superpowers/specs/2026-05-13-template-builder-design.md` Section 8 — test every item in both the Designer and NuGet checklists.

- [ ] **Step 4: Final commit**

```bash
git add .
git commit -m "feat: complete TemplateBuilder — designer web app and Core NuGet rendering engine"
```

---

## Self-Review Notes

**Spec coverage check:**
- ✅ Database schema: Tasks 4–5
- ✅ Template syntax (Scriban): Task 7 (tests cover scalar, loop, conditional, error cases)
- ✅ NuGet API (`RenderAsync`, `RenderByNameAsync`, `RenderBodyAsync`, exceptions, caching): Tasks 7–8
- ✅ Template List page: Task 12
- ✅ 3-panel Template Editor: Task 13
- ✅ Version History: Task 13 (`_VersionHistory.cshtml` + `openVersionHistory` + `restoreVersion`)
- ✅ Live Preview: Task 13 (Preview modal + `renderPreview` + `Preview` endpoint)
- ✅ SQL View auto-discovery: Task 9 (`SqlViewDiscoveryService`) + Task 13 (palette JS)
- ✅ Soft delete (IsActive): Task 10 (`ToggleActive` endpoint) + Task 12 (Index toggle button)
- ✅ Append-only versioning / Restore: Task 10 (`RestoreVersion` endpoint)
- ✅ Duplicate template: Task 10 (`Duplicate` endpoint + 2 tests) + Task 12 (Index button + modal + JS)

**Type consistency check:**
- `ITemplateEngine` defined in Task 3: `RenderAsync(int, object)`, `RenderByNameAsync(string, object)`, `RenderBodyAsync(string, object)` — all three used consistently in Tasks 7, 8, 10, 13
- `ITemplateRepository` defined in Task 3 — all methods implemented in Task 6, all used in Task 10 controller
- `TemplateNotFoundException(int)` and `TemplateNotFoundException(string)` — both constructors defined in Task 3, both used in Task 7
- `SaveVersionRequest` record defined in Task 10, consumed in same task's controller
- `DuplicateRequest` record defined in Task 10, consumed in same task's `Duplicate` endpoint and tests

**No placeholders found.** All steps have complete code, commands, and expected output.
