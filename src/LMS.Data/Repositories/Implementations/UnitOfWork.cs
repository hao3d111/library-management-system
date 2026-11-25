using LMS.Data.Context;
using LMS.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace LMS.Data.Repositories.Implementations;

internal sealed class EfTransactionScope : IDbTransactionScope
{
    private readonly IDbContextTransaction _transaction;
    private bool _completed;

    public EfTransactionScope(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _transaction.CommitAsync(ct);
        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (!_completed)
        {
            await _transaction.RollbackAsync(ct);
            _completed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Ignore rollback exceptions on already aborted/closed connections
            }
        }
        await _transaction.DisposeAsync();
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly LibraryDbContext _context;

    public UnitOfWork(LibraryDbContext context)
    {
        _context = context;
    }

    public async Task<IDbTransactionScope> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _context.Database.BeginTransactionAsync(ct);
        return new EfTransactionScope(tx);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
