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

# Error Handling
* Creating a consistant error response
* Custome middleware
* Swagger

200 Range => Ok
300 Range => Redirection
400 Range => Client Side Error
500 Range => Server Error

## Error Controller for testing
```C#
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
  public class BuggyController : BaseApiController
  {
    private readonly StoreContext _context;
    public BuggyController(StoreContext context)
    {
      _context = context;
    }

    [HttpGet("notfound")]
    public ActionResult GetNotFoundRequest()
    {
        var thing = _context.Products.Find(42);

        if (thing == null)
            return NotFound();
        
        return Ok();
    }

    [HttpGet("servererror")]
    public ActionResult GetServerError()
    {
        var thing = _context.Products.Find(42);

        // Thing will be null so Null Exception will happen. Very common excepiton
        var thingToReturn = thing.ToString();

        return Ok();
    }

    [HttpGet("badrequest")]
    public ActionResult GetBadRequest()
    {
        return BadRequest();
    }

    [HttpGet("badrequest/{id}")]
    public ActionResult GetValidationError(int id)
    {
        return Ok();
    }

  }
}
```
## Making Consistent Error Responses 
When we return an error we'll pass to it a Api Response

```C#
using System;

namespace API.Errors
{
  public class ApiResponse
  {
    public ApiResponse(int statusCode, string message = null)
    {
      StatusCode = statusCode;
      Message = message ?? GetDefaultMessageForStatusCode(statusCode);
    }

    public int StatusCode { get; set; }
    public string Message { get; set; }

    private string GetDefaultMessageForStatusCode(int statusCode)
    {
      return statusCode switch
      {
          400 => "A bad request, you have made",
          401 => "Authorized, you are not",
          404 => "Resource found, it was not",
          500 => "Errors are the path to the dark side. Errors lead to anger. Anger leads to hate. Hate leads to career change",
          _ => null
      };
    }
  }
}
```

Now in a controller we can do the folowing:
```C#
       if (thing == null)
            return NotFound(new ApiResponse(404));
```

## Adding Not Found Endpoint Handler
We want  to return a similar reponse even if our controller isn't handling it.

Create controller to handle Errors. Our middleware will redirect the pipeline here for StatusCodes
```C#
using API.Errors;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("errors/{code}")]
    public class ErrorController : BaseApiController
    {
        public IActionResult Error(int code) 
        {
            return new ObjectResult(new ApiResponse(code));
        }
    }
}
```

And add the controller to the middleware
```C#
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }

      // WIll hit this middleware and will pass the status code.
      // Adds a StatusCodePages middleware to the pipeline. Specifies that the response body should be generated by re-executing the request pipeline using an alternate path. This path may contain a '{0}' placeholder of the status code.
      app.UseStatusCodePagesWithReExecute("/errors/{0}");
```

## Creating Exception Handling Middleware
We want to provide stack trace in dev mode but in production mode we want to provide a consistent error message to the user.

We need to extend the ApiResponse because if we are throwing an exception we might want to pass in the stack trace details.

```C#
namespace API.Errors
{
  public class ApiException : ApiResponse
  {
    public ApiException(int statusCode, string message = null, string details = null) : base(statusCode, message)
    {
        Details = details;
    }

    public string Details { get; set; }
  }
}
```

Now that we ahve a new ApiException class we can create the middleware:
```C#
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using API.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Middleware
{
  public class ExceptionMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
      _env = env;
      _logger = logger;
      _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = _env.IsDevelopment() 
                ? new ApiException((int)HttpStatusCode.InternalServerError, ex.Message, ex.StackTrace.ToString()) 
                : new ApiException((int)HttpStatusCode.InternalServerError);
            
            // Set it to camel case
            var options = new JsonSerializerOptions{PropertyNamingPolicy = JsonNamingPolicy.CamelCase};

            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
  }
}
```

and we can add it in our middleware pipeline:
```C#
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      // if (env.IsDevelopment())
      // {
      //   app.UseDeveloperExceptionPage();
      // }

      // Should be the first thing in our pipeline in case an excpetion is thrown early.
      app.UseMiddleware<ExceptionMiddleware>();

      // WIll hit this middleware and will pass the status code.
      app.UseStatusCodePagesWithReExecute("/errors/{0}");


      app.UseHttpsRedirection();

      app.UseRouting();
      
```

## Making Validatin Error Response Consistent
They are normally thrown when a user submits data. It's handled by the [ApiController] attribute.

We can ovverride the behavior of ApiController, In Startup Class:
```C#
      // This has to be bleow the AddControllers() becuase we are configuring the APi Behavior, controllers have to be loaded first.
      services.Configure<ApiBehaviorOptions>(options => {
        options.InvalidModelStateResponseFactory = actionContext => 
        {
          var errors = actionContext.ModelState
            .Where(e => e.Value.Errors.Count > 0)
            .SelectMany(x => x.Value.Errors)
            .Select(x => x.ErrorMessage).ToArray();

          var errorResponse = new ApiValidationErrorResponse{
            Errors = errors
          };

          return new BadRequestObjectResult(errorResponse);
        };
      });
```

```C#
using System.Collections.Generic;

namespace API.Errors
{
  public class ApiValidationErrorResponse : ApiResponse
  {
    public ApiValidationErrorResponse() : base(400)
    {
    }
    public IEnumerable<string> Errors { get; set; }
  }
}
```

# Adding Swagger for documenting the API
Need to add two packages:
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerGen" Version="5.4.1"/>
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="5.4.1"/>

In StartupClass:
```C#
      services.AddSwaggerGen(c => 
      {
        // Version of API
        c.SwaggerDoc("v1", new OpenApiInfo{ Title = "SkiNet API", Version ="v1"});
      });
```

Then Add it to the middleware right about UseEndpoints.
```C#
      app.UseSwagger();
      app.UseSwaggerUI(c => {c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkiNet API v1");});
```
We also have to exclude the ErrorController from the ApiControllers
```C#
   [ApiExplorerSettings(IgnoreApi = true)]
```

The ErrorController is not a normal controller, it is redirected to via UseStatusCodePagesWithReExecute() middleware. So we want to exclude it from our controllers, it should not have a direct route. Since it doesn't have an httpGet

## Improving Swagger
Give swagger hints in our methods:
```C#
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
```

# Cleaning Up Startup Class
You can move some of the code out of the startup class to make it easier to organize.
WE can extend the IService

## Creating Extension Methods:
```C#
using System.Linq;
using API.Errors;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions
{
    public static class ApplicationServicesExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IProductRepository, ProductRepository>();
            // Adding a scoped generic service for dependency injection
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // This has to be bleow the AddControllers() becuase we are configuring the APi Behavior, controllers have to be loaded first.
            services.Configure<ApiBehaviorOptions>(options => {
                options.InvalidModelStateResponseFactory = actionContext => 
                {
                var errors = actionContext.ModelState
                    .Where(e => e.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors)
                    .Select(x => x.ErrorMessage).ToArray();

                var errorResponse = new ApiValidationErrorResponse{
                    Errors = errors
                };

                return new BadRequestObjectResult(errorResponse);
                };
            });

            return services;
        }
    }
}
```

Now in Startup we can use the new AddApplicationServices
```C#
      // Use our extension method to add services.
      services.AddApplicationServices();
```

Same goes for Middlewre:
WE can extend the IApplicationBuilder and move our use statmetns to this extenion method.
```C#
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace API.Extensions
{
    public static class SwaggerServiceExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {

            services.AddSwaggerGen(c => 
            {
                // Version of API
                c.SwaggerDoc("v1", new OpenApiInfo{ Title = "SkiNet API", Version ="v1"});
            });
            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => {c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkiNet API v1");});

            return app;
        }
    }
}
```

and in Startup:
```C#
      app.UseSwaggerDocumentation();
```

# Base Controller
```C#
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BaseApiController : ControllerBase
    {
        
    }
}
```

# Paging, Filtering, Sorting, and Searching
Goal: 
To be able to implement sorting, searching and pagination functionality in a list using the Specification pattern.

## Defferred Execution
* Query commands are stored in a variable
* Execution of the query is deferred
* IQuerable<T> creates an exression tree
* Execusion:
ToList()
ToArray()
ToDictionary()
Count() or other Singleton queries.

## Adding a sorting specification class

Add new properties to the ISpecification
```C#
    Expression<Func<T, object>> OrderBy {get; }
    Expression<Func<T, object>> OrderByDescending {get; }
```

And Base Specification
```C#
    public Expression<Func<T, object>> OrderBy {get; private set;}
    public Expression<Func<T, object>> OrderByDescending {get; private set;}

    // New methods
    protected void AddOrderBy(Expression<Func<T, object>> orderByExpression)
    {
      OrderBy = orderByExpression;
    }
    protected void AddOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
    {
      OrderByDescending = orderByDescExpression;
    }
```

No we need to update the SpecificationEvaluator, if it sees an order by it has to add that on:
```C#
  if (spec.OrderBy != null)
  {
      query = query.OrderBy(spec.OrderBy); // p => p.ProductTypeId == id
  }
              // If we have an OrderBy then add it on to the query
  if (spec.OrderByDescending != null)
  {
      query = query.OrderByDescending(spec.OrderByDescending); // p => p.ProductTypeId == id
  }
```

Finally we can update the ProductsWithTypesAndBrandsSpecification:
Here we'll check which order by to use, if they add a priceDesc or priceAsc, we'll order by Name by default.
```C#
    public ProductsWithTypesAndBrandsSpecification(string sort)
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
      // Order by Name by default
      AddOrderBy(x => x.Name);

      if (!string.IsNullOrEmpty(sort))
      {
        switch (sort)
        {
          case "priceAsc":
            AddOrderBy(p => p.Price);
            break;
          case "priceDesc":
            AddOrderByDescending(p => p.Price);
            break;
          default:
            AddOrderBy(p => p.Name);
            break;
        }
      }
    }
```

## Adding Filtering functionality
We want to filter by the product Type and the product Brand. We already have a Criteria in our specification. We can use that.

In the controller we can pass a brandId and a TypeId
```C#
 public async Task<ActionResult<IReadOnlyList<ProductToReturnDto>>> GetProducts(string sort, int? brandId, int? typeId)
    {
      var spec = new ProductsWithTypesAndBrandsSpecification(sort, brandId, typeId);
```

Now we need to make use of it in our ProductsWithTypesAndBrandsSpecification
```C#
namespace Core.Specifications
{
  public class ProductsWithTypesAndBrandsSpecification : BaseSpecification<Product>
  {
    // Call into Base,base has a contstructor that takes criteria. We can use that constructor.
    // HEre we'll use a lambda expression but use || and && to build up the expression dependin on if a brandId and typeID is present.
    public ProductsWithTypesAndBrandsSpecification(string sort, int? brandId, int? typeId) :
    base(x => (!brandId.HasValue || x.ProductBrandId == brandId) && (!typeId.HasValue || x.ProductTypeId == typeId))
```

## Pagination
* Done for performance, you don't have to return all items at once.
* Parameters pass by query sting:
`/api/products?pageNumber=2&pageSize=5`
* Page size should be limited
* * We'll set our to 50 max
* Alway page results even if users don't send 

ISpecification
We'll need new propeties for Pagination

```C#
    int Take { get; }
    int Skip { get; }
    bool isPagingEnabled { get; }
```

Implementation in BaseSpecification.
Create the properties:
```C#
    public int Take {get; private set;}
    public int Skip {get; private set;}
    public bool isPagingEnabled {get; private set;}
```

We'll again create methods to add these properties:
```C#
    protected void ApplyPaging(int skip, int take)
    {
      Skip = skip;
      Take = take;
      isPagingEnabled = true;
    }
```

Now in the evaluator we have to evalue the spec:
Order matters here, we wantto page after filtering and ordering
```C#
  if (spec.isPagingEnabled)
  {
      query = query.Skip(spec.Skip).Take(spec.Take).;
  }
```

### Paging In Controller
Create new Class to Store Parameters
the controller is taking too many parameters, we can create a class to take care of that

```C#
namespace Core.Specifications
{
    public class ProductSpecParams
    {
        private const int MaxPageSize = 50;
        public int PageIndex {get; set;} = 1;
        private int _pageSize = 6;
        public int PageSize 
        {
            get  => _pageSize;
            // Only allow a maximum of MaxPageSize when setting this.
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
        public int? BrandId { get; set; }
        public int? TypeId { get; set; }
        public string sort { get; set; }
    }
}
```

Now in the controller:
We have to tell it the object will come from query otherwise it will look at the Body by default and throw an error.
```C#
    public async Task<ActionResult<IReadOnlyList<ProductToReturnDto>>> GetProducts([FromQuery]ProductSpecParams productSpecParams)
    {
      var spec = new ProductsWithTypesAndBrandsSpecification(productSpecParams);

      var products = await _productRepo.ListAsync(spec);

      return Ok(_mapper.Map<IReadOnlyList<ProductToReturnDto>>(products));
```

In the Specification we have to use our ApplyPaging method and fix all the variables now that we are passing the productSpecParam instead of direct values. 
```C# Specification
    public ProductsWithTypesAndBrandsSpecification(ProductSpecParams productSpecParams) :
    base(x => (!productSpecParams.BrandId.HasValue || x.ProductBrandId == productSpecParams.BrandId) 
      && (!productSpecParams.TypeId.HasValue || x.ProductTypeId == productSpecParams.TypeId))
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
      // Order by Name by default
      AddOrderBy(x => x.Name);
      // Apply paging, skip has to be the pagesize * the pageindex -1. The minus 1 is if we are on page 1 we dont' want to skip any.
      ApplyPaging(productSpecParams.PageSize * (productSpecParams.PageIndex - 1), productSpecParams.PageSize);

      if (!string.IsNullOrEmpty(productSpecParams.Sort))
      {
        switch (productSpecParams.Sort)
```

### Returning Pagination Infromation For Client
We can wrap the response in a pagination class that makes the info available for the client.

```C#
using System.Collections.Generic;

namespace API.Helpers
{
    public class Pagination<T> where T : class
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int Count { get; set; }
        public IReadOnlyList<T> Data { get; set; }
    }
}
```
We'll need a specification class just to apply filters and return a Count of total items
This specification will be very similar to the other one, excpetion it will not have the other criteria such as adding includes ordering and filtering.
For this one we just want to get all the items for the criteria specified.
```C#
using Core.Entities;

namespace Core.Specifications
{
  public class ProductsWithFiltersForCountSpecification : BaseSpecification<Product>
  {
    public ProductsWithFiltersForCountSpecification(ProductSpecParams productSpecParams)
    : base(x => (!productSpecParams.BrandId.HasValue || x.ProductBrandId == productSpecParams.BrandId) 
      && (!productSpecParams.TypeId.HasValue || x.ProductTypeId == productSpecParams.TypeId))
    {
        
    }
  }
}
```

We also have to add a new method to out Generic repository to return a Count:
```C# InterfaceGernericRepository
        Task<int> CountAsync(ISpecification<T> spec);
```
It will simply apply the specification and return a count
```C#
    public async Task<int> CountAsync(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).CountAsync();
    }
```
Now in the controller
In the controller we can get a count first,then get the items
Instead of return just the item we'll now wrap it in our Pagination class.
We'll return the count, the pageindex, pageSize and the data.
```C#
    [HttpGet]
    public async Task<ActionResult<Pagination<ProductToReturnDto>>> GetProducts([FromQuery]ProductSpecParams productSpecParams)
    {
      var spec = new ProductsWithTypesAndBrandsSpecification(productSpecParams);

      var countSpec = new ProductsWithFiltersForCountSpecification(productSpecParams);

      var totalItems = await _productRepo.CountAsync(countSpec);

      var products = await _productRepo.ListAsync(spec);
      
      var data = _mapper.Map<IReadOnlyList<ProductToReturnDto>>(products);

      return new Pagination<ProductToReturnDto>(productSpecParams.PageIndex, productSpecParams.PageSize, totalItems, data);
    }
```

## Adding search functionality
Search functinality will be just another criteria to evaluate

Add a new property to the Params
productSpecParams
```C#
private string _search;

public string Search { 
    get => _search;
    // make sure search term is always lower case.
    set => _search = value.ToLower();
  }
```
Add it to the filter in the SPecificaiton
```C#
      (string.IsNullOrEmpty(productSpecParams.Search) || x.Name.ToLower().Contains(productSpecParams.Search))
      && (!productSpecParams.BrandId.HasValue || x.ProductBrandId == productSpecParams.BrandId) 
      && (!productSpecParams.TypeId.HasValue || x.ProductTypeId == productSpecParams.TypeId))
```

IN this case the && will run each statemment either way. If the paramater is empty it will be true and it will move on to the next one.
If it's not empty then the first condion will be false and it will move on to the next one and return that which will return true.


## SQLite Conversion problem
This is just for SQLite becuase it doesn't support ordering on decimal.

In StoreContext onModelCreating
```C#
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
```

# Adding Cors
In Startup Class
```C#
      services.AddCors(opt => 
      {
        opt.AddPolicy("CorsPolicy", policy => 
        {
          policy.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:4200");
        });
      });
```

In middleware before Authoriztion()
```C#
app.UseCors("CorsPolicy");
```



# Setting Up Angular in the project

## Client
`ng new client`
Add Routing
SCSS for styles

## Setting UP Angular for HTTPS
We want to server Angular over HTTPS during development
CreditCard details cannot be put over http, browsers will give warning.

Easier to use HTTPS from the get-go

We'll need to get self signed certificates.
We can generate one with openSSL or some other tool.

Copy the crt and .key file into a new ssl folder created in the client.

Now add the certificate to the certificate store:
```
Windows 10

	1. Double click on the certificate (server.crt)
	2. Click on the button “Install Certificate …”
	3. Select whether you want to store it on user level or on machine level
	4. Click “Next”
	5. Select “Place all certificates in the following store”
	6. Click “Browse”
	7. Select “Trusted Root Certification Authorities”
	8. Click “Ok”
	9. Click “Next”
	10. Click “Finish”

If you get a prompt, click “Yes”
```

### Tell Angular to serve certificates:
angular.json in the serve section:
```json
        "serve": {
          "builder": "@angular-devkit/build-angular:dev-server",
          "options": {
            "browserTarget": "client:build",
            "sslKey": "ssl/server.key",
            "sslCert": "ssl/server.crt",
            "ssl": true
          },
```

## Adding Bootstrap to angular
the package to use for bootstrap is ngx-bootstap
`npm install ngx-bootstrap` or `ng add ngx-bootstrap`

ng add ngx-bootstrap will automatically do the following:
Add bootstrap angular.json styles
```json
          "styles": [
              "./node_modules/bootstrap/dist/css/bootstrap.min.css",
              "./node_modules/ngx-bootstrap/datepicker/bs-datepicker.css",
              "src/styles.scss"
            ],
```
Add BrowserAnimations to module


## Adding FontAwesome
`npm install font-awesome`
Stylesheet needs to be added manually.

# Good Angular VS Code Extensions
Angular Language Service
Prettier
BracketPairColorizer
TSLint


# Angular File and Folder Structure StyleGuide
App Module 

And:

Core Module = Has singletons, things that should only be used once. Should only be imported once in AppModule and never again.
The reason behind this is that we want everything that’s inside the core module to be a Singleton !!! And this is very important if you need your components/services to have only one instance. Some usage examples are the profile service or the header or footer components.


Shared Module = Imported in every feature module, has things that are shared between them. 
It’s recommended to avoid having services in the SharedModule because you will end up with a lot of instances of that service.
The SharedModule is the perfect place for importing and exporting back your UI Modules or components that are used a lot in your application. 


Feature Modules = Every feature of the application will have its own module and routing.
* Every feature module should have a root compoonent that is also named the same.

Common module gives us ngFOr etc...

# Angular: Creating an Error Component

Redirect users for 500 and 404 Errors


# Adding a Basket - API
* Adding Redis to the API
* Creating the Basket repository and controller

## Where to store the basket 
* Database - everytime use updates, save it in the db and keep it there. But the downside is that users can add items and never come back. they will be there and stay there until purged.
* Local Storage - Save it in the browser, downside is it's client side only. 
* Cookie - Not used much anymore, especially with an API.
* Redis - Redis is an in-memory data store. It's main purpose is to used for cacheing. Works on a key value pair storage.

* * In memory data structoure store
* * Supports stings, hashes, lists, sets etc..
* * key/value store.

Since it's stored in memory it's very very fast, but it takes snapshots and actually persists data. 
If it does restart, it will reload the data.
Data can be given a time to live, you can add an expire date, so that your data doesn't blow everything up.
Great for cacheing data


## Setting up Redis
Install Redis in Infrastructure project.
```xml
    <PackageReference Include="StackExchange.Redis" Version="2.1.39"/>
```

Add it to the Startup Class:
```C#
      services.AddSingleton<ConnectionMultiplexer>( c => {
        var configuration = ConfigurationOptions.Parse(_configuration.GetConnectionString("Redis"), true);
        return ConnectionMultiplexer.Connect(configuration);
      });
```
Redis acts as a Singleton. so we have to set it up as such
We'll also have to add a connection string in out appsettings:
```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=Skinet;Trusted_Connection=True;",
    "Redis": "localhost"
  },
```

We'll have to set up and run Redis Server on our computer.

## Setting up Basket class


## Installing Redis on Windows
Using Chocolatey

# Adding Identity
To implement ASP.NET identity to allow clents to login and register to our app and receive a JWT Token which can be used to authenticate against certain classes/methods in our API.

## Installing Identity Packages
Adding to API:
This package will be needed to handle authentication in the API.
```xml
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="3.1.3"/>
```

Infrastructure:
```xml
    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.2.0"/>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="3.1.3"/>
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="6.5.1"/>
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="6.5.1"/>
```

We do need this package in the core.
Normally we don't want dependencies in the core project bu 
Core:
```xml
<PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="3.1.3"/>
```

## Adding Identity Classes
We had to add Microsoft.Extensions.Identity.Stores so that our AppUser Entity can derie from IdentityUser class.
This class uses a string by default as the Id. It's a guid that is used as a string.

Identity will live in a seperate context from the last of the application.

By default identity user give a lot of properties including phone number, security stamp, passwordHash.
PasswordHash is salted and hashed mutliple times.

```C#
using Microsoft.AspNetCore.Identity;

namespace Core.Entities.Identity
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; }
        public Address Address { get; set; }
    }
}
```

And we set up an address class
```C#
namespace Core.Entities.Identity
{
  public class Address
  {
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string AppUserId { get; set; }
    public AppUser AppUser { get; set; }
  }
}
```

We tell entity framwork that appUsetId will be in the address class. So it will have a foreign field there to the appUser. It will be a one to one relationship.

## Adding Identity DBContext
IDentityDbContext brings in all the Dbsets for identity.
```C#
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity
{
  public class AppIdentityDbContext : IdentityDbContext<AppUser>
  {
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
  }
}
```

We have to add it to the startup:
```C#
      services.AddDbContext<AppIdentityDbContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("IdentityConnection")));
```
## Adding Migrations
Now we have two datbase contexts to work with.
We want to store our Identity MIgrations in a different folder
`dotnet ef migrations add IdentityInitial -p Infrastructure -s API -o Identity/Migrations -c AppIdentityDbContext`


## Seeding Data For Identity
```C#
using System.Linq;
using System.Threading.Tasks;
using Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity
{
    public class AddIdentityDbContextSeed
    {
        public static async Task SeedUserAsync(UserManager<AppUser> userManager)
        {
            if (!userManager.Users.Any()) 
            {
                var user = new AppUser
                {
                    DisplayName = "Bob",
                    Email = "bob@test.com",
                    UserName = "bob@test.com",
                    Address = new Address 
                    {
                        FirstName = "Bob",
                        LastName = "Bobity",
                        Street = "10Th The Street",
                        City = "New York",
                        State = "NY",
                        ZipCode = "90210"
                    }
                };

                await userManager.CreateAsync(user, "Pa$$w0rd");
            }
        }
    }
}
```

## Adding StartupServices For Identity
We'll add these as an extension method becuaes they can get quite large.

```C#
using Core.Entities.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions
{
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services)
        {
            var builder = services.AddIdentityCore<AppUser>();

            builder = new IdentityBuilder(builder.UserType, builder.Services);

            // This will allow userManager to work with Entity Framework.
            builder.AddEntityFrameworkStores<AppIdentityDbContext>();
            builder.AddSignInManager<SignInManager<AppUser>>();

            services.AddAuthentication();

            return services;
        }        
    }
}
```

Now we can use the extension method in Startup to add identity
```C# Startup
    // Use our extension method to add services.
      services.AddApplicationServices();

      // Add Identity
      services.AddIdentityServices();
```

We also have to add Authentication because signInManger relies on Authentication.


## Adding Identity Seed to Program Class
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
                    
                    // Seed and migrate identity context.
                    var userManager = Services.GetRequiredService<UserManager<AppUser>>();
                    var identityContext = Services.GetRequiredService<AppIdentityDbContext>();
                    await identityContext.Database.MigrateAsync();
                    await AppIdentityDbContextSeed.SeedUserAsync(userManager);

                }
                catch (Exception ex)
                {
                    // Create instance of the logger, need to specify the class we are logging against.
                    var logger = loggerFactory.CreateLogger<Program>();
                    logger.LogError(ex, "An error occured during migration");
                }
            }
```

## Adding Account Controller
This controller will manage users in our Identity System

```C#
using System.Threading.Tasks;
using API.Dtos;
using API.Errors;
using Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
  public class AccountController : BaseApiController
  {
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
      _signInManager = signInManager;
      _userManager = userManager;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
    {
        var user = await _userManager.FindByEmailAsync(loginDto.Email);

        if (user == null)
            return Unauthorized(new ApiResponse(401));
        
        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded)
            return Unauthorized(new ApiResponse(401));

        return new UserDto 
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            Token = "This will be a token"
        };
    }
  }
}
```

## Adding Register Method
```C#
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
    {
        var user = new AppUser {
            DisplayName = registerDto.DisplayName,
            Email = registerDto.Email,
            UserName = registerDto.Email
        };

        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
            return BadRequest(new ApiResponse(400));
        
        return new UserDto {
            DisplayName = user.DisplayName,
            Email = user.Email,
            Token = "This will be a token"
        };
    }
```

## Adding A Token Generator Service
We'll need to create a way to generate a token. We'll create it as a service.

This service will be in the infrastructure porject and we'll need an interface for it in the Core project.
It'll be an injectable service.
```C#
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Entities.Identity;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services
{
  public class TokenService : ITokenService
  {
    private readonly IConfiguration _config;

    // SymmetricSecurityKey is a key where a secret is used to encrypt/decrypt. Only one key is used.
    private readonly SymmetricSecurityKey _key;
    public TokenService(IConfiguration config)
    {
      _config = config;
      _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Token:Key"]));
    }

    public string createToken(AppUser user)
    {
        // Create a list of claims taht will be sent down inside the encrypeted toekn.
      var claims = new List<Claim>
      {
          new Claim(JwtRegisteredClaimNames.Email, user.Email),
          new Claim(JwtRegisteredClaimNames.GivenName, user.DisplayName)
      };

      var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

      var tokenDescriptor = new SecurityTokenDescriptor
      {
          Subject = new ClaimsIdentity(claims),
          Expires = DateTime.Now.AddDays(7),
          SigningCredentials = creds,
          Issuer = _config["Token:Issuer"]
      };

      var tokenHandler = new JwtSecurityTokenHandler();

      var token = tokenHandler.CreateToken(tokenDescriptor);

      return tokenHandler.WriteToken(token);
    }
  }
}
```

## Setting Identity to Use the Token
We'll set up the AddAuthentication so that it knows what to validate against.
We'll need to add 
```C#
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
        {
            var builder = services.AddIdentityCore<AppUser>();

            builder = new IdentityBuilder(builder.UserType, builder.Services);

            // This will allow userManager to work with Entity Framework.
            builder.AddEntityFrameworkStores<AppIdentityDbContext>();
            builder.AddSignInManager<SignInManager<AppUser>>();
            
            // Tell Authentication what type we're using and how it needs to validate the token.
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Tell the middleware what we want to validate against
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        // Tell what key we're using
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Token:Key"])),
                        ValidIssuer = config["Token:Issuer"],
                        ValidateIssuer = true
                    };
                });
            
            return services;
        }        
    }
```

And we need to useAuthentication middleware now that it's ready:
```C#
      app.UseAuthentication();

      app.UseAuthorization();
```

## Adding and using Token Service
Add the token service so that it's injectable:
```C#
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ITokenService, TokenService>();
```

Make use of the service:
In the methods we can now use the tokenService
```C#
      return new UserDto
      {
        DisplayName = user.DisplayName,
        Email = user.Email,
        Token = _tokenService.createToken(user)
      };
```

## Troubleshooting Authentication
Logging levels can be changed in appsettings

```C#
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
```

We can change the Microsoft one to Infromatioin, we'll see more info now.

`   Failed to validate the token.
Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException: IDX10208: Unable to validate audience. validationParameters.ValidAudience is null or whitespace and validationParameters.ValidAudiences is null.`

We configured identity and identity has some defaults. We didn't override ValidAudiance which is a default Validator and we're not using it. 

We can turn off ValidateAudicance for now in development.

```C#
ValidateAudience = false
```

## Adding more Account Methods


## Extending UserManager to Include Address
We can create some extensions to the userManger class to findUsers from principal.
```C#
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions
{
    public static class UserManagerExtensions
    {
        public static async Task<AppUser> FindUserByClaimsPrincipalWithAddressAsync(this UserManager<AppUser> input, ClaimsPrincipal user)
        {
            // Get he email from the Claims
            // We have access to HttpContext by nature of the ControllerBase
            var email = user?.Claims?.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;

            return await input.Users.Include(u => u.Address).SingleOrDefaultAsync(u => u.Email == email);
        }

        public static async Task<AppUser> FindByEmailFromClaimsPrincipal(this UserManager<AppUser> input, ClaimsPrincipal user)
        {
            // Get he email from the Claims
            // We have access to HttpContext by nature of the ControllerBase
            var email = user?.Claims?.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;

            return await input.Users.SingleOrDefaultAsync(u => u.Email == email);
        }
    }

}
```

Now in the Account Controller we can use the new methods to get user info for our routes/methods.
```C#
    [HttpGet("emailexists")]
    public async Task<ActionResult<bool>> CheckIfEmailExistsAsync([FromQuery] string email)
    {
      return await _userManager.FindByEmailAsync(email) != null;
    }

    [Authorize]
    [HttpGet("address")]
    public async Task<ActionResult<Address>> GetUserAddress()
    {
      var user = await _userManager.FindUserByClaimsPrincipalWithAddressAsync(HttpContext.User);

      return user.Address;
    }
```

## Avoiding Max Depth Problems
When you have navigation properties there becomes an issue with Json conversion becuase a user has a navigation to address but the address has a nvaigaiton to user and so on and so on. 

To address this we can create a Dto withou the naviation properties.

```C# AddressDto

```


# Validation on API
We need to be able to validate the information coming up from the client. 
Server will throw errors in cases where we send up a n empty password so we have to guard against that.
We want to confine our email to be an acutal email not just any string.
We also want to make sure the server validates the basket and doesn't add a quanitty or price of 0

## Model Validation
Model validation is done with Attributes in Dtos, we can add it to the entities but that would be putting more responsibility on the netities and a dependency. We would do this in the entity configuration in the Data / Config folder in the Infrastructure project anyway. It's better to do the validation at the level where the data is being passsed in by the user.

We already set up our error responses for modelstate error.

For the Address we can validate on the Dtos. Our Dto's are part of the API project.

```C#
using System.ComponentModel.DataAnnotations;

namespace API.Dtos
{
    public class AddressDto
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string Street { get; set; }
        [Required]
        public string City { get; set; }
        [Required]
        public string State { get; set; }
        [Required]
        public string ZipCode { get; set; }
    }
}
```

## Check for duplicate email addresses and passwords
```C#
using System.ComponentModel.DataAnnotations;

namespace API.Dtos
{
    public class RegisterDto
    {
        [Required]
        public string DisplayName { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [RegularExpression("(?=^.{6,10}$)(?=.*\\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[!@#$%^&amp;*()_+}{&quot;:;'?/&gt;.&lt;,])(?!.*\\s).*$")]
        public string Password { get; set; }
    }
}
```

We can check if email already exists when the user registers by using our existing method:
```C#
   [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
    {

      if(CheckIfEmailExistsAsync(registerDto.Email).Result.Value)
        return new BadRequestObjectResult(new ApiValidationErrorResponse{
          Errors = new [] {
            "Email address is in use"
          }
        });

      var user = new AppUser
      {
        DisplayName = registerDto.DisplayName,
        Email = registerDto.Email,
        UserName = registerDto.Email
      };

      var result = await _userManager.CreateAsync(user, registerDto.Password);

      if (!result.Succeeded)
        return BadRequest(new ApiResponse(400));

      return new UserDto
      {
        DisplayName = user.DisplayName,
        Email = user.Email,
        Token = _tokenService.createToken(user)
      };
    }
```


## Validating the basket
Make sure quanitty is at least one, price is not lower than 0.


## Updating Swagger Config for Identity
Swagger currently doesn't know how to authenticate, so we can tell it what kind of authentication we're using for our protected routes.

In Swagger Service Extensions:
```C#
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace API.Extensions
{
    public static class SwaggerServiceExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {

            services.AddSwaggerGen(c => 
            {
                // Version of API
                c.SwaggerDoc("v1", new OpenApiInfo{ Title = "SkiNet API", Version ="v1"});

                var securitySchema = new OpenApiSecurityScheme
                {
                    Description = "JWT Auth Bearer Scheme",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                };

                c.AddSecurityDefinition("Bearer", securitySchema);

                var securityRequirement = new OpenApiSecurityRequirement {{securitySchema, new [] { "Bearer"}}};

                c.AddSecurityRequirement(securityRequirement);


            });
            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => {c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkiNet API v1");});

            return app;
        }
    }
}
```

# Adding Auth to Client (Angular)

## Creating an Account Module

## Angular Forms Comparison
FormsModule (Templete Driven)
* Easy to use
* Good for simple scenarios
* Two way binding (uses ngModel)
* Minimal component code
* Automatic tracking by Angular
* Testing is hard

ReactiveFormsModule (ReactiveFromsModule)
* More flexible
* Good for any scenario
* Immutable data model
* MOre component code / less markup
* Reactive transformations (debounce etc is available)
* Testing is easier

Use Obeserable operators to return a new data model. Provides new state when input is changed.

Building blocks of forms
* FromControl
* * Value
* * Validation Status
* * User Interactions (dirty, touched)
* * Events

* FormGroup
* * Holds FormControls
* FormArray




# Creating a Resuable Form Component

## Reusable text input
```C#
import { Component, OnInit, ViewChild, ElementRef, Input, Self } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

@Component({
  selector: 'app-text-input',
  templateUrl: './text-input.component.html',
  styleUrls: ['./text-input.component.scss']
})
export class TextInputComponent implements OnInit, ControlValueAccessor {
  @ViewChild('input', {static: true}) input: ElementRef;
  @Input() type = 'text';
  @Input() label: string;

  // @Self tells angular dependency injection to look for this service in itself, normally it looks up the tree for something that matches.
  // NGControl is what formControls derive from.
  constructor(@Self() public controlDir: NgControl) {
    // Bind controlDir.valueAccessor to this.
    this.controlDir.valueAccessor = this;
  }

  ngOnInit(): void {
    const control = this.controlDir.control;

    // If we have validators use them or not set it to empyt array.
    const validators = control.validator ? [control.validator] : [];
    const asyncValidators = control.asyncValidator ? [control.asyncValidator] : [];

    control.setValidators(validators);
    control.setAsyncValidators(asyncValidators);

    control.updateValueAndValidity();
  }

  onChange(event) {}

  onTouched() {}

  writeValue(obj: any): void {
    this.input.nativeElement.value = obj || '';
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }
}

```

# Replay Subject
Behavior subject immediatly emits it's inital value.
Replay Subject is one that doesn't emit an inital value


# Great tool for Regualr Expresssions
http://regexlib.com/Search.aspx?k=password


# API - Orders

## Order Aggregate
The order will be a more complex entity.
We'll create a OrderAggregate folder and create several classes:

Address
This will be the address to ship to, not the customers address.
It will not have an ID, it will live in our order table we'll create later.

This entity will be owned by our Order it will not ahve it's own ID.
We give it a constructor so we can create one with the properties.
The parameter-less constructor is needed for entity framework.
```C#
namespace Core.Entities.OrderAggregate
{
    public class Address
    {
    public Address()
    {
    }
    public Address(string firstName, string lastName, string street, string city, string state, string zipCode)
    {
      FirstName = firstName;
      LastName = lastName;
      Street = street;
      City = city;
      State = state;
      ZipCode = zipCode;
    }

    public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
    }
}
```

DeliveryMethod
The delivery method will have an ID, it will derive from the BaseEntity.
This will be our available delivry methods for the customer to select.
```C#
namespace Core.Entities.OrderAggregate
{
    public class DeliveryMethod : BaseEntity
    {
        public string ShortName { get; set; }
        public string DeliveryTime { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
}
```

ProductItemOrdered
Snapshot of the item at the time the order was placed. This will be used in our OrderItem table. This will not have it's own ID, rather it will be passed into the OrderItem table.
```C#
namespace Core.Entities.OrderAggregate
{
    public class ProductItemOrdered
    {
    public ProductItemOrdered()
    {
    }
    public ProductItemOrdered(int productItemId, string productName, string pictureUrl)
    {
      ProductItemId = productItemId;
      ProductName = productName;
      PictureUrl = pictureUrl;
    }

    public int ProductItemId { get; set; }
        public string ProductName { get; set; }
        public string PictureUrl { get; set; }
    }
}
```

OrderItem
OrderItem will include the item ordered, it will derive from BaseEntity and it will have it's own ID.
```C#
namespace Core.Entities.OrderAggregate
{
    public class OrderItem : BaseEntity
    {
    public OrderItem()
    {
    }

    public OrderItem(ProductItemOrdered itemOrdered, decimal price, int quantity)
    {
      ItemOrdered = itemOrdered;
      Price = price;
      Quantity = quantity;
    }

    public ProductItemOrdered ItemOrdered { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
```

OrderStatus
This will be an enum that we'll use to get the status of the order. These are just flags.
```C#
using System.Runtime.Serialization;

namespace Core.Entities.OrderAggregate
{
    public enum OrderStatus
    {
        [EnumMember(Value = "Pending")]
        Pending,
        [EnumMember(Value = "Payment Received")]
        PaymentReceived,
        [EnumMember(Value = "Payment Failed")]
        PaymentFailed
    }
}
```

## Creating the Order Entity
Now that we have all those classes we can create the Order.
```C#
using System;
using System.Collections.Generic;

namespace Core.Entities.OrderAggregate
{
    public class Order : BaseEntity
    {
    public Order()
    {
    }

    public Order(IReadOnlyList<OrderItem> orderItems, string buyerEmail, Address shipToAddress, DeliveryMethod deliveryMethod, decimal subtotal)
    {
      BuyerEmail = buyerEmail;
      ShipToAddress = shipToAddress;
      DeliveryMethod = deliveryMethod;
      OrderItems = orderItems;
      Subtotal = subtotal;
    }

    // Orders will be pulled by BuyerEmial
    public string BuyerEmail { get; set; }
        // Ge the offset
        public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.Now;
        public Address ShipToAddress { get; set; }
        public DeliveryMethod DeliveryMethod { get; set; }
        public IReadOnlyList<OrderItem> OrderItems { get; set; }
        public decimal Subtotal { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string PaymentIntentId { get; set; }

        public decimal GetTotal()
        {
            return Subtotal + DeliveryMethod.Price;
        }
    }
}
```

## Configuring the Order Entity
Owned types cannot exist on their own. They cannot be created on their own or have thier own DbSets. They are only creted as part of another entity.

OrderConfiguration
We have to configure our Enum so that we get the nice status instead of a int.
We also configure the owned entity which is the ShipToAddress
And we add the One to Many relationship with the Order Items.
```C#
using System;
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
  public class OrderConfiguration : IEntityTypeConfiguration<Order>
  {
    public void Configure(EntityTypeBuilder<Order> builder)
    {
      builder.OwnsOne(o => o.ShipToAddress, a =>
      {
        a.WithOwner();
      });

      builder.Property(s => s.Status)
        //Convert our enum to a string.
        .HasConversion(o => o.ToString(), o => (OrderStatus)Enum.Parse(typeof(OrderStatus), o));

      // If we delete an order we want to delte any Order Items.
      builder.HasMany(o => o.OrderItems)
          .WithOne()
          .OnDelete(DeleteBehavior.Cascade);
    }
  }
}
```

```C#
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
  public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
  {
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.OwnsOne(i => i.ItemOrdered, io => io.WithOwner());

        builder.Property(i => i.Price)
            .HasColumnType("decimal(18,2)");
    
    }
  }
}
```

```C#
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
  public class DeliveryMethodConfiguration : IEntityTypeConfiguration<DeliveryMethod>
  {
    public void Configure(EntityTypeBuilder<DeliveryMethod> builder)
    {
      builder.Property(d => d.Price)
        .HasColumnType("decimal(18,2)");
    }
  }
}
```


## Store Context for DBSets and Seeding Data
We're only adding the ones that are going to be DbSets here. Not the owned properties.
```C# Store Context
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<DeliveryMethod> DeliveryMethods { get; set; }
```

We need to Seed the data for our delivery Methods.
```C# StoreContextSeed
        if (!context.DeliveryMethods.Any())
        {
          // Will be run from Program Class path.
          var dmData = File.ReadAllText("../Infrastructure/Data/SeedData/delivery.json");

          var deliveryMethods = JsonSerializer.Deserialize<List<DeliveryMethod>>(dmData);

          foreach (var method in deliveryMethods)
          {
            context.DeliveryMethods.Add(method);
          }

          await context.SaveChangesAsync();
        }
```

## Creating an Order Service
We want to keep the logic of this outside of the controllers. We don't want our controllers to become too big. The order process will need to use multiple repositories.

Create Service Interface in Core

```C# IOrderService
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Entities.OrderAggregate;

namespace Core.Interfaces
{
    public interface IOrderService
    {
         Task<Order> CreateOrderAsync(string buyerEmail, int deliveryMethod, string basketId, Address shippingAddress);
         
         Task<IReadOnlyList<Order>> GetOrdersForUserAsync(string buyerEmail);

         Task<Order> GetOrderByIdAsync(int id, string buyerEmail);

         Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync();
    }
}
```

Now we will be implementing it in the Infrastructure project:
The order service will have multiple methods to deal with our orders.
```C#
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities.OrderAggregate;
using Core.Interfaces;

namespace Infrastructure.Services
{
  public class OrderService : IOrderService
  {
    private readonly IBasketRepository _basketRepository;
    private readonly IProductRepository _productRepository;
    private readonly IGenericRepository<Order> _orderRepo;
    private readonly IGenericRepository<DeliveryMethod> _deliveryMethodRepo;

    public OrderService(
        IBasketRepository basketRepository,
        IProductRepository productRepository,
        IGenericRepository<Order> orderRepo,
        IGenericRepository<DeliveryMethod> deliveryMethodRepo
    )
    {
      _productRepository = productRepository;
      _orderRepo = orderRepo;
      _deliveryMethodRepo = deliveryMethodRepo;
      _basketRepository = basketRepository;
    }

    public async Task<Order> CreateOrderAsync(string buyerEmail, int deliveryMethodId, string basketId, Address shippingAddress)
    {
      // Get basket from repo.
      var basket = await _basketRepository.GetBasketAsync(basketId);

      // We can't trust the price in the basket, we'll have to take the price from the product.
      // Get items from product repo
      var items = new List<OrderItem>();

      foreach (var item in basket.Items)
      {
        // Get the prodcut from the repository
        var productItem = await _productRepository.GetProductByIdAsync(item.Id);

        // Create the snapshot of hte item at this give moment in time.
        var itemOrdered = new ProductItemOrdered(productItem.Id, productItem.Name, productItem.PictureUrl);

        // Create the order item, taking the price and quantity fro mthe proudct in the database not the basket.
        var orderItem = new OrderItem(itemOrdered, productItem.Price, item.Quantity);

        items.Add(orderItem);
      }

      // Get the delivery method from repo
      var deliveryMethod = await _deliveryMethodRepo.GetByIdAsync(deliveryMethodId);

      // Calculat subtotal
      var subtotal = items.Sum(item => item.Price * item.Quantity);

      // Create order
      var order = new Order(items, buyerEmail, shippingAddress, deliveryMethod, subtotal);

      // Save the Order


      // Return the order
      return order;
    }

    public Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync()
    {
      throw new System.NotImplementedException();
    }

    public Task<Order> GetOrderByIdAsync(int id, string buyerEmail)
    {
      throw new System.NotImplementedException();
    }

    public Task<IReadOnlyList<Order>> GetOrdersForUserAsync(string buyerEmail)
    {
      throw new System.NotImplementedException();
    }
  }
}
```

Add the service to startup:
```C#
namespace API.Extensions
{
    public static class ApplicationServicesExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IOrderService, OrderService>();
...
```



## Adding Controller for Orders
```C#
using System.Threading.Tasks;
using API.Dtos;
using API.Errors;
using API.Extensions;
using AutoMapper;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
  [Authorize]
  public class OrdersController : BaseApiController
  {
    private readonly IOrderService _orderService;
    private readonly IMapper _mapper;
    public OrdersController(IOrderService orderService, IMapper mapper)
    {
      _mapper = mapper;
      _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(OrderDto orderDto)
    {
        var email = HttpContext.User.RetrieveEmailFromPrincipal();

        var address = _mapper.Map<AddressDto, Address>(orderDto.ShipToAddress);

        var order = await _orderService.CreateOrderAsync(email, orderDto.DeliveryMethodId, orderDto.BasketId, address);

        if (order == null)
            return BadRequest(new ApiResponse(400, "Problem creating order"));
        
        return Ok(order);
    }
  }
}
```

# Unit of Work Pattern.
Currently:
Positives:
* We do not need to create additional repositories, we ahve a genric repository we can use for any entity. We will add more functionality to add.
* Specification pattern - Clear, flexible and allows us to query for our data in the means we want it and still use a generic repository.

Cons:
* Could end up with partial updates
* * Each repo has it's own verion of DbContext so when you save changes you can do it in one repo but not the other when saving changes
* Injecting three repos into the controller!
* Each repo creates it's own instance of dbContext

This is where Unit Of Work comes in.

Our Controller will have a Unit Of Work. Unit of work will create the DbContext and it will create the repositories as needed and each repository will share that same unit of work.

1. Unit of Work (UoW) Creates DBContext instance
2. Uniit of Work creates repositories as needed

Example 

```C#
UoW.Repository<Product>.Add(product)
UoW.Repository<productBrand>.Add(productBrand)

// Save changes, all tracked changes in the UoW, it will save or fail all changes.
UoW.Complete();
```

We will create transactions for each unit of work basically. UoW is all in a transaction.

## Implementing Unit OF Work
Create Interface
```C#
using System;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces
{
  public interface IUnitOfWork : IDisposable
  {
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;

    Task<int> Complete();

  }
}
```

Implement
```C#
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
```

## Updating Generic Repository for adding entities.
```C# IGenericRepository
        void Add(T entity);
        
        void Update(T entity);

        void Delete(T entity);
```

The implementation of the Generic Repo.
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

    public async Task<int> CountAsync(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).CountAsync();
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
      return SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);
    }

    public void Add(T entity)
    {
      _context.Set<T>().Add(entity);
    }

    public void Update(T entity)
    {
      // Start tracking this entity
      _context.Set<T>().Attach(entity);
      // Tell eniity framework that the entity above has been modified and all or some of it's values have changed.
      _context.Entry(entity).State = EntityState.Modified;
    }

    public void Delete(T entity)
    {
      _context.Set<T>().Remove(entity);
    }
  }
}
```

## Refactoring OrderService to use the new UnitOfWork and Repo Methods
```C#
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Core.Interfaces;

namespace Infrastructure.Services
{
  public class OrderService : IOrderService
  {
    private readonly IBasketRepository _basketRepository;
    private readonly IUnitOfWork _unitOfWork;
    public OrderService(IBasketRepository basketRepository, IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
      _basketRepository = basketRepository;
    }

    public async Task<Order> CreateOrderAsync(string buyerEmail, int deliveryMethodId, string basketId, Address shippingAddress)
    {
      // Get basket from repo.
      var basket = await _basketRepository.GetBasketAsync(basketId);

      // We can't trust the price in the basket, we'll have to take the price from the product.
      // Get items from product repo
      var items = new List<OrderItem>();

      foreach (var item in basket.Items)
      {
        // Get the prodcut from the repository
        var productItem = await _unitOfWork.Repository<Product>().GetByIdAsync(item.Id);

        // Create the snapshot of hte item at this give moment in time.
        var itemOrdered = new ProductItemOrdered(productItem.Id, productItem.Name, productItem.PictureUrl);

        // Create the order item, taking the price and quantity fro mthe proudct in the database not the basket.
        var orderItem = new OrderItem(itemOrdered, productItem.Price, item.Quantity);

        items.Add(orderItem);
      }

      // Get the delivery method from repo
      var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>().GetByIdAsync(deliveryMethodId);

      // Calculat subtotal
      var subtotal = items.Sum(item => item.Price * item.Quantity);

      // Create order
      var order = new Order(items, buyerEmail, shippingAddress, deliveryMethod, subtotal);

      // Save the Order
      _unitOfWork.Repository<Order>().Add(order);

      var result = await _unitOfWork.Complete();

      if (result <= 0)
        return null;
      
      // Delete bacsket
      await _basketRepository.DeleteBasketAsync(basketId);
      // Return the order
      return order;
    }

    public Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync()
    {
      throw new System.NotImplementedException();
    }

    public Task<Order> GetOrderByIdAsync(int id, string buyerEmail)
    {
      throw new System.NotImplementedException();
    }

    public Task<IReadOnlyList<Order>> GetOrdersForUserAsync(string buyerEmail)
    {
      throw new System.NotImplementedException();
    }
  }
}
```

## Implementing Order Get methods
In the order service we now can add the methods to get the data
We'll aslo create a new specifican.
```C#
    public async Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync()
    {
      return await _unitOfWork.Repository<DeliveryMethod>().ListAllAsync();
    }

    public async Task<Order> GetOrderByIdAsync(int id, string buyerEmail)
    {
      // We need to add Eager loading for Orders. Need a new specification.
      // Sort them by date as well so we need a new specification.
      var spec = new OrdersWithItemsAndOrderingSpecification(id, buyerEmail);

      return await _unitOfWork.Repository<Order>().GetEntityWithSpec(spec);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersForUserAsync(string buyerEmail)
    {
      var spec = new OrdersWithItemsAndOrderingSpecification(buyerEmail);

      return await _unitOfWork.Repository<Order>().ListAsync(spec);
    }
```

```C#
using System;
using System.Linq.Expressions;
using Core.Entities.OrderAggregate;

namespace Core.Specifications
{
  public class OrdersWithItemsAndOrderingSpecification : BaseSpecification<Order>
  {
    public OrdersWithItemsAndOrderingSpecification(string email) : base(o => o.BuyerEmail == email)
    {
        AddInclude(o => o.DeliveryMethod);
        AddInclude(o => o.OrderItems);
        AddOrderByDescending(o => o.OrderDate);
    }

    public OrdersWithItemsAndOrderingSpecification(int id, string email) : base(o => o.Id == id && o.BuyerEmail == email)
    {
        AddInclude(o => o.DeliveryMethod);
        AddInclude(o => o.OrderItems);
    }
  }
}
```

## Shaping Data For Orders
We want to flatten some data.
We'll create Dtos from it

```C# OrderToReturnDto
using System;
using System.Collections.Generic;
using Core.Entities.OrderAggregate;

namespace API.Dtos
{
  public class OrderToReturnDto
  {
    public int Id { get; set; }
    public string BuyerEmail { get; set; }
    public DateTimeOffset OrderDate { get; set; } 
    public Address ShipToAddress { get; set; }
    public string DeliveryMethod { get; set; }
    public decimal ShippingPrice { get; set; }
    public IReadOnlyList<OrderItemDto> OrderItems { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
  }
}
```

```C# OrderItemDto
namespace API.Dtos
{
    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string PictureUrl { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
```

Create mapping profiles for thes:
```C#
        CreateMap<Order, OrderToReturnDto>();
        CreateMap<OrderItem, OrderItemDto>();
```
Adjust the methods in the controller to return the Dtos now.

```C#
return Ok(_mapper.Map<OrderToReturnDto>(order));
```

Some data will be missing because we still have to configure some data.

## AutoMapper Config
Automapper will look for methods to configure properties.
For example if you have a total proerty and have a GetTotal() method, AutoMapper will be able to map the return value of GetTotal to the property total.

```C#
        CreateMap<Order, OrderToReturnDto>()
          .ForMember(d => d.DeliveryMethod, o => o.MapFrom(s => s.DeliveryMethod.ShortName))
          .ForMember(d => d.ShippingPrice, o => o.MapFrom(s => s.DeliveryMethod.Price));
        CreateMap<OrderItem, OrderItemDto>()
          .ForMember(d => d.ProductId, o => o.MapFrom(s => s.ItemOrdered.ProductItemId))
          .ForMember(d => d.ProductName, o => o.MapFrom(s => s.ItemOrdered.ProductName))
          .ForMember(d => d.PictureUrl, o => o.MapFrom(s => s.ItemOrdered.PictureUrl))
          .ForMember(d => d.PictureUrl, o => o.MapFrom<OrderItemUrlResolver>());
```

Value Resolver to popualte the full URL for this map
```C#
using API.Dtos;
using AutoMapper;
using Core.Entities.OrderAggregate;
using Microsoft.Extensions.Configuration;

namespace API.Helpers
{
  public class OrderItemUrlResolver : IValueResolver<OrderItem, OrderItemDto, string>
  {
    private readonly IConfiguration _config;
    public OrderItemUrlResolver(IConfiguration config)
    {
      _config = config;
    }

    public string Resolve(OrderItem source, OrderItemDto destination, string destMember, ResolutionContext context)
    {
      if (!string.IsNullOrEmpty(source.ItemOrdered.PictureUrl))
      {
          return _config["ApiUrl"] + source.ItemOrdered.PictureUrl;
      }

      return null;
    }
  }
}
```

## SQLite DateTime Offset
SQLite will not allow DateTimeOffset so we need to convert the dates to soemthing it can deal with. 
SQL Server is fine, we'll convert the data to binary for SQLite.
```C#
using System;
using System.Linq;
using System.Reflection;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data
{
  public class StoreContext : DbContext
  {
    public StoreContext(DbContextOptions<StoreContext> options) : base(options) {}

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductBrand> ProductBrands { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<DeliveryMethod> DeliveryMethods { get; set; }

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
          // Get all properties that have a DateTimeOffset
          var dateTimeProperties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(DateTimeOffset));

          // Loop over the propeties that have decimals
          // For each one set the conversion to double.
          foreach (var property in properties)
          {
            modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion<double>();
          }

          foreach (var property in dateTimeProperties)
          {
            modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion(new DateTimeOffsetToBinaryConverter());
          }
        }
      }
    }
  }
}
```

# Client Multi-Step Wizard
We will use Angular CDKStepper
`ng add @angular/cdk`

Create stepper as a shared component.



# Payment on API And Client
Goal: To be able to accept payments securely globally that complies with EU regulations and PCI DSS regulations.

* Creating a Stripe Account
* PCI DSS Compliance
* Strong Customer Authentication
* Payment Intents
* Using Stripe Elements (Credit Card Fields)
* Validating Cards
* Confirming Card Payment
* Webhooks - Allows to recieve confirmation from stipe directly into API


## PCI Complaince
Payment Card Industry Data Security Standard (PCI DSS)
All credit cards follow a set of standards
* Set of industry standards
* Designed to protect payment card Dta
* Increased protection for customers and reduced risk of data breaches involving personal card data

12 broad requirements adn collectively more than 100 line item requirements you need to meet if you're going to store people's credit cards.

There are 6 key areas:
* Building and maintaining a secure network
* Protecting cardholder data
* Maintaining a vulnerability management program
* Impelmenting strong access and control measures
* Regular montior and test networks
* Maintaining an information security policy.

We will use Stripe, they do the hard work to follow PCI Complaince.


Consequences for not meeting PCI Complaince 
* Montly financial penaties from $5000 to 100,000 
* Infringement consequences ($50 to 90$ per card hodler whose information has been endangered)
* Compensation costs 
* Legal Action
* Damaged reptuation
* Federal audits

## Strong Customer Authentication
* EU Standards for authenticating online payments
Around since September 2019
* Requires two of three elements:
* * Something customer knowns (password or pin)
* * Something the customer has (phone or hardware token)
* * Something the customer is (fingerprint or facial recognition)

Typically a password and text message or soemthing

* If these are not implemented banks wil decline payments that require SCA and don't meet the criteria.

Stripe without SCA would still be useful for (USA and Canadian payemnts)


FLOWS:

USA And Canada:
Customer click Submit
Order gets created on the API
API returns the order if successful
Payment is sent to Stripe
Stripe returns a one time use token i payment succeeds.
Client sends token to API
API verifies token with Stripe
Stripe confirms token
We can then tell the client we recieved the payment and send that from the API

In this flow we never touch the user's credit cards.

SCA Flow: (Accept payments globally)
1. Create a payment intent with the API (before payment)
* * can be done when user adds items to basket or when they go to checkout
2. API sends a payment intent to strip
3. Strip sends back a payemtn intent along with a client secret
4. API returns the client secret to the client
* Customer can leave or come back, if they add items to basket we have to update the payment intetn
5. Client creates order with API
6. On success client sends paymetn to Strip using the client secret.
7. Strip sends confirmation to client payment was successful
8. We need to utilize webhooks and Stripe will send confirmation to API that payment was successful. 
9. Payment confirmaed and can be shipped.

## Starting Stripe 
Need to add Stripe NugetPackage

Add to infrastructure Project:
`<PackageReference Include="Stripe.net" Version="36.12.2"/>`

Add keys to configuration setting:
Appsettings.json

```json
  "StripeSettings": {
    "PublishableKey": "pk_test_mAWLDrJG8JwiFMbH8FxblEbo00laHP2cQY",
    "SecretKey": "sk_test_DX8mJkB9EyTh4mf75XU572HH00Ge8DlBx0"
  },
```
Create a service to handle payments:
First set up the Interface:

```C#
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces
{
    public interface IPaymentService
    {
        Task<CustomerBasket> CreateOrUpdatePaymentIntent(string basketId);
    }
}
```

Create implementation:
```C#
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Services
{
  public class PaymentService : IPaymentService
  {
    public Task<CustomerBasket> CreateOrUpdatePaymentIntent(string basketId)
    {
      throw new System.NotImplementedException();
    }
  }
}
```

Add service to application services so that it's injectable:
```C#
using System.Linq;
using API.Errors;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions
{
    public static class ApplicationServicesExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IPaymentService, PaymentService>();
            ...
```

## Implementing Payment Intent
```C#
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Stripe;
using Product = Core.Entities.Product;

namespace Infrastructure.Services
{
  public class PaymentService : IPaymentService
  {
    private readonly IBasketRepository _basketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    public PaymentService(IBasketRepository basketRepository, IUnitOfWork unitOfWork, IConfiguration config)
    {
      _config = config;
      _unitOfWork = unitOfWork;
      _basketRepository = basketRepository;
    }

    public async Task<CustomerBasket> CreateOrUpdatePaymentIntent(string basketId)
    {
      StripeConfiguration.ApiKey = _config["StripeSettings:SecretKey"];

      var basket = await _basketRepository.GetBasketAsync(basketId);

      var shippingPrice = 0m;

      if (basket.DeliveryMethodId.HasValue) 
      {
        var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>().GetByIdAsync((int)basket.DeliveryMethodId);
        shippingPrice = deliveryMethod.Price;
      }

      foreach(var item in basket.Items)
      {
        var productItem = await _unitOfWork.Repository<Product>().GetByIdAsync(item.Id);

        if (item.Price != productItem.Price) 
        {
          item.Price = productItem.Price;
        }
      }

      var service = new PaymentIntentService();
      PaymentIntent intent;

      // Check if we have a payment intent, if we don't then we have to create one.
      if (string.IsNullOrEmpty(basket.PaymentIntentId))
      {
        var options = new PaymentIntentCreateOptions
        {
          Amount = (long) basket.Items.Sum(i => i.Quantity * (i.Price * 100)) + (long) shippingPrice * 100,
          Currency = "usd",
          PaymentMethodTypes = new List<string>{"card"}
        };

        // Get the payment intent from Stripe
        intent = await service.CreateAsync(options);

        basket.PaymentIntentId = intent.Id;
        basket.ClientSecret = intent.ClientSecret;
      } else {
        // If the payament intetn exists we just have to update the amount
        var options = new PaymentIntentUpdateOptions
        {
          Amount = (long) basket.Items.Sum(i => i.Quantity * (i.Price * 100)) + (long) shippingPrice * 100
        };

        await service.UpdateAsync(basket.PaymentIntentId, options);
      }

      // Update the basket.
      await _basketRepository.UpdateBasketAsync(basket);

      return basket;
    }
  }
}
```

## Creating a Payment Controller
```C#
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
  public class PaymentsController : BaseApiController
  {
    private readonly IPaymentService _paymentService;
    public PaymentsController(IPaymentService paymentService)
    {
      _paymentService = paymentService;
    }

    [Authorize]
    [HttpPost("{basketId}")]
    public async Task<ActionResult<CustomerBasket>> CreateOrUpdatePaymentIntent(string basketId)
    {
        return await _paymentService.CreateOrUpdatePaymentIntent(basketId);
    }
  }
}
```

## Adding Stripe to Client
in index.html
```html
<body>
  <app-root></app-root>
  <script scr="https://js.stripe.com/v3/"></script>
</body>
```


## Fixing problem
When we create an Order we will save the PaymentIntentId

## Telling the server the payment was successful (Webhooks)
Right now we submit an order and then there is no way for our API to know that the order was successful.
The basket hangs around event though we delete it locally

We need to listent to stripe to tell us that the payment was successful

We will create an enpoint that stripe can use to send events to. We will get the body and convert it to a stripeEvent. It will make sure that our signature matches when converting. 

```C#
    [HttpPost("webook")]
    public async Task<ActionResult> StripeWebhook()
    {
      var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

      var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], WhSecret);

      PaymentIntent intent;
      Order order;

      switch (stripeEvent.Type)
      {
        case "payment_intent_succeeded":
          intent = (PaymentIntent) stripeEvent.Data.Object;
          _logger.LogInformation("Payment Succeeded", intent.Id);
          // TODO: Update our order with new status
          break;
        case "payment_intent_payment_failed":
          intent = (PaymentIntent) stripeEvent.Data.Object;
          _logger.LogInformation("Payment failed", intent.Id);
          // TODO: Update our order status
          break;
      }

      return new EmptyResult();
    }
```

## Updating order after recieving webhook
```C#
using System.Threading.Tasks;
using Core.Entities;
using Core.Entities.OrderAggregate;

namespace Core.Interfaces
{
    public interface IPaymentService
    {
        Task<CustomerBasket> CreateOrUpdatePaymentIntent(string basketId);
        Task<Order> UpdateOrderPaymentSucceeded(string paymentIntentId);
        Task<Order> UpdateOrderPaymentFailed(string paymentIntentId);
    }
}
```

```C#
    public async Task<Core.Entities.OrderAggregate.Order> UpdateOrderPaymentFailed(string paymentIntentId)
    {
      var spec = new OrderByPaymentIntentIdSpecification(paymentIntentId);

      var order = await _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().GetEntityWithSpec(spec);

      if (order == null)
        return null;

      order.Status = OrderStatus.PaymentFailed;

      await _unitOfWork.Complete();

      return order;
    }

    public async Task<Core.Entities.OrderAggregate.Order> UpdateOrderPaymentSucceeded(string paymentIntentId)
    {
      var spec = new OrderByPaymentIntentIdSpecification(paymentIntentId);

      var order = await _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().GetEntityWithSpec(spec);

      if (order == null)
        return null;

      order.Status = OrderStatus.PaymentReceived;

      _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().Update(order);
      await _unitOfWork.Complete();

      return null;
    }
```

## Testing Stipe Webhooks
Install CLI
Add the secret aafter logging in

# Performance
Best way to make a big difference with minimal effort is to enable caching.

## Caching on API
Respoonses from the database will be cached so we don't have to ask the controller or database.

```C#
using System;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface IResponseCacheService
    {
        Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive);
        
        Task<string> GetCachedResponseAsync(string cacheKey);
    }
}
```

Implementation
```C#
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Services
{
  public class ResponseCacheService : IResponseCacheService
  {
    private readonly IDatabase _database;
    public ResponseCacheService(IConnectionMultiplexer redis)
    {
      _database = redis.GetDatabase();
    }

    public async Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive)
    {
      if (response == null)
        return;

      var options = new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      };

      var serializedResponse = JsonSerializer.Serialize(response, options);

      await _database.StringSetAsync(cacheKey, serializedResponse, timeToLive);
    }

    public async Task<string> GetCachedResponseAsync(string cacheKey)
    {
      var cachedResponse = await _database.StringGetAsync(cacheKey);

      if (cachedResponse.IsNullOrEmpty)
        return null;

      return cachedResponse;
    }
  }
}
```

Application servies:
```C#
            // Add caching as a singleton
            services.AddSingleton<IResponseCacheService, ResponseCacheService>();
```

## Creating attribute for caching
In Helpers

Fitlers allow code to be run before or after specific stages in the request processing pipeline.
We can sure before an action method is called or after a method has been exceuted.

Before the actino is executed we want to check if we have it in the cache. And after the action is executed we want to cache the result.

```C#
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace API.Helpers
{
  public class CachedAttribute : Attribute, IAsyncActionFilter
  {
    private readonly int _timeToLiveInSeconds;
    public CachedAttribute(int timeToLiveInSeconds)
    {
      _timeToLiveInSeconds = timeToLiveInSeconds;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cacheService = context.HttpContext.RequestServices.GetRequiredService<IResponseCacheService>();

        // generate a key
        var cacheKey = GenerateCacheKeyFromRequest(context.HttpContext.Request);
        
        // See if the response is cached already
        var cachedResponse = await cacheService.GetCachedResponseAsync(cacheKey);

        // If we have a respone from redis then retunr the resposne without goin to the controller
        if (!string.IsNullOrEmpty(cachedResponse)) {
            var contentResult = new ContentResult
            {
                Content = cachedResponse,
                ContentType = "application/json",
                StatusCode = 200
            };

            context.Result = contentResult;

            return;
        }

        // Move to the controller
        var executedContext = await next();

        // The executed context is OK we can put it in the cache.
        if (executedContext.Result is OkObjectResult okObjectResult)
        {
            await cacheService.CacheResponseAsync(cacheKey, okObjectResult.Value, TimeSpan.FromSeconds(_timeToLiveInSeconds));
        }

    }

    private  string GenerateCacheKeyFromRequest(HttpRequest request)
    {
        // Organize the queryparams
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"{request.Path}");
        foreach (var (key, value) in request.Query.OrderBy(x => x.Key))
        {
            keyBuilder.Append($"|{key}-{value}");
        }

        return keyBuilder.ToString();
    }
  }
}
```

## Testing The API Cache

Add the attributes to the controllers

The rpoducts brands etc can be cached:
```C#
[Cached(600)]
```


# Client Side Performance
Currently when we click on a product we laod the product, when we go to the shop we load products again.

Data that is in components is thrown away right away when the component is closed.


Products, Brands and Types are pretty static. Right now we are going to the API and getting products every time.
A better way is to store this data in the service.

We can add properties for the data
```ts
  products: IProduct[];
  brands: IBrand[];
  types: IType[];
```

and then we can change our methods to check if we already have the data 
```ts
  getProduct(id: number) {
    const product = this.products.find(p => p.id === id);

    // If we have the product in our cache we can return it.
    if (product) {
      return of(product);
    }
    return this.http.get<IProduct>(this.baseUrl + 'products/' + id);
  }
```

However caching for paginated results will be harder.
Our getProducts gets only paginated products, not all of them.

We will move our shopParams and Pagination into the shop service from the shop component.

```ts

```



# Publishing

## Angular Build Configuration
angular.json holds the build configuaration

Some features:
* Replace production.dev with production
* Output hashing to cache bust
* Minimize and optimize
* Don't include source maps
* Css will be extracted from global styles into css files
* AOT = ahead of time compilation, no more compiler, drastically reduce file size.

When building angular will compile two version:
ES2015 for modern browsers and ES5 for internet explorer

## Angular config changes
* CHeck the environment.prod and environment file.

Remove the URL for production because the app will be served from kestrel on our server.
```ts
export const environment = {
  production: true,
  apiUrl: 'api/'
};
```

* Remove any delay for production

* Angular.json
Order of styles is not respected by build tools
Take all styles out of the array in angular.json and add them to your styles.scss
```css
@import "../node_modules/bootstrap/dist/css/bootstrap.min.css";
@import "../node_modules/ngx-bootstrap/datepicker/bs-datepicker.css";
@import "../node_modules/bootswatch/dist/united/bootstrap.min.css";
@import "../node_modules/font-awesome/css/font-awesome.min.css";
@import "../node_modules/ngx-toastr/toastr.css";
```

* Change output path
We want to pulish to API/wwwroot
```json
"outputPath": "../API/wwwroot",
```

* Move images from wwwroot into a new folder called Content
Currently we are serving images from wwwroot folder we need to move them to a different folder because wwwroot will be used for angualar and angular will be cleaning up that directory on each build. 

* Fix Startup to serve Images from Content

```C#
      // Serve anything inside wwwroot
      app.UseStaticFiles();

      // Anything going for content will go to Content folder
      app.UseStaticFiles(new StaticFileOptions
      {
        FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Content")),
        RequestPath = "/content"
      });
```

* Update the API URL
In appsettingss.Development.json
```C#
 "ApiUrl": "https://localhost:5001/Content/",
```

* Tell API about Endpoints for angular so that API doesnt try to route Angualr
```C#
      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
        // Add a fallback controller to server angualr for any routes that are not found.
        endpoints.MapFallbackToController("Index", "Fallback");
      });
```

```C#
using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class FallbackController : Controller
    {
        public IActionResult Index() 
        {
            return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/HTML");
        }
    }
}
```

## Building Angular
The build engine in Angualr 9 is IVY. No longer experimental.

The vendor file includes the angular compiler but the production version should use Ahead of Time compilation, remove the compiler and the bundles should be much much smaller.

`ng build` - Regualr build command with angular

`ng build --prod` - Use production with AOT IVY compilation


## Installing MySQL
Install MySQL
Remember password for root user
Be able to login to mysql
`mysql -u root -p`

`show databases`

Create a user for connection stirng
`CREATE USER 'appuser'@'localhost' IDENTIFIED BY 'Pa$$w0rd';`

Grant permissions
`GRANT ALL PRIVILEGES ON *.* TO 'appuser'@'localhost' WITH GRANT OPTION;`

`FLUSH PRIVILEGES;`



## Swiching DB Servers
Install Database Provider:
`Pomleo.EntityFramworkCore.MySQL` - match with version of runtime. Add to infrastructure project


## Move configuration from Payment Controller.
```C#
    // private const string WhSecret = "whsec_THIoovKaF4h3AZ4PZiabanKw3xCMUOuY";
    private readonly string _whSecret;
    private readonly ILogger<IPaymentService> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<IPaymentService> logger, IConfiguration config)
    {
      _logger = logger;
      _paymentService = paymentService;
      _whSecret = config.GetSection("StripeSettings:WhSecret").Value;
    }
```

## Seperating out databse servies in startup
We can use different DBContext for production vs development

This is convention based and has to match the names.

```C#
    public void ConfigureDevelopmentServices(IServiceCollection services)
    {
      services.AddDbContext<StoreContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("DefaultConnection")));
      services.AddDbContext<AppIdentityDbContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("IdentityConnection")));
      ConfigureServices(services);
    }
    public void ConfigureProductionServices(IServiceCollection services)
    {
      services.AddDbContext<StoreContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("DefaultConnection")));
      services.AddDbContext<AppIdentityDbContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("IdentityConnection")));
      ConfigureServices(services);
    }
```

## Migrations and switching databases
.NetCore 3.0 changes make this a bit harder.

Previously you were able to add annotations and keep the migrations. But now it's been overly complex.

So one of the better solution is to remove all the migrations and re-create the migrations for the new database providers.

In order to create a migration for production we have to make the session to use Production

Widnows:
Command Prompt:
`set ASPNETCORE_ENVIRONMENT=Development`

PowerShell:
`$Env:ASPNETCORE_ENVIRONMENT=Development`

## Change enviornment for API
In launchSettings.json
```json
    "API": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "weatherforecast",
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
```
Change the ASPNETCORE_ENVIRONMENT here. This is only for the API project, it does not have anything to do with dotnet ef tools,dotnet run looks at this. That you still ahve to set the variable in the command line.


## Pre-Deployment Work
Create a Publishng folder to have all the ocntent needed.

Release configuration output to publish folder at the root
`dotnet publish -c Release -o publish Skinet.sln`

Content folder does not get moved over to the publish folder
Seed data does not get moved over to the publish folder

To fix this:
```xml In API.cspoj
  <ItemGroup>
    // Add this::::
    <Content Include="Content\**" CopyToPublishDirectory="PreserveNewest"/>
    <ProjectReference Include="..\Infrastructure\Infrastructure.csproj"/>
  </ItemGroup>
```

And same in Infrastructure.cspoj
This configuration will not only copy it for the publish folder but also for the bin folder so we can test.

```xml
  <ItemGroup>
    <None Include="Data\SeedData\**" CopyToOutputDirectory="PreserveNewest" />
    <ProjectReference Include="..\Core\Core.csproj"/>
  </ItemGroup>
```

We'll need to adjust our StoreContextSeed

We'll take the path relative to the assmbly. And go into the Data/SeedData folder.
```C#
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (!context.ProductBrands.Any())
        {
          // Will be run from Program Class path.
          // var brandsData = File.ReadAllText("../Infrastructure/Data/SeedData/brands.json");
          var brandsData = File.ReadAllText(path + @"/Data/SeedData/brands.json");
```

IN the exception Middleware:
We can inlcude the stack trace just for initial deployment
Middleware
`new ApiException((int)HttpStatusCode.InternalServerError, ex.Message, ex.StackTrace.ToString()) `

## Deploying to Digital Ocean

You can create a droplet, there is one with LAMP
Apache, MYSQL, PHP on Ubuntu

*** LINUX SERVER SETUP USING A NEWLY CREATED DIGITAL OCEAN LAMP SERVER ***

1. ssh root@ipaddressOfLinuxServer (follow instructions to change password)

2. Set up mysql (password available from welcome message)

cat /root/.digitalocean_password

mysql -u root -p

CREATE USER 'appuser'@'localhost' IDENTIFIED BY 'Pa$$w0rd';
GRANT ALL PRIVILEGES ON *.* TO 'appuser'@'localhost' WITH GRANT OPTION;
FLUSH PRIVILEGES;

3.  Install Redis on the server:

sudo apt update
sudo apt install redis-server
sudo nano /etc/redis/redis.conf

Inside the config look for the line:

#       They do not enable continuous liveness pings back to your supervisor.
supervised no

Change this to:

supervised systemd

Ctrl + X then yes to save changes in the nano editor

Check the status:

sudo systemctl status redis

Check we receive Pong back via the redis cli:

redis-cli
ping

quit out of the redis cli

4.  Install the dotnet runtime (follow instructions from here https://dotnet.microsoft.com/download/linux-package-manager/ubuntu18-04/runtime-current)

5.  Configure Apache

a2enmod proxy proxy_http proxy_html rewrite

systemctl restart apache2

6.  Configure the virtual host

sudo nano /etc/apache2/sites-available/skinet.conf

<VirtualHost *:80>
ProxyPreserveHost On
ProxyPass / http://127.0.0.1:5000/
ProxyPassReverse / http://127.0.0.1:5000/

ErrorLog /var/log/apache2/skinet-error.log
CustomLog /var/log/apache2/skinet-access.log common

</VirtualHost>

6. Enable the site 

a2ensite skinet

7.  Disable the default Apache site:

a2dissite 000-default

Then restart apache

systemctl reload apache2

8. Update the config in appsettings:

Update the endpoints for the webhooks to point to the IP address of the new server https://LinuxIPAddress/api/payments/webhook

Copy the Webhook secret to the appsettings.json file

9.  Add the deploy.reloaded extension to VS Code

10.  Add a settings.json file to the .vscode folder and add the following:

{
    "deploy.reloaded": {
        "packages": [
            {
                "name": "Version 1.0.0",
                "description": "Package version 1.0.0",

                "files": [
                    "publish/**"
                ]
            }
        ],

        "targets": [
            {
                "type": "sftp",
                "name": "Linux",
                "description": "SFTP folder",

                "host": "IP Address", "port": 22,
                "user": "root", "password": "Your Linux password",

                "dir": "/var/skinet",
                "mappings": {
                    "publish/**": "/"
                }
            }
        ]
    }
}

11.  Publish the dotnet application locally from the solution folder:

Update the appsettings.json file and change the ApiUrl to match your server IP address e.g:

"ApiUrl": "http://128.199.203.224/Content/",

dotnet publish -c Release -o publish Skinet.sln

This will create a new folder called publish

12.  Deploy the package using deploy reloaded

=== Back to the Linux server ====

13.  Restart the journalctl service as this has been not working on fresh installs and is very useful to get information about the service:

systemctl restart systemd-journald

14.  Set up the service that will run the kestrel web server

sudo nano /etc/systemd/system/skinet-web.service

Paste in the folllowing:

[Unit]
Description=Kestrel service running on Ubuntu 18.04
[Service]
WorkingDirectory=/var/skinet
ExecStart=/usr/bin/dotnet /var/skinet/API.dll
Restart=always
RestartSec=10
SyslogIdentifier=skinet
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment='Token__Key=CHANGE ME TO SOMETHING SECURE'
Environment='Token__Issuer=https://yoursitegoeshere'
[Install]
WantedBy=multi-user.target


*** NOTE
Colons : have to be repalced with __
***
Then run:

sudo systemctl enable skinet-web.service
sudo systemctl start skinet-web.service

15.  Ensure the server is running:

netstat -ntpl

16.  Check the journal logs:

journalctl -u skinet-web.service
journalctl -u skinet-web.service | tail -n 300
journalctl -u skinet-web.service --since "5 min ago"


===== 
certificate
=====

Demo the Program.cs so URL is different
Demo the config in Apache:

sudo nano /etc/apache2/sites-available/skinet.conf
sudo nano /etc/systemd/system/skinet-web.service

sudo systemctl restart skinet-web.service

redis-cli --scan --pattern '*product*' | xargs -L 100 redis-cli del

