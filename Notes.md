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

# Generics & Specification Pattern
We could have a single repository that can be used with a lot of entities.
It can have a bad reputation becuase different entities will have different patterns, but the way to get aroudn this is to use the "Specification" pattern.

Generics
* Been around since 2002 (C# 2.0)
* Help avoid duplicate code
* Give us type safety, most of the time we use them rather than creating them

Creating a generic repo example:
T has to be a class of BaseEntity or derive from BaseEntity
where can be a class, method
```C#
public interface IGenericRepository<T> where T: BaseEntity
{
  Task<T> GetByIdAsync(int id);
  Task<IReadOnlyList<T>> ListAllAsync();
  ...
}
```

## Creating a generic Repo
We cannot use include in generic repos or return different types like pagination right now. 
We can use Set<T> to get the entity set.

```C#
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
  public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
  {
    private readonly StoreContext _context;
    public GenericRepository(StoreContext context)
    {
      _context = context;
    }

    public async Task<T> GetByIdAsync(int id)
    {
      return await _context.Set<T>().FindAsync(id);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync()
    {
      return await _context.Set<T>().ToListAsync();
    }
  }
}
```

## Specification Pattern
This strategy will help us deal with some of the downfalls of using a generic repository

* Describes the query in an object (Instead of passing in an expression)
* Returns an IQueryable<T>
* Generic List method takes specification as paramter instead of expression
* Specification can have a meaningful name
* * ProductsWithTypesAndBrandsSpecification

The cde looks like this:

Specification (all products with name red in name returns an IQueryable)
Then we pass that to the generic repo ListAsync(specification)

## Setting up Specification Pattern

```C#
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Core.Specifications
{
  public interface ISpecification<T>
  {
    Expression<Func<T, bool>> Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
  }
}
```

Base specification Class
```C#
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Core.Specifications
{
  public class BaseSpecification<T> : ISpecification<T>
  {
    public BaseSpecification(Expression<Func<T, bool>> criteria)
    {
      Criteria = criteria;
    }

    public Expression<Func<T, bool>> Criteria { get; }

    // The list of includes will be a list of expressions that take a type and object.
    public List<Expression<Func<T, object>>> Includes { get;} = new List<Expression<Func<T, object>>>();

    // This method will be used to add ot the list of includes.
    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }
  }
}
```

## Creating a Specification Evaluator
This will take in a specification object (List of inclues and the criteria), eveluate them and generate the IQueryable.

Evaluator will be part of the infrastructure project.
```C#
using System.Linq;
using Core.Entities;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class SpecificationEvaluator<TEntity> where TEntity : BaseEntity
    {
        public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> inputQuery, ISpecification<TEntity> spec)
        {
            var query = inputQuery;

            // If we have a criteria in the specification then add it to the query.
            if (spec.Criteria != null)
            {
                query = query.Where(spec.Criteria);
            }

            // Go over all the includes, aggregate them into include expressions.
            query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

            return query;
        }
    }
}
```

## Adding Repository Methods for Specification Queries
We create a method to Apply Specification which just calls the Evaluator using the curernt entity and passes in the specification.

THen the new methods will simply apply specification first then run firstOrDefaultAsync or toListAsync().

```C#
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
  public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
  {
    private readonly StoreContext _context;
    public GenericRepository(StoreContext context)
    {
      _context = context;
    }

    public async Task<T> GetByIdAsync(int id)
    {
      return await _context.Set<T>().FindAsync(id);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync()
    {
      return await _context.Set<T>().ToListAsync();
    }

    public async Task<T> GetEntityWithSpec(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).ToListAsync();
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
      return SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);
    }
  }
}
```

## Creating a Specification to include items
We cretae two constructores one for all items and one for a specific item.

```C#
using System;
using System.Linq.Expressions;
using Core.Entities;

namespace Core.Specifications
{
  public class ProductsWithTypesAndBrandsSpecification : BaseSpecification<Product>
  {
    public ProductsWithTypesAndBrandsSpecification()
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
    }

    public ProductsWithTypesAndBrandsSpecification(int id) : base(x => x.Id == id)
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
    }
  }
}
```

## Making use of the Specifications in Controllers
```C#
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
      var spec = new ProductsWithTypesAndBrandsSpecification(id);

      return await _productRepo.GetEntityWithSpec(spec);
    }
```

# Shaping Data
We'll use Dto's to return object that will be sent back.
Dto's will be held in API.

## Creating Dto and using it in controller
```C#
namespace API.Dtos
{
    public class ProductToReturnDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUrl { get; set; }
        public string ProductType { get; set; }
        public string ProductBrand { get; set; }
    }
}
```

```C#
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductToReturnDto>> GetProduct(int id)
    {
      var spec = new ProductsWithTypesAndBrandsSpecification(id);

      var product = await _productRepo.GetEntityWithSpec(spec);

      return new ProductToReturnDto{
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        PictureUrl = product.PictureUrl,
        ProductBrand = product.ProductBrand.Name,
        ProductType = product.ProductType.Name
      };
    }
```

And Mapping a List
```C#

    [HttpGet]
    public async Task<ActionResult<List<ProductToReturnDto>>> GetProducts()
    {
      var spec = new ProductsWithTypesAndBrandsSpecification();

      var products = await _productRepo.ListAsync(spec);

      return products.Select(product => new ProductToReturnDto
      {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        PictureUrl = product.PictureUrl,
        ProductBrand = product.ProductBrand.Name,
        ProductType = product.ProductType.Name
      }).ToList();
    }
```

## Using AutoMapper to take care of mapping.
Install in API project
    <PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="7.0.0"/>

### Create a Profile
```C#
using API.Dtos;
using AutoMapper;
using Core.Entities;

namespace API.Helpers
{
  public class MappingProfiles : Profile
  {
    public MappingProfiles()
    {
        CreateMap<Product, ProductToReturnDto>()
            .ForMember(d => d.ProductBrand, o => o.MapFrom(s => s.ProductBrand.Name))
            .ForMember(d => d.ProductType, o => o.MapFrom(s => s.ProductType.Name))
            .ForMember(d => d.PictureUrl, o => o.MapFrom<ProductUrlResolver>());
    }
  }
}
```
### Add it to Startup Class
```C#
services.AddAutoMapper(typeof(MappingProfiles));
```

### Creating a custom Resolver
We can inject services in the resolver constructor.
```C#
using API.Dtos;
using AutoMapper;
using Core.Entities;
using Microsoft.Extensions.Configuration;

namespace API.Helpers
{
  public class ProductUrlResolver : IValueResolver<Product, ProductToReturnDto, string>
  {
    private readonly IConfiguration _config;
    public ProductUrlResolver(IConfiguration config)
    {
      _config = config;
    }

    public string Resolve(Product source, ProductToReturnDto destination, string destMember, ResolutionContext context)
    {
        if (!string.IsNullOrEmpty(source.PictureUrl))
        {
            return _config["ApiUrl"] + source.PictureUrl;
        }

        return null;
    }
  }
}
```

# Serving Static Content From API
Any content inside wwwroot, will be looked in and served automatically.

Has to go after UseRouting();
If routes is not found then it will look in wwwroot.
```C#
      app.UseRouting();
      
      app.UseStaticFiles();
```