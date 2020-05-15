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
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            // Adding a scoped generic service for dependency injection
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Adding basket Reposiory
            services.AddScoped<IBasketRepository, BasketRepository>();

            // This has to be bleow the AddControllers() becuase we are configuring the APi Behavior, controllers have to be loaded first.
            // This configuration has to do with how to throw model state errors.
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