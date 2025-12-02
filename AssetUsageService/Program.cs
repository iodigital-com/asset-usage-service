using AssetUsageService.Business.Controllers;
using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Handlers;
using AssetUsageService.Business.Handlers.interfaces;
using AssetUsageService.Business.Services;
using AssetUsageService.Business.Services.ServiceBusQueueServices;
using AssetUsageService.Domain.Data;
using AssetUsageService.Infrastructure;
using AssetUsageService.Integration;
using AssetUsageService.Integration.Mappers;
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

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MongoDB:ConnectionString"]
        ?? throw new InvalidOperationException("MongoDB:ConnectionString configuration is missing");
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IAssetItemLinkRepository, AssetItemLinkRepository>();
builder.Services.AddSingleton<DeltaCalculationService>();
builder.Services.AddSingleton<DBContext>();
builder.Services.AddSingleton<IMediator, InMemoryMediator>();
builder.Services.AddSingleton<IEventHandler<PushToDamEvent>, PushToDamHandler>();
builder.Services.AddSingleton<IEventHandler<AssetIdsByPublicLinksEvent>, AssetIdsByPublicLinksHandler>();
builder.Services.AddSingleton<PublishAssetIdsByPublicLinksEventService>();
builder.Services.AddSingleton<PublishPushToDamEventsService>();
builder.Services.AddSingleton<PublishedItemMapper>();
builder.Services.AddSingleton<IContentHubConnectionService, ContentHubConnectionService>();
builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddSingleton<AssetItemController>();
builder.Services.AddSingleton<IServiceBusQueueService, ServiceBusQueueService>();
builder.Services.AddSingleton<ServiceBusConfigService>();


var app = builder.Build();

app.Run();
