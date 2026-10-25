using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Json;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Authentication;
using StockNet.Api.Infrastructure.Errors;
using StockNet.Api.Infrastructure.Health;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Infrastructure.Seeding;
using StockNet.Api.Options;
using StockNet.Api.Services;
using StockNet.Api.Services.Billing;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting StockNet API");

    var builder = WebApplication.CreateBuilder(args);

    const long maxMultipartBodyBytes = 10 * 1024 * 1024;
    const long maxRequestBodyBytes = 12 * 1024 * 1024;
    var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodyBytes);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        if (context.HostingEnvironment.IsDevelopment())
        {
            loggerConfiguration.WriteTo.Console();
        }
        else
        {
            loggerConfiguration.WriteTo.Console(new JsonFormatter());
        }
    });

    var connectionString = builder.Configuration.GetConnectionString("StockNet");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'StockNet' is required. Configure it with user-secrets or the ConnectionStrings__StockNet environment variable.");
    }

    var jwtConfiguration = builder.Configuration.GetSection("Jwt");
    var jwtIssuer = jwtConfiguration["Issuer"];
    var jwtAudience = jwtConfiguration["Audience"];
    var jwtSigningKey = jwtConfiguration["SigningKey"];
    var jwtExpirationMinutes = jwtConfiguration.GetValue("ExpirationMinutes", 60);

    if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    {
        throw new InvalidOperationException("JWT issuer and audience must be configured under the Jwt configuration section.");
    }

    if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 UTF-8 bytes.");
    }

    if (jwtExpirationMinutes is < 5 or > 1440)
    {
        throw new InvalidOperationException("Jwt:ExpirationMinutes must be between 5 and 1440.");
    }

    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxMultipartBodyBytes);
    builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedCorsOrigins)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type")
        .WithExposedHeaders("Location")));
    builder.Services.AddDbContext<StockNetDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.UseNetTopologySuite();
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: new[] { "42704", "53300", "08006", "40001" });
            npgsqlOptions.CommandTimeout(60);
        }));
    builder.Services.AddScoped<IPasswordHasher<Company>, PasswordHasher<Company>>();
    builder.Services.AddScoped<JwtTokenService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<StockSubscriptionGuard>();
    builder.Services.AddScoped<DevelopmentSeeder>();
    builder.Services.AddScoped<PerformanceSeeder>();
    builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
    builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
    builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = CompanyClaims.CompanyId,
                RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (context.Principal is null || !CompanyClaims.TryGetCompanyId(context.Principal, out _))
                    {
                        context.Fail("The token does not contain a valid CompanyId claim.");
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            policy.RequireAuthenticatedUser()
                .AddRequirements(new IsAdminRequirement()));
    });
    builder.Services.AddScoped<IAuthorizationHandler, IsAdminAuthorizationHandler>();

    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddHealthChecks()
        .AddCheck<PostgresHealthCheck>("postgres");

    builder.Services.AddOpenApi();
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddSwaggerGen();
    }

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors("Frontend");
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "StockNet API v1");
            options.RoutePrefix = "swagger";
        });
    }
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
    }

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (httpContext, report) =>
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
            });
        }
    }).AllowAnonymous();

    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        await seeder.SeedAsync(app.Lifetime.ApplicationStopping);
        if (app.Configuration.GetValue<bool>("Seed:PerformanceData"))
        {
            var performanceSeeder = scope.ServiceProvider.GetRequiredService<PerformanceSeeder>();
            await performanceSeeder.SeedAsync(10, 2, 50, app.Lifetime.ApplicationStopping);
        }
    }

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "StockNet API terminated unexpectedly. ExceptionType: {ExceptionType}", exception.GetType().FullName);
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
