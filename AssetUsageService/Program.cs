using AssetUsageService.Business.Controllers;
using AssetUsageService.Data;
using AssetUsageService.Infrastructure;
using AssetUsageService.Integration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MongoDB:ConnectionString"]
        ?? throw new InvalidOperationException("MongoDB:ConnectionString configuration is missing");
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IAssetItemLinkRepository, AssetItemLinkRepository>();
builder.Services.AddSingleton<DBContext>();
builder.Services.AddSingleton<ContentHubConnectionService>();
builder.Services.AddSingleton<AssetItemController>();
builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddSingleton<APIGateway>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    if (services.GetRequiredService<IConfiguration>().GetValue<bool>("MongoDB:SeedData", false))
    {
        await services.GetRequiredService<DBContext>().SeedDataAsync();
    }

    var logger = services.GetRequiredService<ILogger<Program>>();
    var gateway = services.GetRequiredService<APIGateway>();

    try
    {
        var reachable = await gateway.IsContentHubReachableAsync();
        var client = await gateway.GetContentHubClientAsync();
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "ContentHub test failed");
    }
}

app.Run();
