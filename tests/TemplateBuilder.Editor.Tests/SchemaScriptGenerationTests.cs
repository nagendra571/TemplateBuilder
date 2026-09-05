using System;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TemplateBuilder.Infrastructure.Data;
using Xunit;

namespace TemplateBuilder.Editor.Tests;

// Golden-file gate for the DBA-provisioned-schema script.
//
// The committed script (src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.<version>.sql)
// is GENERATED from the EF Core migration chain, so there is a single source of truth: the
// migrations. This test regenerates the script in-process (via IMigrator — no live database
// connection is needed to script migrations) and compares it with the committed file — it fails
// whenever the migration chain drifts from the shipped script.
//
// Pinned to net10.0 only (#if NET10_0 below): the Editor package multi-targets net8.0/net10.0,
// and EF Core's migrations SQL generator changed its GO-batch formatting between v8 and v10 —
// not just the recorded ProductVersion (masked by Normalize below), but actual batch-separator
// placement. Rather than chase generator-formatting drift across major EF Core versions, the
// shipped script is canonically net10.0's output, and this gate only runs under net10.0 (it
// does not exist as a test at all under net8.0 — not a silent pass).
//
// Regeneration: run with TB_REGEN_SCHEMA=1 -f net10.0 (e.g.
// `TB_REGEN_SCHEMA=1 dotnet test ... -f net10.0`) — the test then rewrites the committed file
// instead of comparing. Commit the regenerated file, then run the test normally as the gate.
// IMPORTANT: when the package version bumps and the schema changes, update the filename below
// and the csproj pack entry to match.
#if NET10_0
public class SchemaScriptGenerationTests
{
    private static readonly string ScriptPath = Path.Combine(
        FindRepoRoot(),
        "src", "TemplateBuilder.Editor", "Scripts", "TemplateBuilder.schema.3.0.0.sql");

    private static string Generate()
    {
        var options = new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            // Never actually connects — IMigrator.GenerateScript only needs the provider and
            // the migrations model, not a live database.
            .UseSqlServer("Server=(script-gen-only);Database=ScriptGenOnly;Trusted_Connection=True;")
            .Options;

        using var context = new TemplateBuilderDbContext(options);
        var migrator = context.GetService<IMigrator>();
        return migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
    }

    [Fact]
    public void SchemaScript_matches_the_committed_file()
    {
        if (Environment.GetEnvironmentVariable("TB_REGEN_SCHEMA") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScriptPath)!);
            File.WriteAllText(ScriptPath, Generate());
            return;
        }

        var committed = File.ReadAllText(ScriptPath);
        Normalize(Generate()).Should().Be(Normalize(committed));
    }

    // The Editor package multi-targets net8.0/net10.0, each pulling a different EF Core patch
    // version — the migrations themselves are identical either way, but the recorded
    // __EFMigrationsHistory.ProductVersion literal (e.g. "8.0.30" vs "10.0.11") isn't. Masking
    // it keeps this test a real drift gate on the actual schema (tables/columns/indexes)
    // regardless of which target framework happens to run it.
    private static string Normalize(string script) =>
        Regex.Replace(script, @"N'\d+\.\d+\.\d+'", "N'<version>'");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TemplateBuilder.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repo root from " + AppContext.BaseDirectory);
    }
}
#endif
