using System;
using Microsoft.AspNetCore.Http;
using TemplateBuilder.Editor.Authorization;

namespace TemplateBuilder.Editor;

public class TemplateBuilderEditorOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public TemplateBuilderAuthorizationOptions Authorization { get; set; } = new();
    public Func<HttpContext, string?>? ActorResolver { get; set; }

    /// <summary>
    /// When <see langword="false"/>, the package never runs EF Core migrations at startup and
    /// never attempts DDL. Use this for DBA-managed databases: have a DBA run the schema script
    /// shipped in the package (<c>Scripts/TemplateBuilder.schema.&lt;version&gt;.sql</c>), then
    /// the app's SQL login only needs DML rights.
    /// </summary>
    public bool ApplyMigrations { get; set; } = true;
}
