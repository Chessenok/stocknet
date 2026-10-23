# Logging & Monitoring Guidelines (Serilog)

## 1. Setup & Configuration
Use `Serilog.AspNetCore` as the primary logging provider. Replace the default Microsoft logger in `Program.cs`.

Configuration requirements:
- Output structured logs in JSON format for production (or formatted text for local development).
- Log to Console by default.
- Set minimum log levels appropriately (e.g., `Information` for general app flow, `Warning`/`Error` for exceptions).
- Never log passwords/password hashes, JWTs, authorization headers, Stripe secrets, webhook signing secrets, or payment credentials. Avoid logging raw webhook payloads that may contain sensitive data.
- Prefer CompanyId as a structured actor/correlation field only after it has been validated; never treat or log a client-supplied ID as authenticated identity.

Example `Program.cs` setup:
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting StockNet API...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // ... other services ...
    
    var app = builder.Build();

    // Mandatory: Request logging middleware
    app.UseSerilogRequestLogging();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
