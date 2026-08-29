using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace DndCampaign.Modules.AdventureCatalog.Api;

internal sealed class InternalControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var type in parts.OfType<AssemblyPart>()
                     .SelectMany(part => part.Types)
                     .Where(type => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type)
                         && !type.IsAbstract
                         && type.Namespace?.StartsWith(
                             "DndCampaign.Modules.AdventureCatalog.Api",
                             StringComparison.Ordinal) == true))
        {
            if (!feature.Controllers.Contains(type))
            {
                feature.Controllers.Add(type);
            }
        }
    }
}
