namespace TemplateBuilder.Application.DTOs;

public record SqlColumnInfo(string Name, string DataType, int? MaxLength = null, bool IsNullable = false);
