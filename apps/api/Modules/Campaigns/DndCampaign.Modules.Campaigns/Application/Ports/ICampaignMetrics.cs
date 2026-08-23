namespace DndCampaign.Modules.Campaigns.Application.Ports;

internal interface ICampaignMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
