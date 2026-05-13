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
