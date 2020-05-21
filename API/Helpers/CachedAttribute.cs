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