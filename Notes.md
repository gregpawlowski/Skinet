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

# Setting up Git

# API Architecture
