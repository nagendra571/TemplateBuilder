namespace TemplateBuilder.Domain.Exceptions;

public class SchemaVersionMismatchException : Exception
{
    public SchemaVersionMismatchException(string requiredMigrationId)
        : base($"TemplateBuilder.Core requires DB migration '{requiredMigrationId}' which has not been applied. Run: dotnet ef database update --project TemplateBuilder.Infrastructure") { }
}
