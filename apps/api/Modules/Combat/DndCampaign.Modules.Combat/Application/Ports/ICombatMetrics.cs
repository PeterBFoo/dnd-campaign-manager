namespace DndCampaign.Modules.Combat.Application.Ports;

internal interface ICombatMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
