namespace DndCampaign.Modules.Access.Application.Abstractions.Messaging;

internal interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
