namespace TemplateBuilder.Editor.Models;

public record GenerateSampleDataRequest(string? ViewName, string? TemplateBody);

public record SaveSampleDataRequest(string? SampleData);
