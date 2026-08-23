using System.Diagnostics;
using System.Diagnostics.Metrics;
using DndCampaign.Modules.Combat.Application.Ports;

namespace DndCampaign.Modules.Combat.Infrastructure.Observability;

internal sealed class CombatMetrics : ICombatMetrics
{
    public const string MeterName = "DndCampaign.Modules.Combat";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("combat.operations");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("combat.operation.duration", "ms");

    public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "combat.operation", operation },
            { "combat.outcome", outcome },
        };
        Operations.Add(1, tags);
        Duration.Record(elapsedMilliseconds, tags);
    }
}
