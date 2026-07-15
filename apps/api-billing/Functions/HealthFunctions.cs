using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace RestrictPoint.Api.Billing.Functions;

public static class HealthFunctions
{
    [Function("HealthLive")]
    public static IActionResult Live(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/live")] HttpRequest request)
    {
        return new OkObjectResult(new { status = "Healthy" });
    }

    [Function("HealthReady")]
    public static IActionResult Ready(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequest request)
    {
        return new OkObjectResult(new { status = "Healthy" });
    }
}
