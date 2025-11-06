# Asset Usage Service

A microservice for tracking and managing relationships between items and digital assets across Sitecore CMS and ContentHub DAM. Built as an Azure Function application with MongoDB storage.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Complete Data Flow](#complete-data-flow)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Data Model](#data-model)
- [API Endpoints](#api-endpoints)
- [Integration Points](#integration-points)
- [Development Setup](#development-setup)
- [Testing](#testing)
- [Deployment](#deployment)

## Overview

The Asset Usage Service provides a centralized system for tracking which digital assets (from ContentHub DAM) are used in Sitecore CMS items. It maintains bidirectional relationships through an event-driven architecture, enabling:

- Track which assets are used by specific Sitecore items
- Find which Sitecore items are using specific assets
- Calculate delta changes for asset usage
- Synchronize asset metadata between Sitecore and ContentHub

## Architecture

### System Components

```
┌──────────────────────────────────────────────────────────────────┐
│                        SITECORE CMS                               │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         iO.Sitecore.Publishing Module                     │   │
│  │         - PublishEventHandler                             │   │
│  │         - OnItemProcessing                                │   │
│  │         - OnItemProcessed                                 │   │
│  │         - OnPublishEnd                                    │   │
│  └────────────────────────┬─────────────────────────────────┘   │
│                           │                                       │
└───────────────────────────┼───────────────────────────────────────┘
                            │
                            │ HTTP POST
                            │ (Publish Events)
                            ▼
┌──────────────────────────────────────────────────────────────────┐
│                   ASSET USAGE SERVICE                             │
│                   (Azure Functions)                               │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │             SitecorePublishAPI                            │   │
│  │             (HTTP Trigger)                                │   │
│  └────────────────────────┬─────────────────────────────────┘   │
│                           │                                       │
│  ┌────────────────────────▼─────────────────────────────────┐   │
│  │           Business Layer                                  │   │
│  │           - AssetItemController                           │   │
│  │           - DeltaCalculationService                       │   │
│  │           - PushToDamHandler                              │   │
│  └────────────────────────┬─────────────────────────────────┘   │
│                           │                                       │
│  ┌────────────────────────▼─────────────────────────────────┐   │
│  │           Infrastructure Layer                            │   │
│  │           - AssetItemLinkRepository                       │   │
│  └────────────────────────┬─────────────────────────────────┘   │
│                           │                                       │
│  ┌────────────────────────▼─────────────────────────────────┐   │
│  │           Data Access Layer                               │   │
│  │           - DBContext                                     │   │
│  └────────────────────────┬─────────────────────────────────┘   │
│                           │                                       │
└───────────────────────────┼───────────────────────────────────────┘
                            │
        ┌───────────────────┼────────────────────┐
        │                   │                    │
        ▼                   ▼                    ▼
  ┌──────────┐      ┌──────────────┐    ┌──────────────┐
  │ MongoDB  │      │  ContentHub  │    │ Application  │
  │ Database │      │     DAM      │    │   Insights   │
  └──────────┘      └──────────────┘    └──────────────┘
```

## Complete Data Flow

### 1. Sitecore Publish Event Flow

```
SITECORE CMS
     │
     │ (1) User publishes item
     ▼
┌─────────────────────────────────┐
│  Sitecore Publishing Pipeline   │
└──────────────┬──────────────────┘
               │
               │ (2) Trigger publish:itemProcessing event
               ▼
┌─────────────────────────────────┐
│ iO.Sitecore.Publishing          │
│ PublishEventHandler             │
│ OnItemProcessing()              │
└──────────────┬──────────────────┘
               │
               │ (3) Extract item data:
               │     - Item ID (Guid)
               │     - Asset references
               │     - Metadata
               ▼
┌─────────────────────────────────┐
│ Local Audit Log                 │
│ $(dataFolder)/logs/             │
│ published-items.json            │
└──────────────┬──────────────────┘
               │
               │ (4) Trigger publish:itemProcessed event
               ▼
┌─────────────────────────────────┐
│ PublishEventHandler             │
│ OnItemProcessed()               │
└──────────────┬──────────────────┘
               │
               │ (5) HTTP POST to Asset Usage Service
               │     Endpoint: /api/SitecorePublishAPI
               │     Body: {
               │       "itemId": "guid",
               │       "assetIds": [123, 456],
               │       "action": "publish"
               │     }
               ▼
┌─────────────────────────────────┐
│ ASSET USAGE SERVICE             │
│ SitecorePublishAPI Function     │
└──────────────┬──────────────────┘
               │
               │ (6) Process publish event
               ▼
     [Continue to Service Processing Flow]
```

### 2. Asset Usage Service Processing Flow

```
Asset Usage Service
     │
     │ (1) Receive publish event from Sitecore
     ▼
┌─────────────────────────────────┐
│ SitecorePublishAPI              │
│ (HTTP Trigger)                  │
└──────────────┬──────────────────┘
               │
               │ (2) Validate request
               │     - Check authentication
               │     - Validate payload
               ▼
┌─────────────────────────────────┐
│ AssetItemController             │
└──────────────┬──────────────────┘
               │
               │ (3) Query existing relationships
               ▼
┌─────────────────────────────────┐
│ AssetItemLinkRepository         │
│ GetAssetItemLinkByItemIdAsync() │
└──────────────┬──────────────────┘
               │
               │ (4) Fetch from MongoDB
               ▼
┌─────────────────────────────────┐
│ MongoDB Database                │
│ AssetItemLinks Collection       │
└──────────────┬──────────────────┘
               │
               │ (5) Return existing data
               ▼
┌─────────────────────────────────┐
│ DeltaCalculationService         │
└──────────────┬──────────────────┘
               │
               │ (6) Calculate changes:
               │     - Added assets
               │     - Removed assets
               │     - Unchanged assets
               ▼
┌─────────────────────────────────┐
│ AssetItemLinkRepository         │
│ InsertAssetItemLinkAsync()      │
└──────────────┬──────────────────┘
               │
               │ (7) Update MongoDB
               ▼
┌─────────────────────────────────┐
│ MongoDB Database                │
│ Upsert Document                 │
└──────────────┬──────────────────┘
               │
               │ (8) Trigger DAM sync
               ▼
┌─────────────────────────────────┐
│ PushToDamHandler                │
└──────────────┬──────────────────┘
               │
               │ (9) Update asset metadata
               ▼
     [Continue to ContentHub Flow]
```

### 3. ContentHub DAM Integration Flow

```
Asset Usage Service
     │
     │ (1) Prepare asset updates
     ▼
┌─────────────────────────────────┐
│ PushToDamHandler                │
└──────────────┬──────────────────┘
               │
               │ (2) Get ContentHub client
               ▼
┌─────────────────────────────────┐
│ APIGateway                      │
│ GetContentHubClientAsync()      │
└──────────────┬──────────────────┘
               │
               │ (3) Authenticate
               ▼
┌─────────────────────────────────┐
│ ContentHubConnectionService     │
│ CreateClient()                  │
└──────────────┬──────────────────┘
               │
               │ (4) OAuth2 Client Credentials
               │     - ClientId
               │     - ClientSecret
               ▼
┌─────────────────────────────────┐
│ Stylelabs M.Sdk WebClient       │
└──────────────┬──────────────────┘
               │
               │ (5) HTTPS Connection
               ▼
┌─────────────────────────────────┐
│ Sitecore ContentHub             │
│ REST API                        │
└──────────────┬──────────────────┘
               │
               │ (6) Update asset relations:
               │     - Link to Sitecore items
               │     - Update usage metadata
               │     - Set relation properties
               ▼
┌─────────────────────────────────┐
│ ContentHub Database             │
│ Asset Relations Updated         │
└─────────────────────────────────┘
```

### 4. Reverse Query Flow (Asset → Items)

```
External System / ContentHub
     │
     │ (1) Query: "Which items use Asset X?"
     ▼
┌─────────────────────────────────┐
│ Asset Usage Service API         │
└──────────────┬──────────────────┘
               │
               │ (2) GetItemIdsByAssetId(X)
               ▼
┌─────────────────────────────────┐
│ AssetItemLinkRepository         │
└──────────────┬──────────────────┘
               │
               │ (3) MongoDB Query:
               │     db.AssetItemLinks.find({
               │       assetIds: X
               │     })
               ▼
┌─────────────────────────────────┐
│ MongoDB Database                │
│ Index Scan on assetIds          │
└──────────────┬──────────────────┘
               │
               │ (4) Return matching documents
               ▼
┌─────────────────────────────────┐
│ Response to Caller              │
│ [                               │
│   { itemId: "guid1", ... },     │
│   { itemId: "guid2", ... }      │
│ ]                               │
└─────────────────────────────────┘
```

### 5. Complete End-to-End Flow

```
┌─────────────────┐
│ SITECORE CMS    │
│                 │
│ Content Editor  │
│ publishes item  │
│ with assets     │
└────────┬────────┘
         │
         │ Publish Pipeline
         ▼
┌─────────────────────────────────┐
│ iO.Sitecore.Publishing          │
│ Event Handler                   │
│                                 │
│ 1. OnItemProcessing             │
│    - Extract item data          │
│    - Log to audit file          │
│                                 │
│ 2. OnItemProcessed              │
│    - POST to Asset Service      │
│                                 │
│ 3. OnPublishEnd                 │
│    - Batch processing complete  │
└────────┬────────────────────────┘
         │
         │ HTTP POST
         │ http://localhost:7183/api/SitecorePublishAPI
         ▼
┌─────────────────────────────────┐
│ ASSET USAGE SERVICE             │
│ (Azure Function)                │
│                                 │
│ 1. Receive Event                │
│    - Validate payload           │
│                                 │
│ 2. Query MongoDB                │
│    - Get current state          │
│                                 │
│ 3. Calculate Delta              │
│    - Compare old vs new         │
│                                 │
│ 4. Update MongoDB               │
│    - Save new relationships     │
│                                 │
│ 5. Sync to ContentHub           │
│    - Update asset metadata      │
└────────┬────────────────────────┘
         │
         │ OAuth2 + REST API
         ▼
┌─────────────────────────────────┐
│ SITECORE CONTENTHUB             │
│                                 │
│ 1. Authenticate Request         │
│                                 │
│ 2. Update Asset Relations       │
│    - Link to Sitecore items     │
│                                 │
│ 3. Update Metadata              │
│    - Usage count                │
│    - Last used date             │
│    - Related items list         │
└─────────────────────────────────┘
```

## Technology Stack

### Core Technologies

- **.NET 8.0**: Application framework
- **Azure Functions (Isolated Worker)**: Serverless compute
- **MongoDB**: NoSQL document database
- **Sitecore CMS**: Content management system
- **Sitecore ContentHub SDK**: DAM integration
- **Application Insights**: Monitoring and telemetry

### Key Dependencies

- `Microsoft.Azure.Functions.Worker`
- `MongoDB.Driver`
- `Stylelabs.M.Sdk.WebClient`
- `Microsoft.Extensions.DependencyInjection`

## Prerequisites

- .NET 8.0 SDK or later
- Azure Functions Core Tools v4
- MongoDB instance (local or Azure CosmosDB with MongoDB API)
- Sitecore CMS instance (with iO.Sitecore.Publishing module)
- Access to Sitecore ContentHub instance
- Azure subscription (for deployment)

## Configuration

### Sitecore CMS Configuration

Install the `iO.Sitecore.Publishing` event handler by placing the configuration file in:

`C:\inetpub\wwwroot\[SITECORE.INSTANCE]\App_Config\Include\zzz.iO\iO.Publishing.Events.config`

```xml
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <events>
      <event name="publish:itemProcessing">
        <handler type="iO.Sitecore.publishing.Events.PublishEventHandler, iO.Sitecore.publishing" method="OnItemProcessing" />
      </event>
      <event name="publish:itemProcessed">
        <handler type="iO.Sitecore.publishing.Events.PublishEventHandler, iO.Sitecore.publishing" method="OnItemProcessed" />
      </event>
      <event name="publish:end">
        <handler type="iO.Sitecore.publishing.Events.PublishEventHandler, iO.Sitecore.publishing" method="OnPublishEnd" />
      </event>
      <event name="publish:end:remote">
        <handler type="iO.Sitecore.publishing.Events.PublishEventHandler, iO.Sitecore.publishing" method="OnPublishEndRemote" />
      </event>
    </events>
    
    <settings>
      <setting name="AssetUsage.AuditLogPath" value="$(dataFolder)/logs/published-items.json" />
      <setting name="AssetUsageService.Endpoint" value="http://localhost:7183/api/SitecorePublishAPI" />
      <setting name="AssetUsageService.UserAgent" value="Sitecore-AssetUsage/1.0" />
    </settings>
  </sitecore>
</configuration>
```

### Asset Usage Service Configuration

Configure in `local.settings.json` (local) or Azure Function App Configuration (production):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    
    "MongoDB:ConnectionString": "mongodb://localhost:27017",
    "MongoDB:DatabaseName": "AssetUsageDb",
    "MongoDB:SeedData": "false",
    
    "ContentHub:Endpoint": "https://your-instance.stylelabs.cloud",
    "ContentHub:ClientId": "your-client-id",
    "ContentHub:ClientSecret": "your-client-secret",
    
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "your-app-insights-connection-string"
  }
}
```

### Configuration Options

#### Sitecore Settings

- **AssetUsage.AuditLogPath**: Local audit log file path for published items
- **AssetUsageService.Endpoint**: Asset Usage Service API endpoint
- **AssetUsageService.UserAgent**: User agent string for HTTP requests

#### MongoDB Settings

- **ConnectionString**: MongoDB connection string
- **DatabaseName**: Database name for asset usage data
- **SeedData**: Set to `true` to populate test data on startup

#### ContentHub Settings

- **Endpoint**: ContentHub instance URL
- **ClientId**: OAuth2 client ID
- **ClientSecret**: OAuth2 client secret

## Data Model

### AssetItemLink Collection

Document structure in MongoDB:

```csharp
{
  "_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Guid (ItemId)
  "assetIds": [34013, 13343, 23213],               // List<int>
  "createdAt": "2025-11-06T10:30:00Z",            // DateTime
  "updatedAt": "2025-11-06T14:25:00Z"             // DateTime
}
```

#### Field Descriptions

- **_id (ItemId)**: Unique identifier for the Sitecore item (GUID)
- **assetIds**: Array of asset IDs from ContentHub DAM
- **createdAt**: Timestamp when the relationship was first created (UTC)
- **updatedAt**: Timestamp of the last update to asset relationships (UTC)

#### Indexes

```javascript
db.AssetItemLinks.createIndex({ "assetIds": 1 })
db.AssetItemLinks.createIndex({ "updatedAt": -1, "createdAt": -1 })
```

## API Endpoints

### SitecorePublishAPI

**Endpoint**: `POST /api/SitecorePublishAPI`

**Authorization**: Function level

**Description**: Receives publish events from Sitecore CMS

**Request Body**:
```json
{
  "itemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "assetIds": [34013, 13343, 23213],
  "action": "publish",
  "timestamp": "2025-11-06T14:25:00Z"
}
```

**Response**:
```json
{
  "success": true,
  "itemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "assetsProcessed": 3
}
```

### Planned Endpoints

- `GET /api/items/{itemId}/assets` - Get all assets for an item
- `GET /api/assets/{assetId}/items` - Get all items using an asset
- `DELETE /api/items/{itemId}` - Remove all asset links for an item

## Integration Points

### Sitecore CMS Integration

The `iO.Sitecore.Publishing` module captures publish events:

- **OnItemProcessing**: Triggered when item enters publish pipeline
- **OnItemProcessed**: Triggered after item is published
- **OnPublishEnd**: Triggered when publish operation completes
- **OnPublishEndRemote**: Triggered for remote publish events

### Repository Interface

```csharp
public interface IAssetItemLinkRepository
{
    Task<AssetItemLink> InsertAssetItemLinkAsync(
        Guid itemId, 
        List<int> assetIds, 
        CancellationToken cancellationToken = default);
    
    Task<AssetItemLink?> GetAssetItemLinkByItemIdAsync(
        Guid itemId, 
        CancellationToken cancellationToken = default);
    
    Task<List<int>> GetAssetIdsFromItemIdAsync(
        Guid itemId, 
        CancellationToken cancellationToken = default);
    
    Task<List<AssetItemLink>> GetItemIdsByAssetIdAsync(
        int assetId, 
        CancellationToken cancellationToken = default);
}
```

### ContentHub Integration

The `ContentHubConnectionService` manages authentication and connectivity:

```csharp
var client = await apiGateway.GetContentHubClientAsync();
var isReachable = await apiGateway.IsContentHubReachableAsync();
```

## Development Setup

### Local Development

1. Clone the repository:
```bash
git clone https://github.com/weareyou/asset-usage-service.git
cd asset-usage-service
```

2. Install dependencies:
```bash
dotnet restore
```

3. Configure local settings:
```bash
cp local.settings.json.example local.settings.json
```

4. Start MongoDB:
```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```

5. Run the application:
```bash
func start
```

6. Configure Sitecore:
- Copy `iO.Publishing.Events.example` to your Sitecore instance
- Update `AssetUsageService.Endpoint` to point to your local function
- Restart Sitecore

### Project Structure

```
asset-usage-service/
├── AssetUsageService/
│   ├── Business/
│   │   ├── Controllers/
│   │   ├── Events/
│   │   ├── Handlers/
│   │   └── Services/
│   ├── Data/
│   │   ├── AssetItemLink.cs
│   │   └── DBContext.cs
│   ├── Infrastructure/
│   │   ├── AssetItemLinkRepository.cs
│   │   └── IAssetItemLinkRepository.cs
│   ├── Integration/
│   │   ├── APIGateway.cs
│   │   ├── ContentHubConnectionService.cs
│   │   └── MessageHandler.cs
│   ├── Function1.cs
│   └── Program.cs
├── AssetUsageServiceTests/
│   ├── Integration/
│   └── Performance/
├── iO.Sitecore.publishing/
│   └── Events/
│       └── PublishEventHandler.cs
├── iO.Publishing.Events
├── iO.Publishing.Events.example
└── README.md
```

## Testing

### Running Tests

```bash
dotnet test

dotnet test --filter "Category=Performance"

dotnet test --filter "FullyQualifiedName~Integration"
```

### Test Categories

- **Unit Tests**: Business logic and service tests
- **Integration Tests**: ContentHub and MongoDB integration
- **Performance Tests**: Repository performance benchmarks

## Deployment

### Azure Deployment

1. Create Azure resources:
```bash
az group create --name asset-usage-rg --location westeurope
az storage account create --name assetusagestorage --resource-group asset-usage-rg
az functionapp create --name asset-usage-service --resource-group asset-usage-rg \
  --consumption-plan-location westeurope --runtime dotnet-isolated --runtime-version 8 \
  --functions-version 4 --storage-account assetusagestorage
```

2. Configure application settings:
```bash
az functionapp config appsettings set --name asset-usage-service \
  --resource-group asset-usage-rg \
  --settings "MongoDB:ConnectionString=your-connection-string" \
             "MongoDB:DatabaseName=AssetUsageDb" \
             "ContentHub:Endpoint=your-endpoint"
```

3. Deploy the application:
```bash
func azure functionapp publish asset-usage-service
```

4. Update Sitecore configuration:
- Update `AssetUsageService.Endpoint` to Azure Function URL
- Restart Sitecore

### Database Deployment

Create indexes for optimal performance:
```bash
mongosh "your-connection-string" --eval "
  db.AssetItemLinks.createIndex({ 'assetIds': 1 });
  db.AssetItemLinks.createIndex({ 'updatedAt': -1 });
"
```

## Monitoring and Logging

### Application Insights

The service integrates with Azure Application Insights:

- Request tracing and performance monitoring
- Exception tracking and diagnostics
- Custom metrics and events
- Dependency tracking (MongoDB, ContentHub, Sitecore)

### Logging Levels

Configure in `host.json`:

```json
{
  "logging": {
    "logLevel": {
      "default": "Information",
      "AssetUsageService": "Information",
      "AssetUsageService.Integration": "Debug"
    }
  }
}
```

### Sitecore Audit Logs

Published items are logged locally at:
`$(dataFolder)/logs/published-items.json`

## Troubleshooting

### Sitecore Integration Issues

- Verify `iO.Sitecore.Publishing.dll` is in the bin folder
- Check Sitecore logs for event handler errors
- Confirm `AssetUsageService.Endpoint` is accessible from Sitecore server
- Validate audit log file permissions

### MongoDB Connection Failures

- Verify connection string format
- Check network connectivity and firewall rules
- Ensure MongoDB version compatibility (4.0+)

### ContentHub Authentication Errors

- Validate ClientId and ClientSecret
- Check OAuth2 permissions in ContentHub
- Verify endpoint URL format (must include https://)
