namespace DndCampaign.Modules.Access.Application.Abstractions.Messaging;

internal interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
