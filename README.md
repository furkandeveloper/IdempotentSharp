# IdempotentSharp
<p align="center">
  <img src="https://github.com/user-attachments/assets/449d7dd6-ae6a-4c52-92ef-bdbe962f1215" style="max-width:100%;" height="300" />
</p>

## Give a Star 🌟
If you liked the project or if **IdempotentSharp** helped you, please give a star.

### Purpose
**IdempotentSharp** allows you to develop idempotent endpoints.

### How To Use(?)

### Install
If you are using Minimal API

```bash
dotnet add package IdempotentSharp.AspNetCore.MinimalApi
```

If you are using Controller

```bash
dotnet add package IdempotentSharp.AspNetCore
```

To use the `IdempotentSharp` library, you must configure the `Hybrid Cache` feature.

```csharp
builder.Services.AddHybridCache(options =>
{
    // Maximum size of cached items
    options.MaximumPayloadBytes = 1024 * 1024 * 10; // 10MB
    options.MaximumKeyLength = 512;

    // Default timeouts
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };
});
```
If you are using redis:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "{your_connection_string}";
});
```
Now everything is ready.

You can give this feature to any endpoint you want with the idempotent attribute.

```csharp
[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private readonly List<string> Response = ["value1", "value2", "value3"];
    [HttpGet]
    [Idempotent]
    public async Task<IActionResult> GetAsync()
    {
        return Ok(Response);
    }
}
```

When the endpoint with the attribute added runs, it throws an error if it cannot find the `X-Request-Id` value from the headers.

For Minimal Api:
```csharp
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return Results.Ok(forecast);
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi()
    .AddEndpointFilter<IdempotentFilter>();
```


## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=furkandeveloper/IdempotentSharp&type=Date)](https://star-history.com/#furkandeveloper/IdempotentSharp&Date)
