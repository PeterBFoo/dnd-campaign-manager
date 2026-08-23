namespace DndCampaign.Modules.Missions.Application.Ports;

internal interface IMissionMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
