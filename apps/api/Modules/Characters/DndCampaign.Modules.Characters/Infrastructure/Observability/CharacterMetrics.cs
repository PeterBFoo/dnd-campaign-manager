using System.Diagnostics;
using System.Diagnostics.Metrics;
using DndCampaign.Modules.Characters.Application.Ports;

namespace DndCampaign.Modules.Characters.Infrastructure.Observability;

internal sealed class CharacterMetrics : ICharacterMetrics
{
    public const string MeterName = "DndCampaign.Modules.Characters";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("characters.operations");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("characters.operation.duration", "ms");

    public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "character.operation", operation },
            { "character.outcome", outcome },
        };
        Operations.Add(1, tags);
        Duration.Record(elapsedMilliseconds, tags);
    }
}
