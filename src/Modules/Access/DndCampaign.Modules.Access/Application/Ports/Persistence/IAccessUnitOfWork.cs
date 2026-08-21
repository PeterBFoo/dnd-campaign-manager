namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface IAccessUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}

internal sealed class ConcurrentOperationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
