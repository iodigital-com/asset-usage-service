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
- [ContentHub settings](#content-hub-settings)
- [Contenthub React Component Setup](#content-hub-react-components-setup)
- [Development Setup](#development-setup)
- [Initial Migration Tool](#initial-migration-tool)
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
var client = await contentHubConnectionService.GetContentHubClientAsync();
var isReachable = await contentHubConnectionService.IsContentHubReachableAsync();
```

## Initial Migration Tool

For existing Sitecore instances with Content Hub assets already in use, you need to perform an initial migration to populate the AssetUsageService database.

### Prerequisites

- Asset Usage Service deployed and running
- iO.Sitecore.Publishing module installed
- AssetUsageService.config properly configured

### Installation Steps

#### 1. Add Migration Files

Place the following files in your Sitecore instance:

<details>
<summary><strong>MigrateAssets.html</strong> - Click to expand code</summary>

**Location**: `C:\inetpub\wwwroot\[YOUR_SITECORE_INSTANCE]\sitecore\admin\MigrateAssets.html`

```html
<!DOCTYPE html>
<html>
<head>
    <title>Asset Link Migration</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; }
        .btn { padding: 12px 24px; background: #007acc; color: white; border: none; cursor: pointer; font-size: 16px; border-radius: 4px; }
        .btn:hover { background: #005a9e; }
        .btn:disabled { background: #ccc; cursor: not-allowed; }
        .progress-container { margin-top: 20px; display: none; }
        .progress-bar-bg { width: 100%; height: 30px; background: #e0e0e0; border-radius: 4px; overflow: hidden; }
        .progress-bar { height: 100%; background: #007acc; transition: width 0.3s; display: flex; align-items: center; justify-content: center; color: white; font-weight: bold; }
        .stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 15px; margin-top: 20px; }
        .stat-box { background: #f5f5f5; padding: 15px; border-radius: 4px; text-align: center; }
        .stat-value { font-size: 32px; font-weight: bold; color: #007acc; }
        .stat-label { font-size: 14px; color: #666; margin-top: 5px; }
        .current-item { margin-top: 15px; padding: 10px; background: #f8f9fa; border-left: 4px solid #007acc; font-family: monospace; font-size: 12px; word-break: break-all; }
        .error { background: #f8d7da; color: #721c24; padding: 15px; border-radius: 4px; margin-top: 20px; }
        .success { background: #d4edda; color: #155724; padding: 15px; border-radius: 4px; margin-top: 20px; }
        .warning { background: #fff3cd; color: #856404; padding: 15px; border-radius: 4px; margin-top: 20px; }
    </style>
</head>
<body>
    <h1>Initial Asset Link Migration</h1>
    <p>This will process all items from the Web database and send them to the Asset Usage Service.</p>
    
    <button id="btnStart" class="btn" onclick="startMigration()">Start Migration</button>
    
    <div id="progressContainer" class="progress-container">
        <div class="progress-bar-bg">
            <div id="progressBar" class="progress-bar" style="width: 0%">0%</div>
        </div>
        
        <div class="stats">
            <div class="stat-box">
                <div class="stat-value" id="processedItems">0</div>
                <div class="stat-label">Processed</div>
            </div>
            <div class="stat-box">
                <div class="stat-value" id="successCount" style="color: #28a745;">0</div>
                <div class="stat-label">Success</div>
            </div>
            <div class="stat-box">
                <div class="stat-value" id="failureCount" style="color: #dc3545;">0</div>
                <div class="stat-label">Failed</div>
            </div>
        </div>
        
        <div class="current-item">
            <strong>Current Item:</strong><br>
            <span id="currentItem">-</span>
        </div>
        
        <div style="margin-top: 15px; color: #666;">
            <strong>Duration:</strong> <span id="duration">0s</span>
        </div>
    </div>
    
    <div id="errorMessage" class="error" style="display: none;"></div>
    <div id="warningMessage" class="warning" style="display: none;"></div>
    <div id="successMessage" class="success" style="display: none;"></div>
    
    <script>
        let pollInterval;
        let pollCount = 0;
        const MAX_POLLS = 5;
        
        function startMigration() {
            document.getElementById('btnStart').disabled = true;
            document.getElementById('progressContainer').style.display = 'block';
            document.getElementById('errorMessage').style.display = 'none';
            document.getElementById('warningMessage').style.display = 'none';
            document.getElementById('successMessage').style.display = 'none';
            
            pollCount = 0;
            
            fetch('/sitecore/admin/MigrationHandler.ashx?action=start', { method: 'POST' })
                .then(response => response.json())
                .then(data => {
                    console.log('Migration started:', data);
                    startPolling();
                })
                .catch(error => {
                    document.getElementById('errorMessage').textContent = 'Failed to start migration: ' + error.message;
                    document.getElementById('errorMessage').style.display = 'block';
                    document.getElementById('btnStart').disabled = false;
                });
        }
        
        function startPolling() {
            pollInterval = setInterval(checkStatus, 1000);
        }
        
        function stopPolling() {
            if (pollInterval) {
                clearInterval(pollInterval);
                pollInterval = null;
            }
        }
        
        function checkStatus() {
            pollCount++;
            
            fetch('/sitecore/admin/MigrationHandler.ashx?action=status')
                .then(response => response.json())
                .then(data => {
                    console.log('Status check #' + pollCount + ':', data);
                    updateUI(data);
                    
                    // Stop polling if migration is not running
                    if (!data.isRunning) {
                        // Give it a few polls to see if items appear
                        if (pollCount > MAX_POLLS) {
                            stopPolling();
                            
                            if (data.totalItems === 0) {
                                // No items found
                                if (data.errorMessage) {
                                    document.getElementById('warningMessage').textContent = 
                                        'Migration completed but no items were processed: ' + data.errorMessage;
                                    document.getElementById('warningMessage').style.display = 'block';
                                } else {
                                    document.getElementById('warningMessage').textContent = 
                                        'No items with Content Hub links found. Check Sitecore logs for details.';
                                    document.getElementById('warningMessage').style.display = 'block';
                                }
                                document.getElementById('btnStart').disabled = false;
                            } else if (data.processedItems > 0) {
                                // Migration completed with items
                                showCompletion(data);
                            }
                        }
                    } else {
                        // Reset poll count if migration is still running
                        pollCount = 0;
                    }
                })
                .catch(error => {
                    console.error('Status check failed:', error);
                    stopPolling();
                    document.getElementById('errorMessage').textContent = 'Status check failed: ' + error.message;
                    document.getElementById('errorMessage').style.display = 'block';
                    document.getElementById('btnStart').disabled = false;
                });
        }
        
        function updateUI(data) {
            document.getElementById('progressBar').style.width = data.progressPercentage + '%';
            document.getElementById('progressBar').textContent = data.progressPercentage + '%';
            document.getElementById('processedItems').textContent = data.processedItems + ' / ' + data.totalItems;
            document.getElementById('successCount').textContent = data.successCount;
            document.getElementById('failureCount').textContent = data.failureCount;
            document.getElementById('currentItem').textContent = data.currentItem || '-';
            document.getElementById('duration').textContent = data.durationSeconds + 's';
            
            if (data.errorMessage && data.totalItems > 0) {
                document.getElementById('errorMessage').textContent = 'Error: ' + data.errorMessage;
                document.getElementById('errorMessage').style.display = 'block';
            }
        }
        
        function showCompletion(data) {
            document.getElementById('btnStart').disabled = false;
            
            if (data.errorMessage) {
                document.getElementById('errorMessage').textContent = 'Migration completed with errors. Check logs for details.';
                document.getElementById('errorMessage').style.display = 'block';
            } else {
                document.getElementById('successMessage').textContent = 
                    'Migration completed successfully! Processed ' + data.totalItems + ' items in ' + data.durationSeconds + 's (Success: ' + data.successCount + ', Failed: ' + data.failureCount + ')';
                document.getElementById('successMessage').style.display = 'block';
            }
        }
    </script>
</body>
</html>
```

</details>

<details>
<summary><strong>MigrationHandler.ashx</strong> - Click to expand code</summary>

**Location**: `C:\inetpub\wwwroot\[YOUR_SITECORE_INSTANCE]\sitecore\admin\MigrationHandler.ashx`

```csharp
<%@ WebHandler Language="C#" Class="MigrationHandler" %>

using System;
using System.Web;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using iO.Sitecore.Publishing.Services;
using Sitecore.Diagnostics;

public class MigrationHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        
        var action = context.Request.QueryString["action"];
        
        if (action == "start")
        {
            StartMigration(context);
        }
        else if (action == "status")
        {
            GetStatus(context);
        }
        else
        {
            context.Response.StatusCode = 400;
            context.Response.Write("{\"error\":\"Invalid action\"}");
        }
    }
    
    private void StartMigration(HttpContext context)
    {
        if (MigrationProgressTracker.IsRunning)
        {
            context.Response.StatusCode = 400;
            context.Response.Write("{\"error\":\"Migration is already running\"}");
            return;
        }
        
        Log.Info("[MigrationHandler] Starting migration via Task.Run", this);
        
        Task.Run(async () =>
        {
            try
            {
                Log.Info("[MigrationHandler] Inside Task.Run - about to create service", this);
                
                var service = new InitialItemAssetLinkService();
                
                Log.Info("[MigrationHandler] Service created, calling ExecuteMigrationAsync()", this);
                
                await service.ExecuteMigrationAsync();
                
                Log.Info("[MigrationHandler] ExecuteMigrationAsync() completed", this);
            }
            catch (Exception ex)
            {
                Log.Error("[MigrationHandler] EXCEPTION in Task.Run", ex, this);
                MigrationProgressTracker.ErrorMessage = ex.Message + " | " + ex.StackTrace;
                MigrationProgressTracker.IsRunning = false;
                MigrationProgressTracker.EndTime = DateTime.UtcNow;
            }
        });
        
        Log.Info("[MigrationHandler] Task.Run launched", this);
        
        context.Response.Write("{\"message\":\"Migration started\"}");
    }
    
    private void GetStatus(HttpContext context)
    {
        var duration = MigrationProgressTracker.StartTime.HasValue
            ? (MigrationProgressTracker.EndTime ?? DateTime.UtcNow) - MigrationProgressTracker.StartTime.Value
            : TimeSpan.Zero;
        
        var serializer = new JavaScriptSerializer();
        var status = new
        {
            isRunning = MigrationProgressTracker.IsRunning,
            totalItems = MigrationProgressTracker.TotalItems,
            processedItems = MigrationProgressTracker.ProcessedItems,
            successCount = MigrationProgressTracker.SuccessCount,
            failureCount = MigrationProgressTracker.FailureCount,
            progressPercentage = MigrationProgressTracker.ProgressPercentage,
            currentItem = MigrationProgressTracker.CurrentItem ?? "",
            errorMessage = MigrationProgressTracker.ErrorMessage ?? "",
            durationSeconds = (int)duration.TotalSeconds
        };
        
        context.Response.Write(serializer.Serialize(status));
    }
    
    public bool IsReusable
    {
        get { return false; }
    }
}
```

</details>

#### 2. Update AssetUsageService.config

Add the ContentHub endpoint setting to your existing `AssetUsageService.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <settings>
      <!-- Existing setting -->
      <setting name="AssetUsageService.ApiEndpoint" value="http://localhost:7183/api/SitecorePublishAPI" />
      
      <!-- NEW: Add this setting -->
      <setting name="AssetUsageService.ContentHubEndpoint" value="https://your-instance.sitecoresandbox.cloud" />
    </settings>
  </sitecore>
</configuration>
```

Replace `https://your-instance.sitecoresandbox.cloud` with your actual Content Hub endpoint.

#### 3. Run the Migration

1. Navigate to: `https://[YOUR_SITECORE_INSTANCE]/sitecore/admin/MigrateAssets.html`
2. Click **Start Migration**
3. Monitor progress in real-time:
   - Total items processed
   - Success/failure counts
   - Current item being processed
   - Duration

#### 4. Verify Migration

After completion:

1. **Check MongoDB**: Verify `AssetItemLinks` collection contains records
2. **Check Content Hub**: Verify asset relations are updated
3. **Review Sitecore Logs**: Check for any errors or warnings

### Migration Behavior

The migration tool:
- Scans the entire Sitecore Web database
- Identifies items with Content Hub asset references
- Sends each item to the Asset Usage Service
- Updates progress in real-time via AJAX polling
- Handles errors gracefully and logs them

### Troubleshooting

**No items found**:
- Verify items in Sitecore actually have Content Hub asset links
- Check that `AssetUsageService.ContentHubEndpoint` is correct
- Review Sitecore logs for detailed error messages

**Migration fails mid-process**:
- Check Asset Usage Service logs in Application Insights
- Verify MongoDB connectivity
- Check Content Hub authentication credentials

**Timeout errors**:
- For large databases, the migration may take several minutes
- Monitor Sitecore logs for progress
- Consider running during off-peak hours

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

 ## Setting up the React Component

This guide will show you how to add the React component to your Content Hub instance.

### Prerequisites

- Access to the asset-usage-service project folder
- Node.js and npm installed
- Admin access to your Content Hub instance
- Your Sitecore XP URL

### Installation Steps

#### 1. Install Dependencies

Open your terminal and navigate to the component directory:

```bash
cd asset-usage-service/contenthubtrackingcomponent
npm install
```

#### 2. Configure the Component

1. Open the file `src/AssetUsageTracker.tsx` in your preferred editor
2. Replace the constant `CMS_BASE_URL` with your Sitecore XP URL
3. Save the file

#### 3. Build the Component

Run the build command:

```bash
npm run build
```

This will generate an `AssetUsageTracker.js` file in the `dist` folder.

#### 4. Upload to Content Hub

1. Log in to your Content Hub instance
2. Navigate to **Manage** (settings icon)
3. Go to the **Portal assets** page
4. Click **Upload file** and upload the `AssetUsageTracker.js` file from the `dist` folder

#### 5. Wait for Processing

1. Click on your profile picture
2. Open **Background processes**
3. Refresh the page and wait until the upload job is processed

#### 6. Add Component to Asset Details Page

1. Go to **Manage** → **Pages**
2. Select the **Asset details** page
3. Click **+ Component** where you want to add the component
4. In the "Add component" popup, search for **External**
5. Click **Add**

#### 7. Configure the Component

1. Give it a title (e.g., "AssetUsageTracker")
2. Turn the **Visible** switch **on**
3. Click on the component you just added
4. Under **JS bundle**, select **From asset**
5. Click the **+** icon
6. Search for the `AssetUsageTracker.js` file
7. Select it and click **Save**
8. Click **Save** in the upper right corner of the page

### Verification

The Asset Usage Tracker component should now be visible on your Asset details page and ready to use.

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


## Content Hub settings 
This configuration is required for the Asset Tracking microservice to securely connect to Sitecore Content Hub and update asset usage data.  
Without this setup, the microservice cannot authenticate safely and modify usage tracking information.

This section explains how to:
1. Create a minimal-permission service user
2. Assign permissions via a custom user group
3. Create an OAuth client (Client Credentials flow)
4. Extend the M.Asset schema with a secured JSON field
5. Apply read/write member-level security

---

### 1. Create Service User (Minimal Required Permissions)

#### 1.1 Create the User
1. Log in to Sitecore Content Hub.
2. Navigate: Manage > Users.
3. Click User to open the user list.
4. Click + User.
5. Enter a username (example: asset-service-user).
6. Click Save.
7. Click Edit profile and add a valid Email.
8. Open the email inbox and complete verification.
9. After verifying, use the reset password link to set an initial password.

#### 1.2 Create a User Group
1. Navigate: Manage > Users > User groups.
2. Click + Usergroup.
3. Fill in:
   - Name: Asset Editors Service (example)
   - Modules: Media (or required module granting asset access)
4. Click the User field + button.
5. Search and add asset-service-user.
6. Click Save.

#### 1.3 Configure User Group Policy
1. From the User groups overview, locate the newly created group.
2. Click Policies on that group.
3. Click New rule.
4. Entity Definition: select M.Asset.
5. Permissions (check only what you need):
   - Read
   - Create
   - Update
   - AddVersion
   - ReadPublicLinks
   - Delete (only if deletion is required; omit if not)
6. (Optional) Add conditions to restrict scope (e.g., folder, metadata).
7. Click Save.

#### 1.4 Verify Group Memberships
1. Go to Manage > Users.
2. Open asset-service-user.
3. Go to the User groups tab.
4. Ensure membership includes:
   - Everyone (usually automatic)
   - Asset Editors Service
5. Click Save.

#### 1.5 Test With Impersonation
1. Open the user details for asset-service-user.
2. Click Impersonate.
3. Verify:
   - Can view assets.
   - Can edit or create assets (as per granted permissions).
   - Does not have access to administrative modules beyond scope.
4. Click Stop impersonating to return.

---

### 2. Create OAuth Client (Client Credentials Flow)

#### 2.1 Create OAuth Client
1. Navigate: Manage > OAuth clients.
2. Click OAuth client.
3. Fill in:
   - Name: Asset Service Client
   - Client ID: asset-service-client
   - Client Secret: (generate a strong secret; copy it immediately)
   - Redirect URL: https://localhost/ (placeholder; not used for client credentials)
   - Type: Client Credentials
   - User: select asset-service-user
4. Click Save.

Important:
- You will not be able to retrieve the Client Secret later. Store it safely.

---

### 3. Extend M.Asset Schema With a Secured JSON Property

Goal: Add a JSON property (UsageTracking) that is readable by all but writable only by a designated group/service user.

#### 3.1 Open Schema
1. Navigate: Manage > Schema.

#### 3.2 Locate M.Asset
1. Use search: M.Asset.
2. Select M.Asset.

#### 3.3 Create a Member Group
1. Click New group.
2. Name: UsageTracking (!important to keep it UsageTracking or the react interface wont notice it).
3. Click Save.

#### 3.4 Add a New Property Member
1. Inside the group, click New member.
2. In the New member dialog:
   - Next to Property click Select.
   - Choose Data type: JSON.
3. Click Next and configure:
   - Name: UsageTracking (!important to keep it UsageTracking or the react interface wont notice it).
   - Allow Updates: checked
   - Secured: checked
4. Click Save.

---

### 4. Member-Level Security: Read Access for Everyone

Goal: All users can view the field; only specific group can modify.

1. Navigate: Manage > Users > User groups.
2. Open Everyone.
3. Click Policies.
4. Go to Member security tab.
5. Definitions: select M.Asset.
6. Member groups: select UsageTracking.
7. Members: find property UsageTracking.
8. Check only Read.
9. Click Save.

---

### 5. Member-Level Security: Write Access for Service Group

1. Navigate: Manage > Users > User groups.
2. Open the Asset Editors Service.
3. Click Policies.
4. Go to Member security tab.
5. Definitions: select M.Asset.
6. Member groups: select UsageTracking.
7. Members: select UsageTracking property.
8. Check Read and Write.
9. Click Save.

---

### 6. Result Verification

Expected outcome:
- Everyone: can see UsageTracking (read-only).
- Asset Editors Service (and impersonated service user): can read and update UsageTracking.
- The property appears under the UsageTracking member group on M.Asset entities.

---

### 7. Troubleshooting

| Symptom | Cause | Resolution |
|---------|-------|-----------|
| Property not visible | Member group security misconfiguration | Re-check Everyone group member security (Read) |
| Cannot update property as service user | Missing Write at member level | Verify Asset Editors Service policy member security |
| OAuth calls fail (401) | Wrong client secret or user missing permissions | Recreate secret or adjust user group permissions |

---

### Summary

You have:
- A scoped service user with only necessary asset permissions.
- An OAuth client using Client Credentials tied to that user.
- A secured JSON property on M.Asset with controlled read/write access.
- A foundation for storing and exposing asset usage metadata safely.

## Content Hub React Components Setup

This section describes how to add the usage insights and the custom delete Modal component to the asset details page:
1. Asset Usage Tracker (shows which Sitecore CMS items use the asset)
2. Custom Delete Modal (replaces the default delete action and performs usage checks)

---

### Asset Usage Tracker

#### Requirements
- Access to project: `asset-usage-service/contenthubtrackingcomponent`
- Node.js + npm installed
- Manage permissions in Content Hub
- Your Sitecore XP base URL (for `CMS_BASE_URL` constant)

#### Build & Configure
1. Install dependencies and navigate into the component folder:
   ```bash
   cd asset-usage-service/contenthubtrackingcomponent
   npm install
   ```
2. Open `src/AssetUsageTracker.tsx`.
3. Replace the constant `CMS_BASE_URL` with your Sitecore XP URL.
4. Save the file.
5. Build:
   ```bash
   npm run build:usageTracking
   ```
6. Result: `dist/AssetUsageTracker.js`.

#### Upload to Content Hub
1. Log in to your contenthub instance.
2. Go to: Manage → Portal assets.
3. Upload `dist/AssetUsageTracker.js`.
4. Click Profile picture → Background processes and wait until the job status is Success.

#### Add to Asset Details Page
1. Navigate to: Manage → Pages → Asset details.
2. Click + Component where you want it.
3. Search for External → Add.
4. Configure:
   - Title: `AssetUsageTracker`
   - Visible: on
   - JS bundle: From asset → + → select `AssetUsageTracker.js` → Save
5. Save the page (top-right).

#### Verification
The component loads without errors and displays usage information on the asset details page.

---

### Custom Delete Modal (External Component Action)

#### Requirements
- Access to project: `asset-usage-service/contenthubtrackingcomponent`
- Node.js + npm
- Manage permissions in Content Hub

#### Build
```bash
npm run build:deleteModal
```
Result: `dist/DeleteAssetButton.js`.

#### Upload to Content Hub
1. Log in.
2. Manage → Portal assets → Upload `dist/DeleteAssetButton.js`.
3. Profile picture → Background processes → wait for Success.

#### Configure on Asset Details Page
1. Manage → Pages → Asset details.
2. Locate component: Entity operations → click the user icon.
3. Click on the Etity operations
3. Add operation → External component action.
4. Remove the existing native Delete operation:
   - Click the X next to the current Delete.
   - Confirm Remove.
5. Drag the new external operation to Secondary operations.
6. Click it to configure.

#### Display Settings
1. Choose a trash/bin icon.
2. Set Label: `Delete`.
3. Save component.

#### Operation Settings
1. Source: From asset (or From entity if named that way in your environment).
2. JS bundle: + → select `DeleteAssetButton.js` → Save.

#### Permissions
1. Add permission: `Delete`.
2. Save the page (top-right).

#### Verification
1. Open an asset that is referenced/used in the CMS.
2. Open the context menu (three dots) → Delete.
3. Modal should appear showing:
   - A confirmation checkbox.
   - A message indicating the asset is used in X items.

If both appear, the external delete component is working.

---

### Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| JS bundle not listed | Upload not processed yet | Wait for Success in Background processes |
| Delete action not visible | Missing permission | Ensure your role has Delete for the asset type |

---

### Quick Reference

| Action | Command / Location |
|--------|--------------------|
| Build usage tracking component | `npm run build:usageTracking` |
| Build delete modal component | `npm run build:deleteModal` |
| Usage bundle path | `dist/AssetUsageTracker.js` |
| Delete bundle path | `dist/DeleteAssetButton.js` |
| Upload location | Manage → Portal assets |
| Attach bundle | Component / Operation → JS bundle → From asset |

---

### Summary
You now have:
- An Asset Usage Tracker component that displays Sitecore usage relationships.
- A Custom Delete Modal that conditionally allows deletion and surfaces usage details.

Both are managed as external JS assets in Content Hub and can be updated independently by rebuilding and re-uploading the corresponding bundle.

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
