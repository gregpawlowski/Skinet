using System.Linq;
using System.Reflection;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
  public class StoreContext : DbContext
  {
    public StoreContext(DbContextOptions<StoreContext> options) : base(options) {}

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductBrand> ProductBrands { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
      modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

      // This si for SQLite only, it doesn't support decimal, they will be converted to doubles and return doubles instead of decimal.
      if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
      {
        // Loop over all entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
          // get all properties of the entity that have a decimal propety type
          var properties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(decimal));

          // Loop over the propeties that have decimals
          // For each one set the conversion to double.
          foreach (var property in properties)
          {
            modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion<double>();
          }
        }
      }
    }
  }
}