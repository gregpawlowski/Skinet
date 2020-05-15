using System;
using System.Collections;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Data
{
  public class UnitOfWork : IUnitOfWork
  {
    private readonly StoreContext _context;
    private Hashtable _repositories;
    public UnitOfWork(StoreContext context)
    {
      _context = context;
    }

    public async Task<int> Complete()
    {
      return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
      _context.Dispose();
    }

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
      if (_repositories == null) _repositories = new Hashtable();

      // Get the name of the entity we are looking for
      var type = typeof(TEntity).Name;

      // See if this entitie already exists
      if (!_repositories.ContainsKey(type))
      {
        // Entity doesn't exit we ahve to add it.
        var repositoryType = typeof(GenericRepository<>);
        // We'll create a generic repository instance, passing in the _context that we already injected in this class
        var repositryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity)), _context);

        _repositories.Add(type, repositryInstance);
      }

      return (IGenericRepository<TEntity>) _repositories[type];
    }
  }
}