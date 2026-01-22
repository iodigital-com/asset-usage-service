# Asset Usage Service

A microservice for tracking and managing relationships between items and digital assets across Sitecore CMS and ContentHub DAM. Built as an Azure Function application with MongoDB storage.

## Table of Contents

- [Documentation](#documentation)
- [Prerequisites](#prerequisites)
- [Development Setup](#development-setup)
- [Configuration](#configuration)
- [Content Hub Settings](#content-hub-settings)
- [Content Hub React Components Setup](#content-hub-react-components-setup)
- [Deployment](#deployment)
- [Initial Migration Script](#initial-migration-script)
- [Monitoring and Logging](#monitoring-and-logging)

## Documentation

For detailed documentation, see the following guides:

### Architecture
- [Architecture Overview](docs/architecture/overview.md) - System architecture, components, and design patterns
- [Data Flows](docs/architecture/flows.md) - Publishing flow, queue processing, and integration patterns

### Troubleshooting
- [Common Issues](docs/troubleshooting/COMMON-ISSUES.md) - Common issues and their solutions
- [Integration Troubleshooting](docs/troubleshooting/INTEGRATION.md) - Integration-specific troubleshooting for Sitecore CMS, ContentHub, and Azure services

## Prerequisites

- .NET 8.0 SDK or later
- Azure Functions Core Tools v4
- MongoDB instance (local or Azure CosmosDB with MongoDB API)
- Sitecore CMS instance (with iO.Sitecore.Publishing module)
- Access to Sitecore ContentHub instance

## Development Setup

### Local Development

See [Local Development Guide](docs/local-development/README.md) for setup instructions.

### Testing

See [Testing Guide](docs/testing/README.md) for test instructions.

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
      <setting name="AssetUsageService.ContentHubEndpoint" value="https://your-instance.sitecoresandbox.cloud" />
    </settings>

  </sitecore>
</configuration>
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

## Content Hub React Components Setup

This section describes how to add the usage insights and the custom delete Modal component to the asset details page:
1. Asset Usage Tracker (shows which Sitecore CMS items use the asset)
2. Custom Delete Modal (replaces the default delete action and performs usage checks)
3. Custom Archive Modal (replaces the default Archive action and performs usage checks)

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
Result: `dist/DeleteModal.js`.

#### Upload to Content Hub
1. Log in.
2. Manage → Portal assets → Upload `dist/DeleteModal.js`.
3. Profile picture → Background processes → wait for Success.

#### Configure on Asset Details Page
1. Manage → Pages → Asset details.
2. Locate component: Entity operations → click the user icon.
3. Click on the Entity operations
3. Add operation → External component action.
4. Remove the existing native Delete operation:
   - Click the X next to the current Delete.
   - Confirm Remove.
5. Drag the new external operation to Secondary operations.
6. Click it to configure.

##### Display Settings
1. Choose a trash/bin icon.
2. Set Label: `Delete`.
3. Save component.

##### Operation Settings
1. Source: From entity
2. Source: + → select `DeleteModal.js` → Save.

##### Permissions
1. Add permission: `Delete`.
2. Save the page (top-right).

#### Verification
1. Open an asset that is referenced/used in the CMS.
2. Open the context menu (three dots) → Delete.
3. Modal should appear showing:
   - A confirmation checkbox.
   - A message indicating the asset is used in X items.

If both appear, the external delete component is working.

### Custom Archive Modal 

#### Requirements
- Access to project: `asset-usage-service/contenthubtrackingcomponent`
- Node.js + npm installed
- Manage permissions in Content Hub
---

#### Build
```bash
npm run build:archiveModal
```
Result: `dist/ArchiveModal.js`.

#### Upload to Content Hub
1. Log in.
2. Manage → Portal assets → Upload `dist/ArchiveModal.js`.
3. Profile picture → Background processes → wait for Success.

#### Remove the existing native Archive
1. Manage → Pages → Asset details.
2. Click on the Entity operations
3. Remove the existing native Archive operation:
   - Click the X next to the current Archive.
   - Confirm Remove.

#### Add Archive component on Asset Details Page
1. Manage → Pages → Asset details.
2. Within the *Header zone (right)* click `+ Component`
3. Search for and select `Entity operations` And click Add
4. Click on the Entity operations you just added
5. Click Add operation → External component action.
6. Click it to configure.

##### Display Settings
1. Choose a Archive icon.
2. Set Label: `Archive`.
3. Set Button style `Secondary`.

##### Operation Settings
1. Source: From entity
2. Source: + → select `ArchiveModal.js` → Save.

##### Permissions
1. Add permission: `Archive`.
2. Save the page (top-right).

#### Visibility settings
1. Go back to: Manage → Pages → Asset details.
2. Drag your Entity operations by the 10 dots to your preferred location (Recommended is above the other Entity operations).
3. Click on the 3 dots and click settings
4. Select the `Conditions` tab and select `Member condition`
5. On the Member input search for and select `Archived By`
6. Select is missing next to the Archived By input And click Save

#### Verification

##### Modal Verification
1. Open an asset that is referenced/used in the CMS.
2. Click on the Archive button.
3. Modal should appear showing:
   - A confirmation checkbox.
   - A message indicating the asset is used in X items.

##### Visibility Verification
1. Archive an asset
2. go to: Manage -> Archived assets
3. Click on a Archived asset
4. Verify that the Archive button is not showing here

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

## Initial Migration Script

The Initial Migration Script scans all Sitecore items and registers existing Content Hub asset links with the Asset Usage Service. This is a one-time operation to populate the tracking database with historical data.

### Configuration

Add the following configuration to `App_Config\Include\AssetUsageService.Config`:

**AssetUsageService.Config**

          <!-- Initial Migration Script Configuration -->
          <setting name="AssetUsageService.RootItemId" value="{THE ROOT ITEM ID FROM SITECORE/CONTENT FROM THE SELECTED DATABASE}" />
          <setting name="AssetUsageService.MaxConcurrency" value="10" />
          <setting name="AssetUsageService.DatabaseName" value="DBNAME" />

| Setting | Description |
|---------|-------------|
| `AssetUsageService.RootItemId` | Sitecore item ID to start the migration from |
| `AssetUsageService.MaxConcurrency` | Maximum concurrent requests during migration (The recommended concurrency is 10) |
| `AssetUsageService.DatabaseName` | Sitecore database to scan (e.g., `master`, `web`) |

### Admin Files

Unfold to copy the following files to `[SITECORE_ROOT]\sitecore\admin\`:

<details>
<summary><strong>MigrateAssets.html</strong></summary>

    <!DOCTYPE html>
    <html>
    <head>
        <title>Asset Link Migration</title>
        <style>
            body {
                font-family: Arial, sans-serif;
                padding: 20px;
                max-width: 900px;
                margin: 0 auto;
            }

            h1 {
                color: #333;
                margin-bottom: 20px;
            }

            .btn {
                padding: 12px 24px;
                background: #007acc;
                color: white;
                border: none;
                cursor: pointer;
                font-size: 16px;
                border-radius: 4px;
            }

            .btn:hover {
                background: #005a9e;
            }

            .btn:disabled {
                background: #ccc;
                cursor: not-allowed;
            }

            .info-box {
                background: #e7f3ff;
                border-left: 4px solid #007acc;
                padding: 15px;
                margin: 20px 0;
                border-radius: 4px;
            }

            .info-box p {
                margin: 0 0 10px 0;
                line-height: 1.6;
                color: #333;
            }

            .info-box p:last-child {
                margin-bottom: 0;
            }

            .progress-container {
                margin-top: 20px;
                display: none;
            }

            .phases {
                display: flex;
                justify-content: space-between;
                margin-bottom: 25px;
                gap: 10px;
            }

            .phase {
                flex: 1;
                text-align: center;
                padding: 20px 15px;
                background: #f5f5f5;
                border-radius: 8px;
                border: 2px solid #e0e0e0;
                transition: all 0.3s;
            }

            .phase.active {
                background: #e7f3ff;
                border-color: #007acc;
            }

            .phase.completed {
                background: #d4edda;
                border-color: #28a745;
            }

            .phase-number {
                width: 30px;
                height: 30px;
                border-radius: 50%;
                background: #ccc;
                color: white;
                display: inline-flex;
                align-items: center;
                justify-content: center;
                font-weight: bold;
                margin-bottom: 10px;
            }

            .phase.active .phase-number {
                background: #007acc;
            }

            .phase.completed .phase-number {
                background: #28a745;
            }

            .phase-title {
                font-weight: bold;
                color: #333;
                margin-bottom: 5px;
            }

            .phase-status {
                font-size: 12px;
                color: #666;
            }

            .phase.active .phase-status {
                color: #007acc;
            }

            .phase.completed .phase-status {
                color: #28a745;
            }

            .progress-bar-bg {
                width: 100%;
                height: 30px;
                background: #e0e0e0;
                border-radius: 4px;
                overflow: hidden;
            }

            .progress-bar {
                height: 100%;
                background: #007acc;
                transition: width 0.3s;
                display: flex;
                align-items: center;
                justify-content: center;
                color: white;
                font-weight: bold;
            }

            .stats {
                display: grid;
                grid-template-columns: repeat(4, 1fr);
                gap: 15px;
                margin-top: 20px;
            }

            .stat-box {
                background: #f5f5f5;
                padding: 15px;
                border-radius: 4px;
                text-align: center;
            }

            .stat-value {
                font-size: 28px;
                font-weight: bold;
                color: #007acc;
            }

            .stat-label {
                font-size: 13px;
                color: #666;
                margin-top: 5px;
            }

            .current-item {
                margin-top: 15px;
                padding: 10px;
                background: #f8f9fa;
                border-left: 4px solid #007acc;
                font-family: monospace;
                font-size: 12px;
                word-break: break-all;
            }

            .duration-info {
                margin-top: 15px;
                color: #666;
            }

            .error {
                background: #f8d7da;
                color: #721c24;
                padding: 15px;
                border-radius: 4px;
                margin-top: 20px;
            }

            .success {
                background: #d4edda;
                color: #155724;
                padding: 15px;
                border-radius: 4px;
                margin-top: 20px;
            }

            .warning {
                background: #fff3cd;
                color: #856404;
                padding: 15px;
                border-radius: 4px;
                margin-top: 20px;
            }
        </style>
    </head>
    <body>
        <h1>Initial Asset Link Migration</h1>
        
        <div class="info-box">
            <p><strong>What does this migration do?</strong></p>
            <p>This script scans all items in the database to identify those that contain Content Hub asset links. Only items with Content Hub references will be sent to the Asset Usage Service for tracking.</p>
            <p><strong>The migration runs in 3 phases:</strong></p>
            <p>1. <strong>Count</strong> - Collects all items from the content tree<br>
               2. <strong>Extract</strong> - Analyzes each item and extracts Content Hub asset references<br>
               3. <strong>Send</strong> - Sends items with assets to the Asset Usage Service</p>
        </div>
        
        <button id="btnStart" class="btn" onclick="startMigration()">Start Migration</button>
        
        <div id="progressContainer" class="progress-container">
            <div class="phases">
                <div class="phase" id="phase1">
                    <div class="phase-number">1</div>
                    <div class="phase-title">Count</div>
                    <div class="phase-status" id="phase1Status">Waiting...</div>
                </div>
                <div class="phase" id="phase2">
                    <div class="phase-number">2</div>
                    <div class="phase-title">Extract</div>
                    <div class="phase-status" id="phase2Status">Waiting...</div>
                </div>
                <div class="phase" id="phase3">
                    <div class="phase-number">3</div>
                    <div class="phase-title">Send</div>
                    <div class="phase-status" id="phase3Status">Waiting...</div>
                </div>
            </div>

            <div class="progress-bar-bg">
                <div id="progressBar" class="progress-bar" style="width: 0%">0%</div>
            </div>
            
            <div class="stats">
                <div class="stat-box">
                    <div class="stat-value" id="totalItems">0</div>
                    <div class="stat-label">Total Items</div>
                </div>
                <div class="stat-box">
                    <div class="stat-value" id="extractedCount" style="color: #17a2b8;">0</div>
                    <div class="stat-label">With Assets</div>
                </div>
                <div class="stat-box">
                    <div class="stat-value" id="successCount" style="color: #28a745;">0</div>
                    <div class="stat-label">Sent</div>
                </div>
                <div class="stat-box">
                    <div class="stat-value" id="failureCount" style="color: #dc3545;">0</div>
                    <div class="stat-label">Failed</div>
                </div>
            </div>
            
            <div class="current-item">
                <strong>Current:</strong> <span id="phaseLabel"></span><br>
                <span id="currentItem">-</span>
            </div>
            
            <div class="duration-info">
                <strong>Duration:</strong> <span id="duration">0s</span> | 
                <strong>Progress:</strong> <span id="processedItems">0</span> / <span id="phaseTotal">0</span>
            </div>
        </div>
        
        <div id="errorMessage" class="error" style="display: none;"></div>
        <div id="warningMessage" class="warning" style="display: none;"></div>
        <div id="successMessage" class="success" style="display: none;"></div>
        
        <script>
            let pollInterval;
            let pollCount = 0;
            const MAX_POLLS = 5;
            let lastPhase = 0;
            let phase1Total = 0;
            let phase2Total = 0;
            
            function startMigration() {
                document.getElementById('btnStart').disabled = true;
                document.getElementById('progressContainer').style.display = 'block';
                document.getElementById('errorMessage').style.display = 'none';
                document.getElementById('warningMessage').style.display = 'none';
                document.getElementById('successMessage').style.display = 'none';
                
                resetPhases();
                pollCount = 0;
                lastPhase = 0;
                phase1Total = 0;
                phase2Total = 0;
                
                fetch('/sitecore/admin/MigrationHandler.ashx?action=start', { 
                    method: 'POST' 
                })
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
            
            function resetPhases() {
                for (let i = 1; i <= 3; i++) {
                    document.getElementById('phase' + i).className = 'phase';
                    document.getElementById('phase' + i + 'Status').textContent = 'Waiting...';
                }
            }
            
            function startPolling() {
                pollInterval = setInterval(checkStatus, 500);
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
                        console.log('Status:', data);
                        updateUI(data);
                        
                        if (!data.isRunning) {
                            if (pollCount > MAX_POLLS) {
                                stopPolling();
                                showCompletion(data);
                            }
                        } else {
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
                updatePhases(data);
                
                document.getElementById('progressBar').style.width = data.progressPercentage + '%';
                document.getElementById('progressBar').textContent = data.progressPercentage + '%';
                
                document.getElementById('totalItems').textContent = phase1Total > 0 ? phase1Total : data.totalItems;
                document.getElementById('extractedCount').textContent = data.extractedCount;
                document.getElementById('successCount').textContent = data.successCount;
                document.getElementById('failureCount').textContent = data.failureCount;
                
                document.getElementById('phaseLabel').textContent = data.phaseDescription || '';
                document.getElementById('currentItem').textContent = data.currentItem || '-';
                
                document.getElementById('duration').textContent = data.durationSeconds + 's';
                document.getElementById('processedItems').textContent = data.processedItems;
                document.getElementById('phaseTotal').textContent = data.totalItems;
                
                if (data.errorMessage && data.currentPhase > 0) {
                    document.getElementById('errorMessage').textContent = 'Error: ' + data.errorMessage;
                    document.getElementById('errorMessage').style.display = 'block';
                }
            }
            
            function updatePhases(data) {
                var currentPhase = data.currentPhase;
                
                if (currentPhase === 2 && lastPhase === 1) {
                    phase1Total = data.totalItems;
                }
                if (currentPhase === 3 && lastPhase === 2) {
                    phase2Total = data.extractedCount;
                }
                
                for (let i = 1; i < currentPhase; i++) {
                    document.getElementById('phase' + i).className = 'phase completed';
                    if (i === 1) {
                        document.getElementById('phase1Status').textContent = phase1Total + ' items';
                    } else if (i === 2) {
                        document.getElementById('phase2Status').textContent = data.extractedCount + ' with assets';
                    }
                }
                
                if (currentPhase >= 1 && currentPhase <= 3) {
                    document.getElementById('phase' + currentPhase).className = 'phase active';
                    
                    if (currentPhase === 1) {
                        document.getElementById('phase1Status').textContent = 'Counting... ' + data.processedItems;
                    } else if (currentPhase === 2) {
                        document.getElementById('phase2Status').textContent = data.processedItems + '/' + data.totalItems;
                    } else if (currentPhase === 3) {
                        document.getElementById('phase3Status').textContent = data.processedItems + '/' + data.totalItems;
                    }
                }
                
                lastPhase = currentPhase;
            }
            
            function showCompletion(data) {
                document.getElementById('btnStart').disabled = false;
                
                for (let i = 1; i <= 3; i++) {
                    document.getElementById('phase' + i).className = 'phase completed';
                }
                document.getElementById('phase1Status').textContent = phase1Total + ' items';
                document.getElementById('phase2Status').textContent = data.extractedCount + ' with assets';
                document.getElementById('phase3Status').textContent = data.successCount + ' sent';
                
                if (data.errorMessage && data.extractedCount === 0) {
                    document.getElementById('warningMessage').textContent = data.errorMessage;
                    document.getElementById('warningMessage').style.display = 'block';
                } else if (data.failureCount > 0) {
                    document.getElementById('errorMessage').textContent = 
                        'Migration completed with ' + data.failureCount + ' failures. Check logs for details.';
                    document.getElementById('errorMessage').style.display = 'block';
                } else {
                    var message = 'Migration completed successfully in ' + data.durationSeconds + 's. ';
                    message += 'Scanned ' + phase1Total + ' items, found ' + data.extractedCount + ' with assets, ';
                    message += 'sent ' + data.successCount + ' to service.';
                    document.getElementById('successMessage').textContent = message;
                    document.getElementById('successMessage').style.display = 'block';
                }
            }
        </script>
    </body>
    </html>

</details>

<details>
<summary><strong>MigrationHandler.ashx</strong></summary>

    <%@ WebHandler Language="C#" Class="MigrationHandler" %>

    using System;
    using System.Web;
    using System.Threading.Tasks;
    using System.Text.Json;
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
            
            var status = new
            {
                isRunning = MigrationProgressTracker.IsRunning,
                currentPhase = MigrationProgressTracker.CurrentPhase,
                phaseDescription = MigrationProgressTracker.PhaseDescription ?? "",
                totalItems = MigrationProgressTracker.TotalItems,
                processedItems = MigrationProgressTracker.ProcessedItems,
                extractedCount = MigrationProgressTracker.ExtractedCount,
                successCount = MigrationProgressTracker.SuccessCount,
                failureCount = MigrationProgressTracker.FailureCount,
                progressPercentage = MigrationProgressTracker.ProgressPercentage,
                currentItem = MigrationProgressTracker.CurrentItem ?? "",
                errorMessage = MigrationProgressTracker.ErrorMessage ?? "",
                durationSeconds = (int)duration.TotalSeconds
            };
            
            context.Response.Write(JsonSerializer.Serialize(status));
        }
        
        public bool IsReusable
        {
            get { return false; }
        }
    }

</details>

### Usage

1. Navigate to `https://[SITECORE_INSTANCE]/sitecore/admin/MigrateAssets.html`
2. Click **Start Migration**
3. Monitor progress through the three phases:
   - **Count**: Enumerates all items in the content tree
   - **Extract**: Identifies items containing Content Hub asset links
   - **Send**: Transmits asset usage data to the Asset Usage Service

### Notes

- The migration runs asynchronously in the background
- Progress is displayed in real-time via polling
- Check Sitecore logs for detailed error information if failures occur
- The migration can be re-run safely; duplicate entries are handled by the service

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
