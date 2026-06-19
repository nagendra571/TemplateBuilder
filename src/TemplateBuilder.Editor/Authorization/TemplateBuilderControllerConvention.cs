using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace TemplateBuilder.Editor.Authorization;

internal sealed class TemplateBuilderControllerConvention : IControllerModelConvention
{
    private readonly AuthorizeFilter _filter;

    public TemplateBuilderControllerConvention(string policyName)
        => _filter = new AuthorizeFilter(policyName);

    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Assembly == typeof(TemplateBuilderControllerConvention).Assembly)
            controller.Filters.Add(_filter);
    }
}
