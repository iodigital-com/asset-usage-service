# Local Development

## Prerequisites

- .NET 8.0 SDK or later
- Azure Functions Core Tools v4
- MongoDB instance (local or Azure CosmosDB with MongoDB API)
- Sitecore CMS instance (with iO.Sitecore.Publishing module)
- Access to Sitecore ContentHub instance

## Getting Started

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

      <!-- Content Hub endpoint -->
      <setting name="AssetUsageService.ContentHubEndpoint" value="https://stage-pasha-darlon-2.sitecoresandbox.cloud" />
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

#### ServiceBus Queue Settings

- **ServiceBusQueue:ConnectionString**: ServiceBus Queue connection string (For local it is the filled in string)

- **ServiceBusQueue:PublicLinkQueueName**: The name of the Public Link Queue (For local it is the filled in string)
- **AzureWebJobs.PublicLinkQueueFunction.Disabled**: If you use the queue set it to false else set it to true
- **AzureWebJobs.MonitorPublicLinkDeadLetterQueue.Disabled**:  If you use the queue set it to false else set it to true


- **ServiceBusQueue:DeltaCalculationQueueName**: The name of the Delta Calculation Queue (For local it is the filled in string)

- **ServiceBusQueue:PushToDAMQueueName**: The name of the Push To DAM QueueName (For local it is the filled in string)

## Local azure ServiceBus Queues 
Install [Docker desktop](https://docs.docker.com/desktop/setup/install/windows-install/) and make sure it is running 

Switch to Linux container (right click docker icon in menubar and click `Switch to linux containers...` if you see `Switch to windows containers...` you are already on linux containers) 

To run the azure servicebus Queue local go to the folder **DockerAzureServiceBusQueues** in your terminal and run:
```zsh
 docker-compose up -d
 ```
Wait for it to say:
- Container sqlserver:            `Healthy`
- Container servicebus-emulator:  `Started`

Check your `local.settings.json` to ensure you have the correct queue names and that the `Disabled` property of the queue functions you want to use is set to `false`.

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

### Summary

You have:
- A scoped service user with only necessary asset permissions.
- An OAuth client using Client Credentials tied to that user.
- A secured JSON property on M.Asset with controlled read/write access.
- A foundation for storing and exposing asset usage metadata safely.
