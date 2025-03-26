using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using PowerSync.Domain.Interfaces;
using PowerSync.Infrastructure.Configuration;
using PowerSync.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Logging middleware
builder.Services.AddLogging();

// Bind the PowerSyncConfig from appsettings.json and environment variables
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<PowerSyncConfig>(builder.Configuration.GetSection(PowerSyncConfig.SectionName));


// Add PowerSyncConfig as a singleton service
builder.Services.AddSingleton<NpgsqlConnection>(provider =>
{
    var config = provider.GetRequiredService<IOptions<PowerSyncConfig>>().Value;
    var connectionString = config.DatabaseUri;

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string is missing.");
    }

    // Set up connection to the Supabase PostgreSQL database
    return new NpgsqlConnection(connectionString);
});

// Register IPersisterFactory and IPersister
builder.Services.AddSingleton<PersisterFactoryRegistry>();
builder.Services.AddSingleton<IPersisterFactory>(provider =>
{
    var registry = provider.GetRequiredService<PersisterFactoryRegistry>();
    //var config = provider.GetRequiredService<PowerSyncConfig>();
    var config = provider.GetRequiredService<IOptions<PowerSyncConfig>>().Value;
    return registry.GetFactory(config.DatabaseType!);
});
builder.Services.AddSingleton<IPersister>(provider =>
{
    var factory = provider.GetRequiredService<IPersisterFactory>();
    //var config = provider.GetRequiredService<PowerSyncConfig>();
    var config = provider.GetRequiredService<IOptions<PowerSyncConfig>>().Value;
    return factory.CreatePersisterAsync(config.DatabaseUri!).Result;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// Middleware configuration
app.UseHttpsRedirection();

// CORS middleware
app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Logging request middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

// Root route
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "powersync-dotnet-backend-todolist-demo",
    });
});

// API routes will be added here
app.MapControllers();

try
{
    app.Run();
}
catch (Exception err)
{
    Console.WriteLine($"Unexpected error: {err}");
}