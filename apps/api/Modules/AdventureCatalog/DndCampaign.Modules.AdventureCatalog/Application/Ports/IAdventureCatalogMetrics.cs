namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal interface IAdventureCatalogMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
