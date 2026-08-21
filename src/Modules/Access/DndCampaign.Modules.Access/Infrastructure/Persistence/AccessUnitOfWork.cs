using System.Data;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class AccessUnitOfWork(AccessDbContext database) : IAccessUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await database.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var result = await operation(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            database.ChangeTracker.Clear();
            throw new ConcurrentOperationException(
                "The operation conflicted with another committed transaction.",
                exception);
        }
    }

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        || (exception.InnerException is not null && IsSerializationFailure(exception.InnerException));
}
