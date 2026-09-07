using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace TemplateBuilder.Editor.Tests;

// Source-guard tests for two bugs found and fixed during a QA-agent triage
// (docs/qa/2026-09-05-qa-agent-triage.md). Neither bug is reachable through the mocked
// controller unit tests elsewhere in this project (one is a Razor markup issue, the other a
// dangling JS reference), and this project has no Razor-rendering or WebApplicationFactory
// test host to exercise them end-to-end. These tests instead pin the exact source pattern
// that caused each bug, so a future edit can't silently reintroduce it.
public class SecurityRegressionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void IndexView_DoesNotSpliceTemplateNameIntoRawInlineHtml()
    {
        // Index.cshtml's "Duplicate" button used to build its onclick handler as
        // onclick="openDuplicateModal(@t.Id, '@Html.Raw(t.Name.Replace("'", "\\'"))')" —
        // Html.Raw skipped Razor's HTML encoding, so a Name containing a double quote broke
        // out of the (double-quoted) onclick attribute for stored XSS. The fix passes the
        // name through a data-* attribute instead, which Razor encodes like any other output.
        var path = Path.Combine(RepoRoot, "src", "TemplateBuilder.Editor", "Views", "Templates", "Index.cshtml");
        var source = File.ReadAllText(path);

        source.Should().NotContain("Html.Raw(t.Name",
            "Template Name must never be written into markup via Html.Raw — it bypasses HTML encoding and is user-controlled");
    }

    [Fact]
    public void TemplateEditorJs_DoesNotCallTheUndefinedErrMessageHelper()
    {
        // createTemplate()'s error branch used to call errMessage(err, 'Failed to create
        // template.') — a function that was never defined anywhere in this file (or the
        // project). Every failed Create request threw a silent ReferenceError, caught by the
        // bare `catch {}` below it, which replaced the real server validation message with a
        // generic "Network error — please try again." The fix inlines the same
        // `err?.message ?? fallback` pattern already used by saveVersion()/restoreVersion().
        var path = Path.Combine(RepoRoot, "src", "TemplateBuilder.Editor", "wwwroot", "js", "template-editor.js");
        var source = File.ReadAllText(path);

        source.Should().NotContain("errMessage(",
            "errMessage() does not exist in this file — calling it throws a ReferenceError that gets silently swallowed");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TemplateBuilder.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repo root from " + AppContext.BaseDirectory);
    }
}
