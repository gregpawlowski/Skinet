using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Generic;
using Core.Entities;
using System;
using Microsoft.EntityFrameworkCore;
using Core.Entities.OrderAggregate;

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


          foreach (var item in brands)
          {
            context.ProductBrands.Add(item);
          }

          await context.Database.OpenConnectionAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ProductBrands ON");
          await context.SaveChangesAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ProductBrands OFF");
          context.Database.CloseConnection();
        }

        if (!context.ProductTypes.Any())
        {
          // Will be run from Program Class path.
          var typesData = File.ReadAllText("../Infrastructure/Data/SeedData/types.json");

          var types = JsonSerializer.Deserialize<List<ProductType>>(typesData);

          foreach (var item in types)
          {
            context.ProductTypes.Add(item);
          }

          await context.Database.OpenConnectionAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ProductTypes ON");
          await context.SaveChangesAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ProductTypes OFF");
          context.Database.CloseConnection();
        }

        if (!context.Products.Any())
        {
          // Will be run from Program Class path.
          var productData = File.ReadAllText("../Infrastructure/Data/SeedData/products.json");

          var products = JsonSerializer.Deserialize<List<Product>>(productData);

          foreach (var item in products)
          {
            context.Products.Add(item);
          }

          await context.SaveChangesAsync();
        }

        if (!context.DeliveryMethods.Any())
        {
          // Will be run from Program Class path.
          var dmData = File.ReadAllText("../Infrastructure/Data/SeedData/delivery.json");

          var deliveryMethods = JsonSerializer.Deserialize<List<DeliveryMethod>>(dmData);

          foreach (var method in deliveryMethods)
          {
            context.DeliveryMethods.Add(method);
          }

          await context.Database.OpenConnectionAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT DeliveryMethods ON");
          await context.SaveChangesAsync();
          await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT DeliveryMethods OFF");
          context.Database.CloseConnection();
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