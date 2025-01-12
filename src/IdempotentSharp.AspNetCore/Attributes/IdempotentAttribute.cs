using System.Text.Json;
using IdempotentSharp.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace IdempotentSharp.AspNetCore.Attributes;

/// <summary>
/// This Attribute provides idempotent endpoint functionality.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class IdempotentAttribute(int cacheTimeInMinutes = IdempotentAttribute.DefaultCacheTimeInMinutes)
    : Attribute, IAsyncActionFilter
{
    private const int DefaultCacheTimeInMinutes = 60;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(cacheTimeInMinutes);

    /// <summary>
    /// Executes the action filter asynchronously, providing idempotent endpoint functionality.
    /// </summary>
    /// <param name="context">The context of the action being executed.</param>
    /// <param name="next">The delegate to execute the next action filter or action method.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!TryGetRequestId(context, out Guid requestId))
        {
            context.Result = new BadRequestObjectResult("Invalid or missing Idempotence-Key header");
            return;
        }

        var cacheKey = GetCacheKey(requestId);
        var cache = context.HttpContext.RequestServices.GetRequiredService<HybridCache>();
        var cachedResult = await GetCachedResultAsync(cache, cacheKey);

        if (cachedResult is not null)
        {
            context.Result = CreateCachedResult(cachedResult);
            return;
        }

        var executedContext = await next();
        await CacheResponseAsync(cache, cacheKey, executedContext);
    }
    /// <summary>
    /// Tries to retrieve the request ID from the HTTP headers.
    /// </summary>
    /// <param name="context">The context of the action being executed.</param>
    /// <param name="requestId">The output parameter that will contain the parsed request ID if successful.</param>
    /// <returns>
    /// True if the request ID was successfully retrieved and parsed; otherwise, false.
    /// </returns>
    private static bool TryGetRequestId(ActionExecutingContext context, out Guid requestId)
    {
        requestId = Guid.Empty;
        return context.HttpContext.Request.Headers.TryGetValue("X-Request-Id", out StringValues idempotenceKeyValue) &&
               Guid.TryParse(idempotenceKeyValue, out requestId);
    }

    /// <summary>
    /// Generates a cache key based on the provided request ID.
    /// </summary>
    /// <param name="requestId">The unique identifier for the request.</param>
    /// <returns>A string representing the cache key.</returns>
    private static string GetCacheKey(Guid requestId) => $"X-Request-Id-{requestId}";

    /// <summary>
    /// Retrieves a cached result asynchronously based on the provided cache key.
    /// </summary>
    /// <param name="cache">The hybrid cache instance used to retrieve the cached result.</param>
    /// <param name="cacheKey">The key used to identify the cached result.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the cached result as a string, or null if no result is found.
    /// </returns>
    private static async Task<string?> GetCachedResultAsync(HybridCache cache, string cacheKey)
    {
        return await cache.GetOrCreateAsync(cacheKey, _ => ValueTask.FromResult(null as string));
    }

    /// <summary>
    /// Creates an ObjectResult from the cached result string.
    /// </summary>
    /// <param name="cachedResult">The cached result as a JSON string.</param>
    /// <returns>An ObjectResult containing the deserialized response value and status code.</returns>
    private static ObjectResult CreateCachedResult(string cachedResult)
    {
        var response = JsonSerializer.Deserialize<IdempotentResponse>(cachedResult)!;
        return new ObjectResult(response.Value) { StatusCode = response.StatusCode };
    }

    /// <summary>
    /// Caches the response asynchronously based on the provided cache key and executed context.
    /// </summary>
    /// <param name="cache">The hybrid cache instance used to store the response.</param>
    /// <param name="cacheKey">The key used to identify the cached response.</param>
    /// <param name="executedContext">The context of the executed action containing the result to be cached.</param>
    /// <returns>A task that represents the asynchronous caching operation.</returns>
    private async Task CacheResponseAsync(HybridCache cache, string cacheKey, ActionExecutedContext executedContext)
    {
        if (executedContext.Result is ObjectResult { StatusCode: >= 200 and < 300 } objectResult)
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            IdempotentResponse response = new(statusCode, objectResult.Value);
            await cache.SetAsync(cacheKey, response, new HybridCacheEntryOptions
            {
                Expiration = _cacheDuration,
                LocalCacheExpiration = _cacheDuration
            });
        }
    }
}