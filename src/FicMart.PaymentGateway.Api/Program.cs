using FicMart.PaymentGateway.Api;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));

app.Run();

public partial class Program
{
}
