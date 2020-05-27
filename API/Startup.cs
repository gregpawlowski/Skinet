using System.IO;
using API.Extensions;
using API.Helpers;
using API.Middleware;
using AutoMapper;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using StackExchange.Redis;

namespace API
{
  public class Startup
  {
    private readonly IConfiguration _configuration;
    public Startup(IConfiguration configuration)
    {
      _configuration = configuration;
    }

    //
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

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddControllers();

      services.AddSingleton<IConnectionMultiplexer>( c => {
        var configuration = ConfigurationOptions.Parse(_configuration.GetConnectionString("Redis"), true);
        return ConnectionMultiplexer.Connect(configuration);
      });

      services.AddAutoMapper(typeof(MappingProfiles));

      // Use our extension method to add services.
      services.AddApplicationServices();

      // Add Identity
      services.AddIdentityServices(_configuration);

      services.AddSwaggerDocumentation();

      services.AddCors(opt => 
      {
        opt.AddPolicy("CorsPolicy", policy => 
        {
          policy.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:4200");
        });
      });

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      // if (env.IsDevelopment())
      // {
      //   app.UseDeveloperExceptionPage();
      // }

      app.UseMiddleware<ExceptionMiddleware>();

      // WIll hit this middleware and will pass the status code.
      app.UseStatusCodePagesWithReExecute("/errors/{0}");


      app.UseHttpsRedirection();

      app.UseRouting();
      
      // Serve anything inside wwwroot
      app.UseStaticFiles();

      // Anything going for content will go to Content folder
      app.UseStaticFiles(new StaticFileOptions
      {
        FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Content")),
        RequestPath = "/content"
      });

      app.UseCors("CorsPolicy");

      app.UseAuthentication();

      app.UseAuthorization();

      app.UseSwaggerDocumentation();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
        // Add a fallback controller to server angualr for any routes that are not found.
        endpoints.MapFallbackToController("Index", "Fallback");
      });
    }
  }
}
