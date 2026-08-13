using FicMart.PaymentGateway.Api;
using FicMart.PaymentGateway.Api.Payments;
using FicMart.PaymentGateway.Infrastructure;
using FicMart.PaymentGateway.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPaymentGatewayInfrastructure(builder.Configuration);
builder.Services.AddOptions<IdempotencyOptions>()
    .Bind(builder.Configuration.GetSection(IdempotencyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PaymentStore>();
builder.Services.AddScoped<PaymentWorkflowStore>();
builder.Services.AddScoped<RequestFingerprint>();
builder.Services.AddScoped<PaymentService>();
var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));
app.MapPaymentEndpoints();

app.Run();

public partial class Program
{
}
