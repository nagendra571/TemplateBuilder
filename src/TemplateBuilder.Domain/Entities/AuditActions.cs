namespace TemplateBuilder.Domain.Entities;

public static class AuditActions
{
    public const string Created = "created";
    public const string DraftSaved = "draft_saved";
    public const string Published = "published";
    public const string Restored = "restored";
    public const string Duplicated = "duplicated";
    public const string ToggledActive = "toggled_active";
    public const string Imported = "imported";
    public const string Deleted = "deleted";
    public const string SnippetCreated = "snippet_created";
    public const string SnippetDeleted = "snippet_deleted";
}
