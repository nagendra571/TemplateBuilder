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
}
