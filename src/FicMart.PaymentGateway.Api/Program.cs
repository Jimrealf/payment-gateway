using FicMart.PaymentGateway.Api;
using FicMart.PaymentGateway.Api.Payments;
using FicMart.PaymentGateway.Infrastructure;
using FicMart.PaymentGateway.Infrastructure.Persistence;
using FicMart.PaymentGateway.Api.Observability;
using FicMart.PaymentGateway.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var logUnhandledRequest = LoggerMessage.Define<string, string>(
    LogLevel.Error,
    new EventId(5000, "UnhandledGatewayRequest"),
    "Unhandled gateway request failure for {Method} {Path}");
builder.Services.AddPaymentGatewayInfrastructure(builder.Configuration);
builder.Services.AddOptions<IdempotencyOptions>()
    .Bind(builder.Configuration.GetSection(IdempotencyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<FicMartApiOptions>()
    .Bind(builder.Configuration.GetSection(FicMartApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentGatewayMetrics>();
builder.Services.AddScoped<PaymentStore>();
builder.Services.AddScoped<PaymentWorkflowStore>();
builder.Services.AddScoped<RequestFingerprint>();
builder.Services.AddScoped<PaymentService>();
var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<PaymentGatewayDbContext>();
    await database.Database.MigrateAsync();
}

app.UseExceptionHandler(exceptionHandler => exceptionHandler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
    logUnhandledRequest(logger, context.Request.Method, context.Request.Path, exception);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(
        new PaymentErrorResponse("internal_error", "The gateway could not complete the request."),
        context.RequestAborted);
}));
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<FicMartApiKeyMiddleware>();
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));
app.MapGet("/health/live", () => TypedResults.Ok(new HealthResponse("healthy")));
app.MapGet("/health/ready", async (PaymentGatewayDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new HealthResponse("ready"))
        : Results.Json(new HealthResponse("not_ready"), statusCode: 503));
app.MapPaymentEndpoints();

app.Run();

public partial class Program
{
}
