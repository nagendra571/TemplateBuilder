# net8.0 + net10.0 Multi-Targeting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change all library and test `.csproj` files from single-TFM `net10.0` to dual-TFM `net8.0;net10.0` so the published NuGet package is consumable by both .NET 8 and .NET 10 projects, and bump the library version to 1.5.0.

**Architecture:** Every project swaps `<TargetFramework>` (singular) to `<TargetFrameworks>` (plural) with the value `net8.0;net10.0`. No C# source changes are required — all APIs and language features used are available in both runtimes. The `BundleInternalAssemblies` MSBuild target already uses `$(TargetFramework)` in its `PackagePath`, so it produces correct `lib/net8.0/` and `lib/net10.0/` assets automatically when packing.

**Tech Stack:** .NET 10 SDK (used for development), MSBuild multi-targeting, `dotnet build`, `dotnet test`, `dotnet pack`

## Global Constraints

- The one-character difference between `<TargetFramework>` (singular) and `<TargetFrameworks>` (plural) is load-bearing — MSBuild's multi-targeting is triggered by the plural form only.
- `TemplateBuilder.Web.csproj` is NOT multi-targeted — it is the dev harness and stays `net10.0`.
- No NuGet package version changes — all current versions support `net8.0`.
- No C# source file changes — zero `.cs` files are touched.
- Final version: `TemplateBuilder.Editor` version `1.5.0` (only the Editor csproj has a `<Version>` property).
- All `dotnet build` commands must be run from the repo root: `C:/Users/nchinnam/source/repos/TemplateBuilder`
- Tasks must be executed in order: Domain → Application → Infrastructure → Editor → Tests.

---

## File Map

| File | Change |
|---|---|
| `src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj` | `TargetFramework` → `TargetFrameworks net8.0;net10.0` |
| `src/TemplateBuilder.Application/TemplateBuilder.Application.csproj` | Same |
| `src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj` | Same |
| `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` | Same + version `1.4.5` → `1.5.0` |
| `tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj` | Same (no version — not packaged) |
| `tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj` | Same |
| `tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj` | Same |
| `tests/TemplateBuilder.Infrastructure.Tests/TemplateBuilder.Infrastructure.Tests.csproj` | **SKIP** — references `TemplateBuilder.Core` which does not exist in main branch; already broken |

---

## Task 1: Multi-target TemplateBuilder.Domain

**Files:**
- Modify: `src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj`

**Interfaces:**
- Produces: `TemplateBuilder.Domain` compiled for both `net8.0` and `net10.0`, output under `bin/Debug/net8.0/` and `bin/Debug/net10.0/`

- [ ] **Step 1: Verify the current build fails for net8.0**

  Run from repo root:
  ```bash
  dotnet build src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj -f net8.0
  ```
  Expected: Error — `The current .NET SDK does not support targeting .NET 8.0` or `NETSDK1045` — confirming net8.0 is not currently a target.

- [ ] **Step 2: Edit the csproj**

  In `src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```
  The full file should look like:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
      <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
      <IsPackable>false</IsPackable>
    </PropertyGroup>

  </Project>
  ```

- [ ] **Step 3: Verify both TFMs build successfully**

  ```bash
  dotnet build src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj -f net8.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

  ```bash
  dotnet build src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj -f net10.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/TemplateBuilder.Domain/TemplateBuilder.Domain.csproj
  git commit -m "feat: multi-target TemplateBuilder.Domain for net8.0 and net10.0"
  ```

---

## Task 2: Multi-target TemplateBuilder.Application

**Files:**
- Modify: `src/TemplateBuilder.Application/TemplateBuilder.Application.csproj`

**Interfaces:**
- Consumes: `TemplateBuilder.Domain` compiled for both TFMs (Task 1)
- Produces: `TemplateBuilder.Application` compiled for both `net8.0` and `net10.0`

- [ ] **Step 1: Verify the current build fails for net8.0**

  ```bash
  dotnet build src/TemplateBuilder.Application/TemplateBuilder.Application.csproj -f net8.0
  ```
  Expected: Error — TFM not targeted.

- [ ] **Step 2: Edit the csproj**

  In `src/TemplateBuilder.Application/TemplateBuilder.Application.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```
  The full file should look like:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\TemplateBuilder.Domain\TemplateBuilder.Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="HtmlSanitizer" Version="9.0.892" />
      <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.1" />
      <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="10.0.8" />
      <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.8" />
      <PackageReference Include="Scriban" Version="7.2.0" />
    </ItemGroup>

    <PropertyGroup>
      <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
      <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
      <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
        <_Parameter1>TemplateBuilder.Application.Tests</_Parameter1>
      </AssemblyAttribute>
    </ItemGroup>

  </Project>
  ```

- [ ] **Step 3: Verify both TFMs build successfully**

  ```bash
  dotnet build src/TemplateBuilder.Application/TemplateBuilder.Application.csproj -f net8.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

  ```bash
  dotnet build src/TemplateBuilder.Application/TemplateBuilder.Application.csproj -f net10.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/TemplateBuilder.Application/TemplateBuilder.Application.csproj
  git commit -m "feat: multi-target TemplateBuilder.Application for net8.0 and net10.0"
  ```

---

## Task 3: Multi-target TemplateBuilder.Infrastructure

**Files:**
- Modify: `src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj`

**Interfaces:**
- Consumes: `TemplateBuilder.Domain` for both TFMs (Task 1)
- Produces: `TemplateBuilder.Infrastructure` compiled for both `net8.0` and `net10.0`

- [ ] **Step 1: Verify the current build fails for net8.0**

  ```bash
  dotnet build src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj -f net8.0
  ```
  Expected: Error — TFM not targeted.

- [ ] **Step 2: Edit the csproj**

  In `src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```
  The full file should look like:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\TemplateBuilder.Domain\TemplateBuilder.Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.1" />
      <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
      <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.8" />
      <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.8" />
    </ItemGroup>

    <PropertyGroup>
      <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
      <IsPackable>false</IsPackable>
    </PropertyGroup>

  </Project>
  ```

- [ ] **Step 3: Verify both TFMs build successfully**

  ```bash
  dotnet build src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj -f net8.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

  ```bash
  dotnet build src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj -f net10.0
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/TemplateBuilder.Infrastructure/TemplateBuilder.Infrastructure.csproj
  git commit -m "feat: multi-target TemplateBuilder.Infrastructure for net8.0 and net10.0"
  ```

---

## Task 4: Multi-target TemplateBuilder.Editor and bump to v1.5.0

**Files:**
- Modify: `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj`

**Interfaces:**
- Consumes: All three internal projects compiled for both TFMs (Tasks 1–3)
- Produces: `TemplateBuilder.Editor` NuGet package with `lib/net8.0/` and `lib/net10.0/` asset folders, version `1.5.0`

- [ ] **Step 1: Verify the current build fails for net8.0**

  ```bash
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -f net8.0 -c Release
  ```
  Expected: Error — TFM not targeted.

- [ ] **Step 2: Edit the csproj — two changes**

  In `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj`:

  Change 1 — TFM (line ~3):
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  →
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```

  Change 2 — version (line ~8):
  ```xml
  <Version>1.4.5</Version>
  ```
  →
  ```xml
  <Version>1.5.0</Version>
  ```

  No other lines change. The `BundleInternalAssemblies` target and all `PackageReference` entries stay exactly as-is.

- [ ] **Step 3: Verify both TFMs build**

  ```bash
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -f net8.0 -c Release
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

  ```bash
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -f net10.0 -c Release
  ```
  Expected: `Build succeeded.` with `0 Error(s)`

- [ ] **Step 4: Full dual-TFM build (no -f flag — builds both at once)**

  ```bash
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded.` — you should see MSBuild output mentioning both `net8.0` and `net10.0` target passes.

- [ ] **Step 5: Pack and inspect NuGet contents**

  ```bash
  dotnet pack src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release -o ./nupkg
  ```
  Expected: `Successfully created package 'nupkg/TemplateBuilder.Editor.1.5.0.nupkg'`

  Inspect the package to verify both TFM folders are present:
  ```bash
  dotnet tool run NuGetPackageExplorer --nupkg ./nupkg/TemplateBuilder.Editor.1.5.0.nupkg
  ```
  Or use `Expand-Archive` on Windows to unzip the `.nupkg` and check the `lib/` folder manually:
  ```powershell
  Expand-Archive -Path ./nupkg/TemplateBuilder.Editor.1.5.0.nupkg -DestinationPath ./nupkg/extracted -Force
  Get-ChildItem ./nupkg/extracted/lib/
  ```
  Expected output:
  ```
  net8.0/
  net10.0/
  ```
  Each folder must contain: `TemplateBuilder.Editor.dll`, `TemplateBuilder.Domain.dll`, `TemplateBuilder.Application.dll`, `TemplateBuilder.Infrastructure.dll`

- [ ] **Step 6: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj
  git commit -m "feat: multi-target TemplateBuilder.Editor for net8.0 and net10.0, bump to v1.5.0"
  ```

---

## Task 5: Multi-target test projects and run full test suite

**Files:**
- Modify: `tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj`
- Modify: `tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj`
- Modify: `tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj`
- Skip: `tests/TemplateBuilder.Infrastructure.Tests/` — references `TemplateBuilder.Core` which does not exist in the main branch; leave untouched.

**Interfaces:**
- Consumes: All library projects compiled for both TFMs (Tasks 1–4)

- [ ] **Step 1: Edit TemplateBuilder.Domain.Tests.csproj**

  In `tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```

- [ ] **Step 2: Edit TemplateBuilder.Application.Tests.csproj**

  In `tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```

- [ ] **Step 3: Edit TemplateBuilder.Editor.Tests.csproj**

  In `tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj`, change:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  to:
  ```xml
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  ```

- [ ] **Step 4: Run Domain tests against net8.0**

  ```bash
  dotnet test tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj -f net8.0 --no-build -v n
  ```
  *(If first run, omit `--no-build`)*
  Expected: All tests pass. Zero failures.

- [ ] **Step 5: Run Domain tests against net10.0**

  ```bash
  dotnet test tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj -f net10.0 -v n
  ```
  Expected: All tests pass. Zero failures.

- [ ] **Step 6: Run Application tests against net8.0**

  ```bash
  dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj -f net8.0 -v n
  ```
  Expected: All tests pass. Zero failures.

- [ ] **Step 7: Run Application tests against net10.0**

  ```bash
  dotnet test tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj -f net10.0 -v n
  ```
  Expected: All tests pass. Zero failures.

- [ ] **Step 8: Run Editor tests against net8.0**

  ```bash
  dotnet test tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj -f net8.0 -v n
  ```
  Expected: All tests pass. Zero failures.

- [ ] **Step 9: Run Editor tests against net10.0**

  ```bash
  dotnet test tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj -f net10.0 -v n
  ```
  Expected: All tests pass. Zero failures.

- [ ] **Step 10: Commit**

  ```bash
  git add tests/TemplateBuilder.Domain.Tests/TemplateBuilder.Domain.Tests.csproj
  git add tests/TemplateBuilder.Application.Tests/TemplateBuilder.Application.Tests.csproj
  git add tests/TemplateBuilder.Editor.Tests/TemplateBuilder.Editor.Tests.csproj
  git commit -m "feat: multi-target test projects for net8.0 and net10.0"
  ```

---

## Final Verification Checklist

After all tasks complete:

- [ ] `dotnet build src/TemplateBuilder.Editor/ -c Release` completes with zero errors
- [ ] `./nupkg/extracted/lib/net8.0/` contains all 4 DLLs
- [ ] `./nupkg/extracted/lib/net10.0/` contains all 4 DLLs
- [ ] All tests pass on `net8.0`
- [ ] All tests pass on `net10.0`
- [ ] `TemplateBuilder.Editor` version in the `.nupkg` filename is `1.5.0`
- [ ] `TemplateBuilder.Web.csproj` still targets only `net10.0` (unchanged)
