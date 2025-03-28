using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PowerSync.Domain.Interfaces;
using PowerSync.Infrastructure.Configuration;
using PowerSync.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container
builder.Services.AddControllers();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});


// Add environment variables and configuration
builder.Configuration.AddEnvironmentVariables();

// Register PersisterFactoryRegistry first
builder.Services.AddSingleton<PersisterFactoryRegistry>();

var envDatabaseUri = Environment.GetEnvironmentVariable("DATABASE_URI");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:DatabaseUri"] = envDatabaseUri;
}

var envDatabaseType = Environment.GetEnvironmentVariable("DATABASE_TYPE");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:DatabaseType"] = envDatabaseType;
}

var envPrivateKey = Environment.GetEnvironmentVariable("POWERSYNC_PRIVATEKEY");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:PrivateKey"] = envPrivateKey;
}

var envPublicKey = Environment.GetEnvironmentVariable("POWERSYNC_PUBLICKEY");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:PublicKey"] = envPublicKey;
}

var envJWTIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:JwtIssuer"] = envJWTIssuer;
}

var envUrl = Environment.GetEnvironmentVariable("POWERSYNC_URL");
if (!string.IsNullOrWhiteSpace(envDatabaseUri))
{
    builder.Configuration[$"{PowerSyncConfig.SectionName}:Url"] = envUrl;
}

// Configure PowerSync settings
builder.Services.Configure<PowerSyncConfig>(builder.Configuration.GetSection(PowerSyncConfig.SectionName));

// Validate and register configuration
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IOptions<PowerSyncConfig>>().Value;
    var logger = provider.GetRequiredService<ILogger<PowerSyncConfig>>();

    if (!config.ValidateConfiguration(out var validationErrors))
    {
        var errorMessage = $"PowerSync configuration is invalid: {string.Join(", ", validationErrors)}";
        logger.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
    }

    return config;
});

// Register database connection
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<PowerSyncConfig>();
    return new NpgsqlConnection(config.DatabaseUri);
});

// Register IPersisterFactory
builder.Services.AddSingleton<IPersisterFactory>(provider =>
{
    var registry = provider.GetRequiredService<PersisterFactoryRegistry>();
    var config = provider.GetRequiredService<PowerSyncConfig>();
    var logger = provider.GetRequiredService<ILogger<IPersisterFactory>>();

    try
    {
        return registry.GetFactory(config.DatabaseType!);
    }
    catch (ArgumentException ex)
    {
        logger.LogError(ex, "Failed to get persister factory");
        throw;
    }
});

// Register IPersister
builder.Services.AddSingleton<IPersister>(provider =>
{
    var factory = provider.GetRequiredService<IPersisterFactory>();
    var config = provider.GetRequiredService<PowerSyncConfig>();
    var logger = provider.GetRequiredService<ILogger<IPersister>>();

    try
    {
        return factory.CreatePersisterAsync(config.DatabaseUri!);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create persister");
        throw;
    }
});

// Configure JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

// Build the application
var app = builder.Build();

// Middleware configuration
app.UseHttpsRedirection();

// CORS middleware
app.UseCors("AllowAll");

// Development-specific configuration
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = 
                context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            
            var exception = exceptionHandlerPathFeature.Error;
            
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(new 
            {
                error = "Internal Server Error",
                message = exception.Message,
                stackTrace = app.Environment.IsDevelopment() ? exception.StackTrace : null
            }));
        });
    });
}

app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
        return;
    }

    await next();
});

// Logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log request details
    logger.LogInformation($"Incoming Request: {context.Request.Method} {context.Request.Path}");
    logger.LogInformation($"Origin: {context.Request.Headers["Origin"]}");
    logger.LogInformation($"Referer: {context.Request.Headers["Referer"]}");

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Enhanced error logging
        logger.LogError(ex, $"Unhandled exception processing request: {context.Request.Path}");
        
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("An unexpected error occurred");
    }
});

// Root route
app.MapGet("/", () => Results.Ok(new
{
    message = "powersync-dotnet-backend-todolist-demo",
}));

// API routes
app.MapControllers();

// Global exception handling
try
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
    //var port = "5227"; 
    app.Run($"http://0.0.0.0:{port}");
}
catch (Exception ex)
{
    // Use proper logging instead of Console.WriteLine
    Console.WriteLine($"Critical error: {ex}");
}