using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DndCampaign.Modules.Missions.Api;

internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo.IsClass
        && !typeInfo.IsAbstract
        && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(typeInfo)
        && typeInfo.Name.EndsWith("Controller", StringComparison.Ordinal);
}
