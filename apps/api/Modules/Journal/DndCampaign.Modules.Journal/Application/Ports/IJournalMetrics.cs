namespace DndCampaign.Modules.Journal.Application.Ports;

internal interface IJournalMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
