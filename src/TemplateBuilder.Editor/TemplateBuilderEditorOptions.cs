using TemplateBuilder.Editor.Authorization;

namespace TemplateBuilder.Editor;

public class TemplateBuilderEditorOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public TemplateBuilderAuthorizationOptions Authorization { get; set; } = new();
}
