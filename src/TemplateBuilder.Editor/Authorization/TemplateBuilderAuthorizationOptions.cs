namespace TemplateBuilder.Editor.Authorization;

public class TemplateBuilderAuthorizationOptions
{
    public TemplateBuilderAuthorizationMode Mode { get; set; } = TemplateBuilderAuthorizationMode.Anonymous;

    /// <summary>
    /// One or more roles allowed access. Required when <see cref="Mode"/> is
    /// <see cref="TemplateBuilderAuthorizationMode.Role"/>. A user in ANY of the listed roles is granted access.
    /// </summary>
    public string[]? RoleNames { get; set; }

    /// <summary>
    /// Advanced escape hatch. When set, the library applies this named policy (which the consumer
    /// must register themselves) instead of building one from <see cref="Mode"/> and <see cref="RoleName"/>.
    /// </summary>
    public string? PolicyName { get; set; }
}
