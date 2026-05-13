namespace TemplateBuilder.Domain.Exceptions;

public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(int templateId)
        : base($"Template with ID {templateId} was not found or is inactive.") { }

    public TemplateNotFoundException(string templateName)
        : base($"Template '{templateName}' was not found or is inactive.") { }
}
