namespace DndCampaign.Api.Application;

public interface ITransactionalBoundary
{
    Task ExecuteSerializableAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);

    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}
