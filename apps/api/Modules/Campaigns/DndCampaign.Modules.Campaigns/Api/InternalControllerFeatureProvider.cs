using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DndCampaign.Modules.Campaigns.Api;

internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        !typeInfo.IsAbstract
        && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(typeInfo)
        && typeInfo.Name.EndsWith("Controller", StringComparison.Ordinal);
}
