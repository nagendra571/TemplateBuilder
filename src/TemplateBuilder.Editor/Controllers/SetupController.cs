using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TemplateBuilder.Editor.Models;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Editor.Controllers;

/// <summary>
/// Development-only diagnostic page. Returns 404 in any non-Development environment.
/// Navigate to /Templates/_setup to verify your integration.
/// </summary>
public class SetupController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly TemplateBuilderDbContext _db;
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly IOptions<MvcOptions> _mvcOptions;

    public SetupController(
        IWebHostEnvironment env,
        TemplateBuilderDbContext db,
        IActionDescriptorCollectionProvider actions,
        IOptions<MvcOptions> mvcOptions)
    {
        _env = env;
        _db = db;
        _actions = actions;
        _mvcOptions = mvcOptions;
    }

    [HttpGet("Templates/_setup")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var checks = new List<SetupCheckResult>();

        // ── 1. Database connection ────────────────────────────────────────────
        bool dbOk = false;
        string? dbDetail = null;
        try { dbOk = await _db.Database.CanConnectAsync(ct); }
        catch (Exception ex) { dbDetail = ex.Message; }

        checks.Add(new SetupCheckResult(
            "Database connection",
            "SQL Server is reachable with the configured connection string.",
            dbOk,
            "Verify the ConnectionString passed to AddTemplateBuilderEditor() in Program.cs.",
            dbDetail));

        // ── 2. Migrations applied ─────────────────────────────────────────────
        bool migrOk = false;
        string? migrDetail = null;
        try
        {
            var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
            migrOk = pending.Count == 0;
            if (pending.Count > 0)
                migrDetail = "Pending: " + string.Join(", ", pending);
        }
        catch (Exception ex) { migrDetail = ex.Message; }

        checks.Add(new SetupCheckResult(
            "Migrations applied",
            "All EF Core migrations have been applied to the database.",
            migrOk,
            "Run 'dotnet ef database update', or wait for MigrationHostedService to complete on next startup.",
            migrDetail));

        // ── 3. app.MapControllers() ───────────────────────────────────────────
        bool mapControllersOk = _actions.ActionDescriptors.Items
            .Any(a => a.AttributeRouteInfo?.Template != null &&
                      a.AttributeRouteInfo.Template.Contains("{id:int}") &&
                      a.AttributeRouteInfo.Template.Contains("Edit"));

        checks.Add(new SetupCheckResult(
            "app.MapControllers() registered",
            "Attribute-routed endpoints (Edit, Preview, SaveVersion, Versions) are reachable.",
            mapControllersOk,
            "Add app.MapControllers() before app.MapControllerRoute(...) in Program.cs."));

        // ── 4. SuppressImplicitRequired ───────────────────────────────────────
        bool suppressOk = _mvcOptions.Value.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes;

        checks.Add(new SetupCheckResult(
            "SuppressImplicitRequiredAttributeForNonNullableReferenceTypes",
            "Prevents ASP.NET Core from rejecting an empty template body on Create.",
            suppressOk,
            "In AddControllersWithViews(o => { o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true; });"));

        // ── 5. Static assets accessible ───────────────────────────────────────
        bool assetsOk = false;
        string? assetsDetail = null;
        try
        {
            var url = $"{Request.Scheme}://{Request.Host}/_content/TemplateBuilder.Editor/css/template-editor.css";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), ct);
            assetsOk = resp.IsSuccessStatusCode;
            if (!assetsOk) assetsDetail = $"HTTP {(int)resp.StatusCode} from {url}";
        }
        catch (Exception ex) { assetsDetail = ex.Message; }

        checks.Add(new SetupCheckResult(
            "Static assets serving (/_content/)",
            "The editor CSS and JS are accessible at /_content/TemplateBuilder.Editor/.",
            assetsOk,
            "Ensure app.UseStaticFiles() or app.MapStaticAssets() is called in Program.cs.",
            assetsDetail));

        return View("_Setup", checks);
    }

    /// <summary>
    /// Probe page: renders using the consumer's layout so the setup page
    /// can inspect whether @section Styles and @section Scripts are wired up.
    /// </summary>
    [HttpGet("Templates/_setup/layout-probe")]
    public IActionResult LayoutProbe()
    {
        if (!_env.IsDevelopment())
            return NotFound();
        return View("_LayoutProbe");
    }
}
