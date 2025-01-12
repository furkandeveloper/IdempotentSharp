using System.Text.Json;
using IdempotentSharp.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace IdempotentSharp.AspNetCore.MinimalApi.Filters;

/// <summary>
/// Provides idempotent functionality as an endpoint filter for Minimal API endpoints.
/// </summary>
public sealed class IdempotentFilter(int cacheTimeInMinutes = IdempotentFilter.DefaultCacheTimeInMinutes)
    : IEndpointFilter
{
    private const int DefaultCacheTimeInMinutes = 60;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(cacheTimeInMinutes);

    /// <summary>
    /// Invokes the endpoint filter asynchronously, providing idempotent functionality.
    /// </summary>
    /// <param name="context">The context of the endpoint invocation.</param>
    /// <param name="next">The delegate to execute the next filter or endpoint.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the endpoint invocation.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!TryGetRequestId(context.HttpContext, out Guid requestId))
        {
            return Results.BadRequest("Invalid or missing X-Request-Id header");
        }

        var cacheKey = GetCacheKey(requestId);
        var cache = context.HttpContext.RequestServices.GetRequiredService<HybridCache>();
        var cachedResult = await GetCachedResultAsync(cache, cacheKey);

        if (cachedResult is not null)
        {
            return CreateCachedResult(cachedResult);
        }

        var result = await next(context);
        await CacheResponseAsync(cache, cacheKey, result);

        return result;
    }

    /// <summary>
    /// Attempts to retrieve the request ID from the HTTP context headers.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing the request headers.</param>
    /// <param name="requestId">When this method returns, contains the parsed request ID if the header is present and valid; otherwise, Guid.Empty.</param>
    /// <returns><c>true</c> if the request ID header is present and valid; otherwise, <c>false</c>.</returns>
    private static bool TryGetRequestId(HttpContext httpContext, out Guid requestId)
    {
        requestId = Guid.Empty;
        return httpContext.Request.Headers.TryGetValue("X-Request-Id", out var idempotenceKeyValue) &&
               Guid.TryParse(idempotenceKeyValue, out requestId);
    }

    /// <summary>
    /// Generates a cache key using the provided request ID.
    /// </summary>
    /// <param name="requestId">The unique identifier for the request.</param>
    /// <returns>A string representing the cache key.</returns>
    private static string GetCacheKey(Guid requestId) => $"X-Request-Id-{requestId}";

    /// <summary>
    /// Retrieves the cached result asynchronously using the provided cache key.
    /// </summary>
    /// <param name="cache">The hybrid cache instance to use for retrieving the cached result.</param>
    /// <param name="cacheKey">The key used to identify the cached result.</param>
    /// <returns>A task that represents the asynchronous operation, containing the cached result as a string, or null if no cached result exists.</returns>
    private static async Task<string?> GetCachedResultAsync(HybridCache cache, string cacheKey)
    {
        return await cache.GetOrCreateAsync(cacheKey, _ => ValueTask.FromResult(null as string));
    }

    /// <summary>
    /// Creates an HTTP result from the cached result string.
    /// </summary>
    /// <param name="cachedResult">The cached result as a JSON string.</param>
    /// <returns>An <see cref="IResult"/> representing the deserialized cached response.</returns>
    private static IResult CreateCachedResult(string cachedResult)
    {
        var response = JsonSerializer.Deserialize<IdempotentResponse>(cachedResult)!;
        return Results.Json(response.Value, statusCode: response.StatusCode);
    }

    /// <summary>
    /// Caches the response asynchronously if the result is a successful HTTP status code.
    /// </summary>
    /// <param name="cache">The hybrid cache instance to use for caching the response.</param>
    /// <param name="cacheKey">The key used to identify the cached response.</param>
    /// <param name="result">The result of the endpoint invocation to be cached.</param>
    /// <returns>A task that represents the asynchronous caching operation.</returns>
    private async Task CacheResponseAsync(HybridCache cache, string cacheKey, object? result)
    {
        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } statusCodeResult
            and IValueHttpResult valueResult)
        {
            var statusCode = statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
            IdempotentResponse response = new(statusCode, valueResult.Value);

            await cache.SetAsync(
                cacheKey,
                response,
                new HybridCacheEntryOptions
                {
                    Expiration = _cacheDuration,
                    LocalCacheExpiration = _cacheDuration
                }
            );
        }
    }
}