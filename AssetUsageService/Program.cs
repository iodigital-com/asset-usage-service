using AssetUsageService.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MongoDB:ConnectionString"]
        ?? throw new InvalidOperationException("MongoDB:ConnectionString configuration is missing");
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<DBContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    if (services.GetRequiredService<IConfiguration>().GetValue<bool>("MongoDB:SeedData", false))
    {
        await services.GetRequiredService<DBContext>().SeedDataAsync();
    }
}

app.Run();
