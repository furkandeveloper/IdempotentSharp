using IdempotentSharp.AspNetCore.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace IdempotentSharp.Sample.AspNetCore.Controllers;

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