# Local Development
This local development guide explains how to setup the service locally. In order for this to work, it is required to complete the README in the root directory afterwards.

## Prerequisites

- .NET 8.0 SDK or later
- Azure Functions Core Tools v4
- MongoDB instance (local or Azure CosmosDB with MongoDB API)
- Access to Sitecore ContentHub instance

## 1. Clone the repository:
```bash
git clone https://github.com/weareyou/asset-usage-service.git
cd asset-usage-service
```

## 2. Install dependencies:
```bash
dotnet restore
```

## 3. Configure local settings:
```bash
{
  "IsEncrypted": false,
  "Values": {

    // Azure Functions Settings
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    // Microservice Database Settings
    "MongoDB:ConnectionString": "mongodb://localhost:27017",
    "MongoDB:DatabaseName": "AssetUsageDb",
    "MongoDB:SeedData": "false",

    // Content Hub OAuth connection settings
    "ContentHub:Endpoint": "https://your-instance.stylelabs.cloud",
    "ContentHub:ClientId": "your-client-id",
    "ContentHub:ClientSecret": "your-client-secret",

    // Service Bus Connection String
    "ServiceBusQueue:ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",

    // Public Link Queue
    "ServiceBusQueue:PublicLinkQueueName": "contenthub-publiclinks-requests",
    "AzureWebJobs.PublicLinkQueueFunction.Disabled": "false",
    "AzureWebJobs.MonitorPublicLinkDeadLetterQueue.Disabled": "false",

    // Delta Calculation Queue
    "ServiceBusQueue:DeltaCalculationQueueName": "contenthub-delta-calculation-requests",
    "AzureWebJobs.DeltaCalculationQueueFunction.Disabled": "true",
    "AzureWebJobs.MonitorPushToDAMDeadLetterQueue.Disabled": "true",

    // Push to DAM Queue
    "ServiceBusQueue:PushToDAMQueueName": "push-to-contenthub-requests",
    "AzureWebJobs.PushToDAMQueueFunction.Disabled": "false",
    "AzureWebJobs.MonitorPushToDAMDeadLetterQueue.Disabled": "false"
  }
}
```
### Sitecore Settings
- **AssetUsageService.ApiEndpoint**: Asset Usage Service API endpoint (HTTP/HTTPS URL)

#### MongoDB Settings
- **ConnectionString**: MongoDB connection string
- **DatabaseName**: Database name for asset usage data
- **SeedData**: Set to `true` to populate test data on startup

##### ContentHub Settings

- **Endpoint**: ContentHub instance URL
- **ClientId**: OAuth2 client ID
- **ClientSecret**: OAuth2 client secret

#### ServiceBus Queue Settings
- **ServiceBusQueue:ConnectionString**: ServiceBus Queue connection string (For local it is the filled in string)
- **ServiceBusQueue:PublicLinkQueueName**: The name of the Public Link Queue (For local it is the filled in string)
- **AzureWebJobs.PublicLinkQueueFunction.Disabled**: If you use the queue set it to false else set it to true
- **AzureWebJobs.MonitorPublicLinkDeadLetterQueue.Disabled**:  If you use the queue set it to false else set it to true
- **ServiceBusQueue:DeltaCalculationQueueName**: The name of the Delta Calculation Queue (For local it is the filled in string)
- **ServiceBusQueue:PushToDAMQueueName**: The name of the Push To DAM QueueName (For local it is the filled in string)

## 4. Configure Local Azure ServiceBus Queues:
   - Install [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/) and make sure it is running
   - Switch to Linux containers (right click Docker icon in menubar and click `Switch to linux containers...` - if you see `Switch to windows containers...` you are already on Linux containers)
   - Create a local `.env` from the example and set a strong SQL SA password (required by SQL Server complexity rules):
```bash
cd DockerAzureServiceBusQueues
cp .env.example .env
# Edit .env and set MSSQL_SA_PASSWORD=...
docker-compose up -d
```
   - Wait for the containers to be ready:
     - Container sqlserver: `Healthy`
     - Container servicebus-emulator: `Started`
   - Check your `local.settings.json` to ensure you have the correct queue names and that the `Disabled` property of the queue functions you want to use is set to `false`

### Sitecore Web.config customization (demo / local XP)
- **Content Hub URL in CSP**: replace `https://your-instance.sitecoresandbox.cloud` in `iO.Sitecore.publishing/Web.config` with your Content Hub host.
- **Telerik encryption keys**: edit `iO.Sitecore.publishing/App_Config/TelerikKeys.config` (template: `TelerikKeys.config.example`). Replace each `REPLACE_ME_*` value with a long random string before using a shared Sitecore instance.

### Content Hub React extension
- Copy `contenthubtrackingcomponent/.env.example` to `.env` and set `VITE_CMS_BASE_URL` to your Sitecore CM host before `npm run build:usageTracking`.

## 5. Start MongoDB:
```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```
