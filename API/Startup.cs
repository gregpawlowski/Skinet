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

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddControllers();

      services.AddDbContext<StoreContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("DefaultConnection")));

      services.AddDbContext<AppIdentityDbContext>(opt => opt.UseSqlServer(_configuration.GetConnectionString("IdentityConnection")));

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
      
      app.UseStaticFiles();

      app.UseCors("CorsPolicy");

      app.UseAuthentication();

      app.UseAuthorization();

      app.UseSwaggerDocumentation();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });
    }
  }
}
