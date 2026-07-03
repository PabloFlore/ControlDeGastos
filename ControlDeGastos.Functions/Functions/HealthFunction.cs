using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Functions.Functions;

public class HealthFunction
{
    [Function("Health")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/health")] HttpRequestData req,
        FunctionContext context)
    {
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteAsJsonAsync(new { status = "ok" });
        return response;
    }
}
