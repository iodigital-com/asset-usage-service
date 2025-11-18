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
- [Deployment](#deployment)
- [Testing](#testing)

## Overview

The Asset Usage Service provides a centralized system for tracking which digital assets (from ContentHub DAM) are used in Sitecore CMS items. It maintains bidirectional relationships through an event-driven architecture.

### Key Features

- Track which assets are used by specific Sitecore items
- Find which Sitecore items are using specific assets
- Calculate delta changes for asset usage
- Synchronize asset metadata between Sitecore and ContentHub

## Architecture

Microservice diagram: 
```mermaid
classDiagram
    %% Integration Layer
    class MessageHandler {
        -ILogger~MessageHandler~ _logger
        -PublishedItemMapper _mapper
        -AssetItemController _controller
        +MessageHandler(logger, mapper, controller)
        +ProcessMessageAsync(message, cancellationToken) Task
    }

    class PublishedItemMapper {
        -ILogger~PublishedItemMapper~ _logger
        +PublishedItemMapper(logger)
        +MapToDomain(dto) PublishedItem
        -ValidateAndParseItemId(itemIdString) Guid
        -ParseAssetIds(assetIdStrings) List~int~
    }

    class APIGateway {
        +SendRequest() Task
    }

    class ContentHubConnectionService {
        +GetAssetsByPublicLinks() Task
    }

    %% Business Layer
    class AssetItemController {
        -PublishAssetIdsByPublicLinksEventService _publishGetAssetIdsByPublicLinksEventService
        -DeltaCalculationService _deltaCalculationService
        -PublishPushToDamEventsService _publishPushToDamEventsService
        -ILogger~AssetItemController~ _logger
        +AssetItemController(publishService, deltaService, pushService, logger)
        +ProcessPublishedItemAsync(publishedItem, cancellationToken) Task
    }

    class DeltaCalculationService {
        -IAssetItemLinkRepository _assetItemLinkRepository
        -ILogger~DeltaCalculationService~ _logger
        +DeltaCalculationService(repository, logger)
        +CalculateDeltaAsync(item, newAssetIds, cancellationToken) Task~ItemAssetChanges~
        -GetCurrentAssetIdsAsync(itemId, cancellationToken) Task~List~int~~
        -CalculateDelta(currentAssetIds, newAssetIds) Tuple
        -ApplyChangesAsync(itemId, itemExists, newAssetIds, assetIdsToAdd, assetIdsToRemove, cancellationToken) Task
    }

    class PublishAssetIdsByPublicLinksEventService {
        -IMediator _mediator
        -IConfiguration _configuration
        -ILogger~PublishAssetIdsByPublicLinksEventService~ _logger
        +PublishAssetIdsByPublicLinksEventService(mediator, configuration, logger)
        +GetAssetIdsByPublicLinksAsync(publishedItem, cancellationToken) Task~List~int~~
        -FilterContentHubLinks(publicLinks) List~string~
    }

    class PublishPushToDamEventsService {
        -ILogger~PublishPushToDamEventsService~ _logger
        -IMediator _mediator
        +PublishPushToDamEventsService(logger, mediator)
        +PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken) Task
    }

    class PushToDamHandler {
        +Handle(event, cancellationToken) Task
    }

    %% Domain Models
    class PublishedItem {
        +Guid ItemId
        +string Language
        +string ItemName
        +int Version
        +string ItemPath
        +List~int~ AssetIds
        +List~string~ PublicLinks
        -PublishedItem(itemId, language, itemName, version, itemPath, assetIds, publicLinks)
        +Create(itemId, language, itemName, version, itemPath, assetIds, publicLinks)$ PublishedItem
        +GetUsageTrackingJson() JObject
    }

    class PublishedItemDto {
        +string ItemId
        +string Language
        +string ItemName
        +int Version
        +string ItemPath
        +List~string~ AssetIds
        +List~string~ PublicLinks
    }

    class ItemAssetChanges {
        +PublishedItem Item
        +List~int~ ToAddAssetIds
        +List~int~ ToRemoveAssetIds
    }

    class AssetItemLink {
        +Guid ItemId
        +List~int~ AssetIds
        +DateTime CreatedAt
        +DateTime UpdatedAt
    }

    %% Events
    class PushToDamEvent {
        +PublishedItem Item
        +List~int~ AssetIds
        +DamOperation Operation
    }

    %% Infrastructure
    class IAssetItemLinkRepository {
        <<interface>>
        +InsertAssetItemLinkAsync(itemId, assetIds, cancellationToken) Task~AssetItemLink~
        +GetAssetItemLinkByItemIdAsync(itemId, cancellationToken) Task~AssetItemLink~
        +GetAssetIdsFromItemIdAsync(itemId, cancellationToken) Task~List~int~~
        +GetItemIdsByAssetIdAsync(assetId, cancellationToken) Task~List~AssetItemLink~~
        +RemoveAssetIdsFromItemAsync(itemId, assetIds, cancellationToken) Task
        +AddAssetIdsToItemAsync(itemId, assetIds, cancellationToken) Task
        +RemoveItemAsync(itemId, cancellationToken) Task
    }

    class AssetItemLinkRepository {
        +InsertAssetItemLinkAsync(itemId, assetIds, cancellationToken) Task~AssetItemLink~
        +GetAssetItemLinkByItemIdAsync(itemId, cancellationToken) Task~AssetItemLink~
        +GetAssetIdsFromItemIdAsync(itemId, cancellationToken) Task~List~int~~
        +GetItemIdsByAssetIdAsync(assetId, cancellationToken) Task~List~AssetItemLink~~
        +RemoveAssetIdsFromItemAsync(itemId, assetIds, cancellationToken) Task
        +AddAssetIdsToItemAsync(itemId, assetIds, cancellationToken) Task
        +RemoveItemAsync(itemId, cancellationToken) Task
    }

    class IMediator {
        <<interface>>
        +PublishAsync~TEvent~(event, cancellationToken) Task
    }

    %% Relationships
    MessageHandler --> PublishedItemMapper : uses
    MessageHandler --> AssetItemController : uses
    PublishedItemMapper --> PublishedItemDto : maps from
    PublishedItemMapper --> PublishedItem : creates

    AssetItemController --> PublishAssetIdsByPublicLinksEventService : uses
    AssetItemController --> DeltaCalculationService : uses
    AssetItemController --> PublishPushToDamEventsService : uses
    AssetItemController --> PublishedItem : processes

    DeltaCalculationService --> IAssetItemLinkRepository : uses
    DeltaCalculationService --> PublishedItem : uses
    DeltaCalculationService --> ItemAssetChanges : creates

    PublishAssetIdsByPublicLinksEventService --> IMediator : uses
    PublishAssetIdsByPublicLinksEventService --> PublishedItem : uses

    PublishPushToDamEventsService --> IMediator : uses
    PublishPushToDamEventsService --> ItemAssetChanges : uses
    PublishPushToDamEventsService --> PushToDamEvent : publishes

    PushToDamHandler --> PushToDamEvent : handles

    AssetItemLinkRepository ..|> IAssetItemLinkRepository : implements
    AssetItemLinkRepository --> AssetItemLink : manages

    ItemAssetChanges --> PublishedItem : contains                                                                                                         
```

Publishing script diagram: 
```mermaid
classDiagram
    %% Sitecore Event Handler
    class PublishingEventHandler {
        -AssetExtractionService _assetExtractionService
        -PublishTelemetryService _telemetryService
        -PublishLoggingService _loggingService
        -AuditLoggingService _auditLoggingService
        -AssetUsageServiceClient _serviceClient
        +OnItemPublished(sender, args) void
        -CreateAssetUsageEvent(item, targetDatabase) AssetUsageEvent
        -SendEventToServiceBus(event) void
    }

    %% Services
    class AssetExtractionService {
        +ExtractAssetIds(item) List~string~
        +ExtractPublicLinks(item) List~string~
    }

    class PublishTelemetryService {
        +TrackPublishEvent(event) void
    }

    class PublishLoggingService {
        +LogEventCreation(event) void
    }

    class AuditLoggingService {
        +LogPublishActivity(event) void
    }

    %% Event Model
    class AssetUsageEvent {
        +List~string~ PublicLinks
        +string ItemId
        +string ItemPath
        +string ItemName
        +string Language
        +int Version
        +List~string~ AssetIds
    }
    
    %% External Dependency
    class AssetUsageServiceClient {
        <<service>>
        +SendAsync(event, cancellationToken) Task
    }

    %% Relationships
    PublishingEventHandler --> AssetExtractionService : uses
    PublishingEventHandler --> PublishTelemetryService : uses
    PublishingEventHandler --> PublishLoggingService : uses
    PublishingEventHandler --> AuditLoggingService : uses
    PublishingEventHandler --> AssetUsageServiceClient : uses
    PublishingEventHandler --> AssetUsageEvent : creates

    PublishTelemetryService --> AssetUsageEvent : tracks
    PublishLoggingService --> AssetUsageEvent : logs
    AuditLoggingService --> AssetUsageEvent : logs
```

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
               │     - Public links
               │     - Metadata
               ▼
┌─────────────────────────────────┐
│ AssetUsageServiceClient         │
│ SendAsync()                     │
└──────────────┬──────────────────┘
               │
               │ (4) HTTP POST to Asset Usage Service
               │     Endpoint: /api/SitecorePublishAPI
               │     Body: AssetUsageEvent {
               │       "itemId": "guid",
               │       "assetIds": ["123", "456"],
               │       "publicLinks": ["..."],
               │       "itemName": "...",
               │       "itemPath": "...",
               │       "language": "en",
               │       "version": 1
               │     }
               ▼
┌─────────────────────────────────┐
│ ASSET USAGE SERVICE             │
│ SitecorePublishAPI Function     │
└──────────────┬──────────────────┘
               │
               │ (5) Process publish event
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
               │     - Check payload
               ▼
┌─────────────────────────────────┐
│ MessageHandler                  │
│ HandleMessageAsync()            │
└──────────────┬──────────────────┘
               │
               │ (3) Map DTO to Domain
               ▼
┌─────────────────────────────────┐
│ PublishedItemMapper             │
│ MapToDomain()                   │
└──────────────┬──────────────────┘
               │
               │ (4) Process published item
               ▼
┌─────────────────────────────────┐
│ AssetItemController             │
│ ProcessPublishedItemAsync()     │
└──────────────┬──────────────────┘
               │
               │ (5) Get asset IDs from public links
               ▼
┌─────────────────────────────────┐
│ PublishAssetIdsByPublicLinks    │
│ EventService                    │
└──────────────┬──────────────────┘
               │
               │ (6) Calculate delta changes
               ▼
┌─────────────────────────────────┐
│ DeltaCalculationService         │
│ CalculateDeltaAsync()           │
└──────────────┬──────────────────┘
               │
               │ (7) Query existing relationships
               ▼
┌─────────────────────────────────┐
│ AssetItemLinkRepository         │
│ GetAssetItemLinkByItemIdAsync() │
└──────────────┬──────────────────┘
               │
               │ (8) Fetch from MongoDB
               ▼
┌─────────────────────────────────┐
│ MongoDB Database                │
│ AssetItemLinks Collection       │
└──────────────┬──────────────────┘
               │
               │ (9) Return existing data
               ▼
┌─────────────────────────────────┐
│ DeltaCalculationService         │
│ - Calculate added assets        │
│ - Calculate removed assets      │
└──────────────┬──────────────────┘
               │
               │ (10) Update MongoDB
               ▼
┌─────────────────────────────────┐
│ AssetItemLinkRepository         │
│ InsertAssetItemLinkAsync()      │
└──────────────┬──────────────────┘
               │
               │ (11) Publish DAM events
               ▼
┌─────────────────────────────────┐
│ PublishPushToDamEventsService   │
│ PublishPushToDamEventsAsync()   │
└──────────────┬──────────────────┘
               │
               │ (12) Handle DAM updates
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
│ Handle(PushToDamEvent)          │
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
│ (Future endpoint)               │
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
│    - Extract asset IDs          │
│    - Extract public links       │
│                                 │
│ 2. OnItemProcessed              │
│    - Create AssetUsageEvent     │
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
│    - Map to domain model        │
│                                 │
│ 2. Resolve Asset IDs            │
│    - Convert public links       │
│                                 │
│ 3. Query MongoDB                │
│    - Get current state          │
│                                 │
│ 4. Calculate Delta              │
│    - Compare old vs new         │
│    - Identify changes           │
│                                 │
│ 5. Update MongoDB               │
│    - Save new relationships     │
│    - Update timestamps          │
│                                 │
│ 6. Sync to ContentHub           │
│    - Publish DAM events         │
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

## Configuration

### Sitecore CMS Configuration

#### 1. Event Handler Configuration

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
  </sitecore>
</configuration>
```

#### 2. Asset Usage Service Configuration

Create a configuration file in:

`C:\inetpub\wwwroot\[SITECORE.INSTANCE]\App_Config\Include\AssetUsageService.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <settings>
      <!-- Azure Function endpoint for Asset Usage Service -->
      <setting name="AssetUsageService.ApiEndpoint" value="http://localhost:7183/api/SitecorePublishAPI" />
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

- **AssetUsageService.ApiEndpoint**: Asset Usage Service API endpoint (HTTP/HTTPS URL)

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

```json
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
  "itemName": "Home Page",
  "itemPath": "/sitecore/content/Home",
  "language": "en",
  "version": 1,
  "assetIds": ["34013", "13343", "23213"],
  "publicLinks": ["https://contenthub.example.com/api/public/content/...", "..."]
}
```

**Response**:
- `202 Accepted`: Event successfully queued for processing
- `400 Bad Request`: Invalid payload or processing error

**Response Body** (on error):
```json
{
  "error": "Error message describing the issue"
}
```

## Integration Points

### Sitecore CMS Integration

The `iO.Sitecore.Publishing` module captures publish events:

- **OnItemProcessing**: Triggered when item enters publish pipeline
- **OnItemProcessed**: Triggered after item is published - sends HTTP POST to Asset Usage Service
- **OnPublishEnd**: Triggered when publish operation completes
- **OnPublishEndRemote**: Triggered for remote publish events

### AssetUsageServiceClient

The client handles HTTP communication with the Asset Usage Service:

```csharp
public class AssetUsageServiceClient : IDisposable
{
    public async Task SendAsync(AssetUsageEvent payload, CancellationToken cancellationToken = default)
    {
        // Validates payload
        // Serializes to JSON
        // POSTs to configured endpoint
        // Handles errors and logging
    }
}
```

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
        
    Task RemoveAssetIdsFromItemAsync(
        Guid itemId, 
        List<int> assetIds, 
        CancellationToken cancellationToken = default);
        
    Task AddAssetIdsToItemAsync(
        Guid itemId, 
        List<int> assetIds, 
        CancellationToken cancellationToken = default);
        
    Task RemoveItemAsync(
        Guid itemId, 
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
# Edit local.settings.json with your configuration
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
- Copy `iO.Sitecore.publishing.dll` to your Sitecore instance bin folder
- Create `iO.Publishing.Events.config` in `App_Config\Include\zzz.iO\`
- Create `AssetUsageService.config` in `App_Config\Include\`
- Update `AssetUsageService.ApiEndpoint` to point to your local function
- Restart Sitecore

### Project Structure

## Deployment

This guide covers deploying the .NET 8 isolated Azure Function that integrates with Sitecore Content Hub and Cosmos DB for MongoDB (vCore).

### Prerequisites

Before deploying the Asset Usage Service, you must provision the following Azure resources. These resources are referenced throughout the deployment configuration and **must be created first**.

#### Required Azure Resources

Based on your Azure environment, ensure the following resources exist:

| Resource | Type | Purpose | Creation Guide |
|----------|------|---------|----------------|
| **Subscription** | Azure Subscription | Container for all Azure resources | Managed at organization level |
| **Resource Group** | Resource Group | Logical container for related resources | [Create via CLI](https://learn.microsoft.com/en-us/azure/key-vault/general/quick-create-cli#create-a-resource-group) |
| **Function App** | Azure Functions (Linux, .NET 8 isolated) | Hosts the Asset Usage Service microservice | [Create via Portal](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-your-first-function-visual-studio) or [Create via CLI](https://learn.microsoft.com/en-us/azure/azure-functions/how-to-create-function-azure-cli) |
| **Key Vault** | Azure Key Vault | Secure storage for secrets (connection strings, credentials) | [Create via Portal](https://learn.microsoft.com/en-us/azure/key-vault/general/quick-create-portal) |
| **Cosmos DB** | Azure Cosmos DB for MongoDB (vCore) | Database for asset-item relationship storage | [Create vCore Cluster](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/quickstart-portal) |
| **Application Insights** | Application Insights | Monitoring, logging, and telemetry | Auto-created with Function App or [create separately](https://learn.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource) |

#### Resource Naming Convention (Example)

Based on your Azure environment, here's an example naming pattern:

    Subscription:        Assets Usage Microservices
    Resource Group:      asset-usage-microservices
    Function App:        asset-usage-fa
    Key Vault:           asset-usage-kv
    Cosmos DB:           asset-usage-mongo
    Application Insights: asset-usage-ai

> **Note**: Adjust names to match your organization's naming standards. Ensure names are globally unique where required (Function App, Key Vault, Cosmos DB).

#### Step-by-Step Resource Provisioning

**1. Create Resource Group**

   ```az group create --name asset-usage-microservices --location westeurope```

   📘 [Documentation](https://learn.microsoft.com/en-us/azure/key-vault/general/quick-create-cli#create-a-resource-group)

**2. Create Key Vault**
   - Navigate to Azure Portal → Create a resource → Key Vault
   - Set resource group, name, and region
   - Enable RBAC authorization (recommended)
   
   📘 [Documentation](https://learn.microsoft.com/en-us/azure/key-vault/general/quick-create-portal)

**3. Create Cosmos DB for MongoDB (vCore)**
   - Navigate to Azure Portal → Create a resource → Azure Cosmos DB
   - Select **Azure Cosmos DB for MongoDB** → **vCore cluster**
   - Configure cluster tier and credentials
   
   📘 [Documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/quickstart-portal)
   
   📘 [Connection Troubleshooting](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

**4. Create Function App**
   - Navigate to Azure Portal → Create a resource → Function App
   - **Runtime**: .NET 8 (isolated)
   - **Operating System**: Linux
   - **Plan Type**: Flex Consumption (recommended) or Consumption
   
   📘 [Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal?tabs=core-tools&pivots=flex-consumption-plan)

**5. Create Application Insights** (if not auto-created)
   - Usually created automatically with Function App
   - If manual creation needed: Azure Portal → Create a resource → Application Insights
   
   📘 [Documentation](https://learn.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource)

---

### Overview

All environment-specific values are provided via Azure Key Vault and GitHub Secrets; nothing is hard-coded in source.

> **Note**: Double-underscore (`__`) in app settings maps to `:` for .NET configuration.

### Required App Settings

- `ContentHub__Endpoint`
- `ContentHub__ClientId`
- `ContentHub__ClientSecret`
- `MongoDb__ConnectionString` (note the capital S)
- `MongoDB__DatabaseName`

### Deployment Values Checklist

Before proceeding with deployment, gather the following values from the resources you created:

- `<AZ_SUBSCRIPTION_ID>` - Your Azure subscription ID
- `<AZ_TENANT_ID>` - Your Azure tenant ID
- `<AZ_CLIENT_ID>` - GitHub OIDC service principal client ID
- `<RESOURCE_GROUP>` - Azure resource group name (e.g., `asset-usage-microservices`)
- `<FUNCTION_APP_NAME>` - Function app name (e.g., `asset-usage-fa`)
- `<KEYVAULT_NAME>` - Key vault name (e.g., `asset-usage-kv`)
- `<MONGO_CONNECTION_STRING>` - SRV format; tls=true; SCRAM-SHA-256; optionally authSource=admin
- `<MONGO_DATABASE_NAME>` - Logical database name (e.g., `asset-usage`)
- `<CONTENTHUB_ENDPOINT>` - ContentHub endpoint (e.g., `https://your-env.sitecoresandbox.cloud`)
- `<CONTENTHUB_CLIENT_ID>` - ContentHub client ID
- `<CONTENTHUB_CLIENT_SECRET>` - ContentHub client secret

### Step 1: Prepare Azure Resources and Identity

1. **Enable System-Assigned Managed Identity** on your Function App:
   - Navigate to Function App → Settings → Identity
   - Under "System assigned" tab, set Status → **On**
   - Save and note the Object (principal) ID

2. **Grant Key Vault access** to the Function App's managed identity:
   - **If using Azure RBAC** (recommended): 
     - Navigate to Key Vault → Access control (IAM) → Add role assignment
     - Select **Key Vault Secrets User** role
     - Assign to the Function App's managed identity
     
     📘 [RBAC Guide](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)
     
     📘 [Built-in Roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#security)
   
   - **If using Access Policies**: 
     - Navigate to Key Vault → Access policies → Create
     - Grant **Get** and **List** permissions for secrets

3. **Configure Cosmos DB Networking**:
   - Navigate to Cosmos DB → Networking
   - Add your deployment environment's IP addresses or enable Azure service access
   
   📘 [Troubleshooting Guide](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

### Step 2: Create Key Vault Secrets

Navigate to your Key Vault and create the following secrets:

1. Go to Key Vault → Secrets → Generate/Import
2. Create these versionless secrets:

   - **Name**: `MongoDbConnectionString`  
     **Value**: `<MONGO_CONNECTION_STRING>`  
     (Format: `mongodb+srv://<user>:<password>@<cluster>.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000`)
   
   - **Name**: `ContentHub-ClientId`  
     **Value**: `<CONTENTHUB_CLIENT_ID>`
   
   - **Name**: `ContentHub-ClientSecret`  
     **Value**: `<CONTENTHUB_CLIENT_SECRET>`

📘 [Add Secrets Documentation](https://learn.microsoft.com/en-us/azure/key-vault/secrets/quick-create-portal)

### Step 3: Configure Function App Settings

Navigate to Function App → Settings → Configuration → Application settings

Set the following application settings (no secrets in plain text):

    ContentHub__Endpoint = <CONTENTHUB_ENDPOINT>
    ContentHub__ClientId = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/ContentHub-ClientId)
    ContentHub__ClientSecret = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/ContentHub-ClientSecret)
    MongoDb__ConnectionString = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/MongoDbConnectionString)
    MongoDB__DatabaseName = <MONGO_DATABASE_NAME>

📘 [Key Vault References Documentation](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)

**Save** the configuration and **restart** the Function App.

**Verify** in Kudu (`https://<FUNCTION_APP_NAME>.scm.azurewebsites.net/Env`) that:
- Key Vault references show `[Hidden Credential]` or similar
- `WEBSITE_KEYVAULT_REFERENCES` section shows status **Resolved**

### Step 4: Set Up CI/CD with GitHub Actions

#### Option A: Automatic Setup via Azure Portal (Recommended)

1. Navigate to Function App → Deployment Center
2. Select **GitHub** as the source
3. Authorize Azure to access your GitHub account
4. Select your repository and branch
5. Azure will automatically:
   - Create the workflow file in `.github/workflows/`
   - Configure OIDC authentication
   - Add necessary secrets to your repository

📘 [Continuous Deployment Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-continuous-deployment)

#### Option B: Manual Setup

1. **Create GitHub Repository Secrets**:
   - Navigate to GitHub repository → Settings → Secrets and variables → Actions
   - Add the following repository secrets:
     - `AZURE_SUBSCRIPTION_ID` = `<AZ_SUBSCRIPTION_ID>`
     - `AZURE_TENANT_ID` = `<AZ_TENANT_ID>`
     - `AZURE_CLIENT_ID` = `<AZ_CLIENT_ID>`

📘 [Azure Functions Action Repository](https://github.com/Azure/functions-action)

> **Notes**:
> - The Sitecore NuGet feed step is **required** for building this project

### Step 5: Configure Sitecore to Use Your Function

#### Recommended Approach (No Secrets in URL)

Keep the endpoint clean; send the function key via HTTP header `x-functions-key`.

**Sitecore Configuration**:

    <?xml version="1.0" encoding="utf-8"?>
    <configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
      <sitecore>
        <settings>
          <setting name="AssetUsageService.ApiEndpoint" 
                   value="https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI" />
          <setting name="AssetUsageService.FunctionKey" 
                   value="<FUNCTION_KEY>" />
        </settings>
      </sitecore>
    </configuration>

Store the function key outside source control (e.g., Sitecore Secret Manager/Key Vault or appSetting).

When calling, your HTTP client should set header: `x-functions-key: <FUNCTION_KEY>`

📘 [Function Keys Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/function-keys-how-to)

📘 [HTTP Trigger Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger)

#### Legacy Approach (Query String)

If you must embed the code in the query string:

1. **Get the function-level key** (not the master host key):

    ```az functionapp function keys list \
      -g <RESOURCE_GROUP> \
      -n <FUNCTION_APP_NAME> \
      --function-name SitecorePublishAPI \
      --query "default" -o tsv```

2. **Update Sitecore configuration**:

    <?xml version="1.0" encoding="utf-8"?>
    <configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
      <sitecore>
        <settings>
          <setting name="AssetUsageService.ApiEndpoint" 
                   value="https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI?code=<FUNCTION_KEY>" />
        </settings>
      </sitecore>
    </configuration>

### Step 6: Smoke Test

1. Navigate to Azure Portal → Function App → Functions → **SitecorePublishAPI** → **Test/Run**

2. Set header:

       Content-Type: application/json

3. Use this test body:

    ```json
      "ItemId": "110d559f-dea5-42ea-9c1c-8a5df7e70ef9",
      "Language": "en",
      "ItemName": "Home",
      "Version": 1,
      "ItemPath": "/sitecore/content/Home",
      "AssetIds": [],
      "PublicLinks": []
    ```

4. Click **Run** and expect `200`/`202` response

5. **Verify in Application Insights**:
   - Navigate to Application Insights → Transaction search
   - Look for recent requests to `/api/SitecorePublishAPI`
   - Dependencies should show successful calls to Content Hub and MongoDB
   - Check for any exceptions or failures

6. **Verify in Cosmos DB**:
   - Connect using MongoDB Compass or mongosh
   - Check the `<MONGO_DATABASE_NAME>` database
   - Verify the `AssetItemLinks` collection contains the test record

📘 [Connect with MongoDB Compass](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/connect-using-compass)

### Troubleshooting

#### Key Vault Resolution Issues

**Symptom**: App settings show `[Hidden Credential]` but function logs show "configuration not found"

**Solution**:
- After changing any secret version, restart the Function App or re-save an app setting to force immediate re-resolution
- Check that the Function App's managed identity has `Key Vault Secrets User` role
- Verify the SecretUri format: `@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>)`
- Do not include version in SecretUri for automatic rotation

📘 [Key Vault References](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)

📘 [RBAC Guide](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

#### SASL Authentication Errors

**Symptom**: `MongoAuthenticationException: SASL authentication failed`

**Solution**:
- Ensure your connection string uses `mongodb+srv://` protocol (SRV format)
- Include `tls=true` in connection string
- Add `authSource=admin` if your user is in the admin database
- Confirm hostname matches the vCore SRV endpoint (`.mongocluster.cosmos.azure.com`)
- Test connection string locally using mongosh before adding to Key Vault

Example connection string:

    mongodb+srv://<user>:<password>@<cluster>.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000

📘 [Troubleshooting Common Issues](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

#### Missing "ContentHub:Endpoint" Error

**Symptom**: Application logs show `Configuration key 'ContentHub:Endpoint' not found`

**Solution**:
- Verify `ContentHub__Endpoint` is set in Function App configuration (note double underscore)
- Ensure `ClientId`/`ClientSecret` are present as Key Vault references
- Restart the Function App after making configuration changes
- Check that Key Vault references show status "Resolved" in Kudu `/Env`

#### Verify Settings in Kudu

Navigate to Kudu diagnostics console: `https://<FUNCTION_APP_NAME>.scm.azurewebsites.net/Env`

Look for:
- Key Vault-backed settings should show `[Hidden Credential]` or similar
- `WEBSITE_KEYVAULT_REFERENCES` section should show:

  ```json
  {
    "status": "Resolved",
    "details": {
      "ContentHub__ClientId": { "status": "Resolved" },
      "ContentHub__ClientSecret": { "status": "Resolved" },
      "MongoDb__ConnectionString": { "status": "Resolved" }
    }
  }
  ```

#### Network Connectivity Issues

**Symptom**: Function cannot reach Cosmos DB or Content Hub

**Solution**:
- For **Cosmos DB**: Check Networking → Firewall settings → Add Function App's outbound IPs
- For **Content Hub**: Verify endpoint URL is accessible from Azure
- Test connectivity using Kudu → Debug console → PowerShell: `Test-NetConnection <hostname> -Port 443`

📘 [Cosmos DB vCore Networking](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

### Visual Studio Publish (Optional, One-Time)

For initial deployment or quick updates:

1. Right-click your Function project in Visual Studio
2. Select **Publish**
3. Choose **Azure** → **Azure Function App (Linux)**
4. Select your subscription and `<FUNCTION_APP_NAME>`
5. Click **Publish**

📘 [Visual Studio Quickstart](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-your-first-function-visual-studio)

> **Note**: Use the CI/CD workflow for ongoing deployments. Visual Studio publish is useful for initial setup or emergency hotfixes.

### Additional Resources

- **ARM/Bicep Templates**: [Provision via Infrastructure as Code](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-first-function-resource-manager)
- **Entra ID Authentication**: [Configure OIDC for Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/how-to-configure-entra-authentication)
- **Multiple Environments**: Use GitHub Environments feature to separate dev/stage/prod configurations

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run performance tests only
dotnet test --filter "Category=Performance"

# Run integration tests only
dotnet test --filter "FullyQualifiedName~Integration"
```

### Test Categories

- **Unit Tests**: Business logic and service tests
- **Integration Tests**: ContentHub and MongoDB integration
- **Performance Tests**: Repository performance benchmarks

### Example Test Data

The service includes seed data for testing (when `MongoDB:SeedData` is `true`):

```csharp
new AssetItemLink
{
    ItemId = Guid.NewGuid(),
    AssetIds = new List<int>{34013, 13343},
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
}
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

### Sitecore Logging

The `AssetUsageServiceClient` logs all activities to Sitecore logs:

- Event creation and validation
- HTTP request/response details
- Error conditions and warnings

## Troubleshooting

### Sitecore Integration Issues

- Verify `iO.Sitecore.Publishing.dll` is in the bin folder
- Check Sitecore logs for event handler errors
- Confirm `AssetUsageService.ApiEndpoint` is accessible from Sitecore server
- Validate endpoint URL format (must include http:// or https://)
- Test endpoint connectivity using curl or Postman

### MongoDB Connection Failures

- Verify connection string format
- Check network connectivity and firewall rules
- Ensure MongoDB version compatibility (4.0+)
- Validate database and collection names

### ContentHub Authentication Errors

- Validate ClientId and ClientSecret
- Check OAuth2 permissions in ContentHub
- Verify endpoint URL format (must include https://)
- Test connectivity using `IsContentHubReachableAsync()`

### Common Error Messages

**"AssetUsageService.ApiEndpoint setting is required but not configured"**
- Solution: Add the `AssetUsageService.ApiEndpoint` setting to `AssetUsageService.config`

**"Payload ItemId is null or empty; skipping send"**
- Solution: Verify the item being published has a valid GUID

**"HTTP request failed for ItemId"**
- Solution: Check Azure Function logs for detailed error information
- Verify the function is running and accessible
- Check Application Insights for request traces
