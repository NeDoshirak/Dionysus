using Microsoft.AspNetCore.Mvc;
[ApiController]
public sealed class HealthController : ControllerBase { [HttpGet("/health")] public IActionResult Get() => Ok(new { status = "ok" }); }
