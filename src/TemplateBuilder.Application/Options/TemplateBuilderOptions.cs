namespace TemplateBuilder.Application.Options;

public class TemplateBuilderOptions
{
    /// <summary>
    /// Used exclusively by <c>AddTemplateBuilder</c> in the NuGet consumer path
    /// (<see cref="TemplateBuilder.Core.Extensions.ServiceCollectionExtensions"/>).
    /// The Web application's <c>Program.cs</c> passes the connection string directly as a
    /// constructor argument to services and does not read this property.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableCaching { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 30;
    public string ViewPrefix { get; set; } = "TemplateBuilder_";
    public IEnumerable<string>? ViewAllowlist { get; set; } = null;
    public bool StrictMode { get; set; } = false;
    public bool ValidateSchemaOnStartup { get; set; } = false;

    /// <summary>
    /// When <see langword="true"/>, template bodies can access model members both at the
    /// top level (<c>{{ Name }}</c>) and under the <c>model</c> wrapper (<c>{{ model.Name }}</c>).
    /// Default <see langword="false"/> preserves the documented <c>model.*</c>-only contract.
    /// </summary>
    /// <remarks>
    /// A model member whose name collides with a Scriban builtin function group
    /// (<c>date</c>, <c>string</c>, <c>math</c>, <c>html</c>, <c>array</c>, <c>object</c>,
    /// <c>for</c>, <c>if</c>, etc.) shadows that builtin at the top level when this option is
    /// enabled — <c>model.*</c> access is unaffected and always safe. Enable only if your
    /// templates and template authors can tolerate that tradeoff.
    /// </remarks>
    public bool AllowTopLevelModelAccess { get; set; } = false;
}
