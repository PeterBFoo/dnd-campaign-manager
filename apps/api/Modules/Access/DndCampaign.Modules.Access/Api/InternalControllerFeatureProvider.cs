using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DndCampaign.Modules.Access.Api;

internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        !typeInfo.IsPublic
        && typeInfo.IsClass
        && !typeInfo.IsAbstract
        && !typeInfo.ContainsGenericParameters
        && typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
        && typeInfo.IsDefined(typeof(ApiControllerAttribute), inherit: true);
}
