# Design: net8.0 + net10.0 Multi-Targeting

**Date:** 2026-06-23
**Version bump:** 1.4.5 → 1.5.0
**Status:** Approved

---

## Goal

Make `TemplateBuilder.Editor` consumable by .NET 8 (LTS) projects in addition to .NET 10 (LTS). The published NuGet package must contain both `lib/net8.0/` and `lib/net10.0/` asset folders.

## Audit Summary

### Status: Warning → Fix (csproj changes only)

All seven library and test projects currently use `<TargetFramework>net10.0</TargetFramework>` (singular). Changing to `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>` (plural) is the only required change.

### Package Compatibility

All six runtime NuGet dependencies are net8.0 compatible without version changes:

| Package | Version | net8.0 Compatible | Reason |
|---|---|---|---|
| HtmlSanitizer | 9.0.892 | ✅ | netstandard2.0 |
| Scriban | 7.2.0 | ✅ | netstandard2.1 |
| Microsoft.Data.SqlClient | 7.0.1 | ✅ | Explicit net8.0 TFM |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.8 | ✅ | EF Core 10 minimum is .NET 8 |
| Microsoft.Extensions.Caching.Memory | 10.0.8 | ✅ | Ships net8.0 + net9.0 TFMs |
| Microsoft.Extensions.Options | 10.0.8 | ✅ | Same |

No conditional `PackageReference` blocks. No package version changes.

### C# Language Features

No C# 13 or 14 exclusive features used. All constructs (`record`, `init`, `await using`, `using var`, `partial`) are available in C# 12, which is the default for net8.0. No `#if NET10_0` guards required.

### BundleInternalAssemblies MSBuild Target

The existing target uses `$(OutputPath)` and `lib\$(TargetFramework)\` in `PackagePath`. MSBuild expands these per-TFM during `dotnet pack`, producing:

```
lib/
  net8.0/
    TemplateBuilder.Editor.dll
    TemplateBuilder.Domain.dll
    TemplateBuilder.Application.dll
    TemplateBuilder.Infrastructure.dll
  net10.0/
    TemplateBuilder.Editor.dll
    TemplateBuilder.Domain.dll
    TemplateBuilder.Application.dll
    TemplateBuilder.Infrastructure.dll
```

No changes to this target are required.

### FrameworkReference

`<FrameworkReference Include="Microsoft.AspNetCore.App" />` resolves correctly for both net8.0 and net10.0. No change needed.

---

## Files Changed

| File | Change |
|---|---|
| `src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj` | `<TargetFramework>net10.0</TargetFramework>` → `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>` |
| `src/TemplateBuilder.Application/TemplateBuilder.Application.csproj` | Same |
| `src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj` | Same |
| `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` | Same + `<Version>1.4.5</Version>` → `<Version>1.5.0</Version>` |
| `tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj` | Same (no version bump — not packaged) |
| `tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj` | Same |
| `tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj` | Same |

**Not changed:**
- `src/TemplateBuilder.Web/TemplateBuilder.Web.csproj` — dev harness, stays `net10.0`
- No `.cs` files — zero source code changes

---

## No-Change Decisions

- **No `global.json`** — pinning SDK to .NET 10 is correct for development; consumers use their own SDK.
- **No conditional PackageReference** — all packages support net8.0 at their current versions.
- **No `#if` guards** — no net10.0-only APIs used.
- **No CI/CD** — out of scope for this change.

---

## Verification Commands

```bash
# Build both TFMs for the library:
dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release

# Run tests for each TFM:
dotnet test tests/TemplateBuilder.Domain.Tests/ -f net8.0
dotnet test tests/TemplateBuilder.Domain.Tests/ -f net10.0
dotnet test tests/TemplateBuilder.Application.Tests/ -f net8.0
dotnet test tests/TemplateBuilder.Application.Tests/ -f net10.0
dotnet test tests/TemplateBuilder.Editor.Tests/ -f net8.0
dotnet test tests/TemplateBuilder.Editor.Tests/ -f net10.0

# Pack and inspect contents:
dotnet pack src/TemplateBuilder.Editor/ -c Release -o ./nupkg
```

---

## Version

`1.4.5` → `1.5.0` — multi-framework support is a minor version bump (new capability, fully backward compatible for existing net10.0 consumers).
