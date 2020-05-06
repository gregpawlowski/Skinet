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
