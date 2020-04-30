# Create dotnet Project
This dotnet project will have three projects

API - Recieves and responds to HTTP requests
* depends on Infrastructure
Infrastructure - Queries database and sending queries to get data
* depneds on Core
Core - Holds business entities, doesn't depend on anything else.
* No dependencies
each one depends on the other from top to bottom.


## Create Solution and Projects
`dotnet new sln` <- No options will create solution using the naem of the containing folder.

Add WebAPI project
`dotnet new webapi -o API` Create API project in API folder

Add project to Solution:
`dotnet sln API/`

Add core project (Contains Domain Entities)
`dotnet new classlib -o Core`

Add Infrastructure Project
`dotnet new classlib -o Infrastructure`

Add both to the Solution:
`dotnet sln add Core/`
`dotnet sln add Infrastructure/`

Add References/Dependencies
API -> Infrastructure -> Core

Infrastructure will hold context among other things. Core will hold entities.

Add dependncy to API
`cd API`
`dotnet add reference ../Infrastructure`

Add dependency to Infrastructure
`cd Infrastructure`
`dotnet add reference ../Core/`


## Self signed certificates
By Defaut self signed certificates are installed when installing the SDK.
`dotnet dev-certs https`
A valid HTTPS certificate is already present.

Just becasue it's present doesn't mean your operating system trusts it.
`dotnet dev-certs https -h`

You can trust the certificate on your computer by running:
`dotnet dev-certs https -t`

`$ dotnet dev-certs https -t`
Trusting the HTTPS development certificate was requested. A confirmation prompt will be displayed if the certificate was not previously trusted. Click yes on the prompt to trust the certificate.
A valid HTTPS certificate is already present.

# Configuring EF

## Configuring DBContext
We need to install Entitiy Framework as a dependency to our project.

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="3.1.3"/>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="3.1.3"/>
```

The version must match the version of the sdk
You can get the version of the sdk by running `dotnet --version or dontet --info`

Now we can configure the DBContext
```C#
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
  public class StoreContext : DbContext
  {
    public StoreContext(DbContextOptions<StoreContext> options) : base(options) {}

    public DbSet<Product> Products { get; set; }
  }
}
```

And have to add it to Startup
```C#
services.AddDbContext<StoreContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("DefaultConnection")));
```

# Adding Migration
Install entity framework tool
`dotnet tool install --global dotnet-ef --version 3.1.201`

## Generate Migration
Add a new migration and put it in the Data/Migrations folder, by default it would jsut create a Migrations folder in root.
`dotnet ef migrations add "InitialCreate" -o Data/Migrations`

Create and update migration
`dotnet database update`

### Error for Design
Your startup project 'API' doesn't reference Microsoft.EntityFrameworkCore.Design. This package is required for the Entity Framework Core Tools to work. Ensure your startup project is correct, install the package, and try again.

This package is required for EF Core tools to work.
```XML
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="3.1.3"/>
```

## Updating Database
`dotnet ef database update` - update to latest migation if migration is not specified
* Will create datbase if not existing.


# Hiding bin and obj folders in Explorer
Preferences -> Settings -> Exclude add **/bin and **/obj

# API Architecture
The architecture used in this project


API:
Controllers
Routing requestes

Infrastructure:
Repository
DBContext
Services

Core:
Entities
Interfaces


## Repository Pattern
The project uses the Repository pattern.
repository sits beteween controller and DbContext.

Commonly used pattern in dotnet, decouples business code from teh data access code. We're already using EF to decouple data but this will make our controllers leaner and less messy.

It gives us a seperation of concern from controllers and DBContext

Minimizes duplicate query logic. If two controllers need to get a List of products they both contact the repository.

### Consequences
Increased level of abstraction
Increased maintainability, flexibility, testibility
More classes / interfaces - less duplicate code
Buisness logic further away from the data.
Harder to optimize certain operations against the data source.


## Adding Repository and Interface
Interface is created in Core, contains signature of the methods that will be in the concrete implementation.
Concrete implementation is in the Infrastructure project
Needs to be registered as a Scoped service in Startup. Then controllers can inject and make sure of repository to query database.


# Service Lifetimes
AddScoped - Created for the lifetime of the HTTP request
AddTransient - Lives for the lifetime of the method itself, very short lifetime.
AddSingleton - Created the first time when the app starts. Hold on to this until the application shuts down.

# Adding Base Entities
```C#
namespace Core.Entities
{
    public class BaseEntity
    {
        public int Id { get; set; }
    }
}
```

# Configuring Migrations
Can create a fluent config for each entity like so:
```C#
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
  public class ProductConfiguration : IEntityTypeConfiguration<Product>
  {
    public void Configure(EntityTypeBuilder<Product> builder)
    {
      builder.Property(p => p.Id).IsRequired();
      builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
      builder.Property(p => p.Description).IsRequired().HasMaxLength(180);
      builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
      builder.Property(p => p.PictureUrl).IsRequired();
      builder.HasOne(b => b.ProductBrand).WithMany()
        .HasForeignKey(p => p.ProductBrandId);
      builder.HasOne(t => t.ProductType).WithMany()
          .HasForeignKey(p => p.ProductTypeId);
    }
  }
}
```

And in the DbContext:
```C#
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
      modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
```

# Adding migrations on Statup
This is the Microsoft Recommended way of applying migrations, no need to run the command every time.

In Program.cs
```C#
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // Get access to data context, outside of the startup class.
            using(var scope = host.Services.CreateScope())
            {
                var Services = scope.ServiceProvider;
                var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
                
                // We're outisde of startup so we need to catch any exceptions.
                try
                {
                    var context = Services.GetRequiredService<StoreContext>();
                    await context.Database.MigrateAsync();

                }
                catch (Exception ex)
                {
                    // Create instance of the logger, need to specify the class we are logging against.
                    var logger = loggerFactory.CreateLogger<Program>();
                    logger.LogError(ex, "An error occured during migration");
                }
            }

            host.Run();
        }
```

# Seeding Data
Converting spreadsheet to JSON

csvjson.com

Create a new Seed for Store Context.
```C#
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Generic;
using Core.Entities;
using System;

namespace Infrastructure.Data
{
    public class StoreContextSeed
    {
        public static async Task SeedAsync(StoreContext context, ILoggerFactory loggerFactory)
        {
            try
            {
                if (!context.ProductBrands.Any())
                {
                    // Will be run from Program Class path.
                  var brandsData = File.ReadAllText("../Infrastructure/Data/SeedData/brands.json");

                  var brands = JsonSerializer.Deserialize<List<ProductBrand>>(brandsData);

                  foreach(var item in brands)
                  {
                      context.ProductBrands.Add(item);
                  }

                  await context.SaveChangesAsync();
                }
                
                if (!context.ProductTypes.Any())
                {
                    // Will be run from Program Class path.
                  var typesData = File.ReadAllText("../Infrastructure/Data/SeedData/types.json");

                  var types = JsonSerializer.Deserialize<List<ProductType>>(typesData);

                  foreach(var item in types)
                  {
                      context.ProductTypes.Add(item);
                  }

                  await context.SaveChangesAsync();
                }

                if (!context.ProductTypes.Any())
                {
                    // Will be run from Program Class path.
                  var productData = File.ReadAllText("../Infrastructure/Data/SeedData/products.json");

                  var products = JsonSerializer.Deserialize<List<ProductType>>(productData);

                  foreach(var item in products)
                  {
                      context.ProductTypes.Add(item);
                  }

                  await context.SaveChangesAsync();
                }
            } 
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger<StoreContextSeed>();

                logger.LogError(ex.Message);
            }
        }
    }
}
```

And in Program:
```C#
            using(var scope = host.Services.CreateScope())
            {
                var Services = scope.ServiceProvider;
                var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
                
                // We're outisde of startup so we need to catch any exceptions.
                try
                {
                    var context = Services.GetRequiredService<StoreContext>();
                    await context.Database.MigrateAsync();
                    await StoreContextSeed.SeedAsync(context, loggerFactory);

                }
                catch (Exception ex)
                {
                    // Create instance of the logger, need to specify the class we are logging against.
                    var logger = loggerFactory.CreateLogger<Program>();
                    logger.LogError(ex, "An error occured during migration");
                }
            }
```

