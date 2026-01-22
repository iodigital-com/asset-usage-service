# Asset Usage Service - Data Flows

This document describes the data flows and integration patterns in the Asset Usage Service.

## Publishing Flow

When content is published in Sitecore CMS, the following flow occurs:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           SITECORE CMS                                  │
│                                                                         │
│  1. Editor publishes item                                               │
│     ▼                                                                   │
│  2. publish:itemProcessing event fires                                  │
│     ▼                                                                   │
│  3. PublishEventHandler.OnItemProcessing() captures event               │
│     ▼                                                                   │
│  4. publish:itemProcessed event fires                                   │
│     ▼                                                                   │
│  5. PublishEventHandler.OnItemProcessed() extracts asset links          │
│     ▼                                                                   │
│  6. publish:end event fires                                             │
│     ▼                                                                   │
│  7. PublishEventHandler.OnPublishEnd() sends data to service            │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      ASSET USAGE SERVICE                                │
│                                                                         │
│  8. SitecorePublishAPI receives request                                 │
│     ▼                                                                   │
│  9. Validates and processes asset-item relationships                    │
│     ▼                                                                   │
│  10. Stores/updates in MongoDB                                          │
│     ▼                                                                   │
│  11. Queues ContentHub update (if needed)                               │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        CONTENT HUB                                      │
│                                                                         │
│  12. UsageTracking property updated on M.Asset                          │
│     ▼                                                                   │
│  13. Asset Usage Tracker component displays usage                       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Queue Processing Flows

### Public Link Queue Flow

```
Sitecore CMS publishes item with ContentHub assets
    │
    ▼
SitecorePublishAPI receives request with public links
    │
    ▼
Message queued to: contenthub-publiclinks-requests
    │
    ▼
PublicLinkQueueFunction processes message
    │
    ▼
Resolves asset IDs from public links via ContentHub API
    │
    ▼
Updates MongoDB with resolved asset-item relationships
```

### Delta Calculation Queue Flow

```
Asset usage change detected
    │
    ▼
Message queued to: contenthub-delta-calculation-requests
    │
    ▼
DeltaCalculationQueueFunction processes message
    │
    ▼
Calculates difference between old and new asset usage
    │
    ▼
Queues updates to Push to DAM queue
```

### Push to DAM Queue Flow

```
Delta calculation complete
    │
    ▼
Message queued to: push-to-contenthub-requests
    │
    ▼
PushToDAMQueueFunction processes message
    │
    ▼
Updates UsageTracking property on M.Asset in ContentHub
    │
    ▼
Asset Usage Tracker component reflects changes
```

## Event Handler Configuration

The Sitecore event handler captures these events:

| Event | Handler Method | Purpose |
|-------|---------------|---------|
| `publish:itemProcessing` | `OnItemProcessing` | Prepare for item processing |
| `publish:itemProcessed` | `OnItemProcessed` | Extract asset links from item |
| `publish:end` | `OnPublishEnd` | Send collected data to service |
| `publish:end:remote` | `OnPublishEndRemote` | Handle remote publish events |

## API Request Format

The SitecorePublishAPI expects requests in this format:

```json
{
  "ItemId": "110d559f-dea5-42ea-9c1c-8a5df7e70ef9",
  "Language": "en",
  "ItemName": "Home",
  "Version": 1,
  "ItemPath": "/sitecore/content/Home",
  "AssetIds": [34013, 13343],
  "PublicLinks": [
    "https://your-instance.sitecoresandbox.cloud/api/public/content/xxxxx"
  ]
}
```

## Initial Migration Flow

For existing content, use the Initial Migration Script:

```
Administrator accesses /sitecore/admin/MigrateAssets.html
    │
    ▼
Phase 1: COUNT
    │   - Enumerates all items in content tree
    │   - Starting from configured RootItemId
    │
    ▼
Phase 2: EXTRACT
    │   - Analyzes each item for ContentHub asset links
    │   - Filters to only items with assets
    │
    ▼
Phase 3: SEND
    │   - Transmits asset usage data to Asset Usage Service
    │   - Processes in batches with configurable concurrency
    │
    ▼
MongoDB populated with historical data
    │
    ▼
ContentHub UsageTracking properties updated
```

### Migration Configuration

| Setting | Description |
|---------|-------------|
| `AssetUsageService.RootItemId` | Sitecore item ID to start migration from |
| `AssetUsageService.MaxConcurrency` | Maximum concurrent requests (recommended: 10) |
| `AssetUsageService.DatabaseName` | Sitecore database to scan (e.g., `master`, `web`) |

## ContentHub Component Flow

### Asset Usage Tracker Component

```
User opens Asset Details page in ContentHub
    │
    ▼
AssetUsageTracker.tsx component loads
    │
    ▼
Reads UsageTracking property from M.Asset
    │
    ▼
Displays list of Sitecore items using this asset
    │
    ▼
Links to Sitecore CMS for each item (via CMS_BASE_URL)
```

### Custom Delete Modal Flow

```
User clicks Delete on asset
    │
    ▼
DeleteModal.js component intercepts action
    │
    ▼
Checks UsageTracking property for linked items
    │
    ▼
If asset is in use:
    │   - Displays warning with usage count
    │   - Requires confirmation checkbox
    │
    ▼
If confirmed or not in use:
    │   - Proceeds with delete operation
```

### Custom Archive Modal Flow

```
User clicks Archive on asset
    │
    ▼
ArchiveModal.js component intercepts action
    │
    ▼
Checks UsageTracking property for linked items
    │
    ▼
If asset is in use:
    │   - Displays warning with usage count
    │   - Requires confirmation checkbox
    │
    ▼
If confirmed or not in use:
    │   - Proceeds with archive operation
```

## Security Flow

### OAuth2 Client Credentials Flow

```
Asset Usage Service needs to update ContentHub
    │
    ▼
Requests access token using Client ID and Secret
    │
    ▼
ContentHub validates credentials
    │
    ▼
Returns access token for asset-service-user
    │
    ▼
Service uses token for UsageTracking property updates
    │
    ▼
Member-level security enforces write permissions
```

### Key Vault Integration

```
Azure Function starts
    │
    ▼
Reads app settings with @Microsoft.KeyVault references
    │
    ▼
Managed Identity authenticates to Key Vault
    │
    ▼
Secrets resolved: ConnectionString, ClientId, ClientSecret
    │
    ▼
Function operates with secure configuration
```
