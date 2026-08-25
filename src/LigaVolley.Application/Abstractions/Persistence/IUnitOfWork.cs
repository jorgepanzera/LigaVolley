namespace LigaVolley.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IApplicationTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IApplicationTransaction>(NoOpApplicationTransaction.Instance);
}
public interface IApplicationTransaction:IAsyncDisposable { Task CommitAsync(CancellationToken cancellationToken=default); }
internal sealed class NoOpApplicationTransaction:IApplicationTransaction
{
 public static NoOpApplicationTransaction Instance { get; }=new();
 public Task CommitAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
 public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
