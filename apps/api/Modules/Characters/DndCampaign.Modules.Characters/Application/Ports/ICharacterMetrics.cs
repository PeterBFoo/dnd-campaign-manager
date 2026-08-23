namespace DndCampaign.Modules.Characters.Application.Ports;

internal interface ICharacterMetrics
{
    void OperationCompleted(string operation, string outcome, double elapsedMilliseconds);
}
