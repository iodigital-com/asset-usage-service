# Integration Troubleshooting

This document covers integration-specific troubleshooting for connecting the Asset Usage Service with Sitecore CMS, ContentHub, and Azure services.

## Sitecore CMS Integration

### Event Handler Not Triggering

**Symptom**: Publishing items in Sitecore doesn't send data to Asset Usage Service

**Checklist**:

1. **DLL Installation**
   - Verify `iO.Sitecore.publishing.dll` is in the Sitecore bin folder
   - Check for any DLL version conflicts

2. **Event Configuration** (`App_Config\Include\zzz.iO\iO.Publishing.Events.config`):
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

3. **Service Endpoint Configuration** (`App_Config\Include\AssetUsageService.config`):
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
     <sitecore>
       <settings>
         <setting name="AssetUsageService.ApiEndpoint" value="https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI" />
         <setting name="AssetUsageService.FunctionKey" value="<FUNCTION_KEY>" />
         <setting name="AssetUsageService.ContentHubEndpoint" value="https://your-instance.sitecoresandbox.cloud" />
       </settings>
     </sitecore>
   </configuration>
   ```

4. **Restart Sitecore** after configuration changes

5. **Check Sitecore Logs** for errors:
   - Look for entries from `AssetUsageServiceClient`
   - Check for HTTP request/response details

### Function Key Authentication Issues

**Symptom**: 401 Unauthorized when Sitecore calls the Azure Function

**Solution**:

**Recommended Approach (HTTP Header)**:
1. Configure Sitecore to send key via `x-functions-key` header
2. Set `AssetUsageService.FunctionKey` in Sitecore config
3. Store the function key outside source control

**Legacy Approach (Query String)**:
1. Get function-level key:
   ```bash
   az functionapp function keys list \
     -g <RESOURCE_GROUP> \
     -n <FUNCTION_APP_NAME> \
     --function-name SitecorePublishAPI \
     --query "default" -o tsv
   ```
2. Append to endpoint URL:
   ```
   https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI?code=<FUNCTION_KEY>
   ```

---

## ContentHub Integration

### OAuth Authentication Failures

**Symptom**: Service cannot authenticate with ContentHub

**Checklist**:

1. **Verify OAuth Client Configuration**:
   - Navigate to Manage → OAuth clients in ContentHub
   - Confirm client is configured for "Client Credentials" type
   - Verify client is associated with correct service user

2. **Check Credentials**:
   - `ContentHub__Endpoint` - Full URL to ContentHub instance
   - `ContentHub__ClientId` - OAuth client ID
   - `ContentHub__ClientSecret` - OAuth client secret (stored in Key Vault)

3. **Verify Service User**:
   - User must be verified (email confirmed)
   - User must be in correct user group
   - User group must have required permissions

### UsageTracking Property Not Updating

**Symptom**: Asset usage data isn't appearing in ContentHub

**Checklist**:

1. **Schema Configuration**:
   - M.Asset must have UsageTracking member group
   - UsageTracking property must be JSON type
   - Property must have "Allow Updates" and "Secured" checked

2. **Member-Level Security**:
   - Everyone group: Read permission on UsageTracking
   - Asset Editors Service group: Read AND Write permissions

3. **Verify with Impersonation**:
   - Impersonate the service user in ContentHub
   - Try to manually edit UsageTracking on an asset
   - If it fails, check user group policies

### ContentHub React Components Not Loading

**Symptom**: AssetUsageTracker or modals don't appear

**Checklist**:

1. **Build the Component**:
   ```bash
   cd asset-usage-service/contenthubtrackingcomponent
   npm install
   npm run build:usageTracking  # or build:deleteModal or build:archiveModal
   ```

2. **Upload to ContentHub**:
   - Manage → Portal assets → Upload the JS file
   - Wait for Background process to complete with "Success"

3. **Configure on Page**:
   - Manage → Pages → Asset details
   - Add External component
   - Set JS bundle source to uploaded file

4. **Verify CMS_BASE_URL**:
   - Open `src/AssetUsageTracker.tsx`
   - Ensure `CMS_BASE_URL` points to your Sitecore XP URL

---

## Azure Service Bus Integration

### Queue Messages Not Processing

**Symptom**: Messages are queued but functions don't process them

**Checklist**:

1. **Verify Queue Functions are Enabled** in `local.settings.json`:
   ```json
   {
     "AzureWebJobs.PublicLinkQueueFunction.Disabled": "false",
     "AzureWebJobs.MonitorPublicLinkDeadLetterQueue.Disabled": "false",
     "AzureWebJobs.DeltaCalculationQueueFunction.Disabled": "false",
     "AzureWebJobs.PushToDAMQueueFunction.Disabled": "false"
   }
   ```

2. **Check Connection String**:
   - Local: Use emulator connection string with `UseDevelopmentEmulator=true`
   - Production: Use Azure Service Bus connection string from Key Vault

3. **Verify Queue Names Match**:
   ```json
   {
     "ServiceBusQueue:PublicLinkQueueName": "contenthub-publiclinks-requests",
     "ServiceBusQueue:DeltaCalculationQueueName": "contenthub-delta-calculation-requests",
     "ServiceBusQueue:PushToDAMQueueName": "push-to-contenthub-requests"
   }
   ```

### Local Service Bus Emulator Issues

**Symptom**: Cannot connect to local Service Bus

**Solution**:

1. **Start Docker Desktop** and switch to Linux containers

2. **Run the emulator**:
   ```bash
   cd DockerAzureServiceBusQueues
   cp .env.example .env   # first time only
   # Set MSSQL_SA_PASSWORD in .env (SQL Server requires a strong password)
   docker-compose up -d
   ```

3. **Wait for containers**:
   - Container sqlserver: `Healthy`
   - Container servicebus-emulator: `Started`

4. **Use correct connection string**:
   ```
   Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
   ```

---

## MongoDB / Cosmos DB Integration

### Connection Failures

**Symptom**: Function cannot connect to database

**Checklist**:

1. **Connection String Format** for Cosmos DB vCore:
   ```
   mongodb+srv://<user>:<password>@<cluster>.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000
   ```

2. **Required Parameters**:
   - `tls=true` - Required for Cosmos DB
   - `authMechanism=SCRAM-SHA-256` - Authentication method
   - `retrywrites=false` - Required for Cosmos DB vCore

3. **Networking**:
   - Add Function App outbound IPs to Cosmos DB firewall
   - Or enable "Allow access from Azure services"

4. **Test Connection Locally**:
   ```bash
   mongosh "mongodb+srv://<connection-string>"
   ```

### Local MongoDB Setup

For local development:
```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```

Connection string for local:
```
mongodb://localhost:27017
```

---

## Azure Key Vault Integration

### Secrets Not Resolving

**Symptom**: Function App can't read secrets from Key Vault

**Checklist**:

1. **Enable Managed Identity**:
   - Function App → Settings → Identity
   - System assigned → Status: On

2. **Grant Key Vault Access**:

   **RBAC (Recommended)**:
   - Key Vault → Access control (IAM)
   - Add role assignment → Key Vault Secrets User
   - Select Function App's managed identity

   **Access Policies (Legacy)**:
   - Key Vault → Access policies → Create
   - Grant Get and List permissions for secrets

3. **Use Correct Reference Format**:
   ```
   @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>)
   ```

4. **Verify in Kudu** (`https://<app>.scm.azurewebsites.net/Env`):
   - Look for `WEBSITE_KEYVAULT_REFERENCES` section
   - All secrets should show `status: "Resolved"`

---

## Testing Integration

### Smoke Test via Azure Portal

1. Navigate to Function App → Functions → **SitecorePublishAPI** → **Test/Run**

2. Set header:
   ```
   Content-Type: application/json
   ```

3. Use test body:
   ```json
   {
     "ItemId": "110d559f-dea5-42ea-9c1c-8a5df7e70ef9",
     "Language": "en",
     "ItemName": "Home",
     "Version": 1,
     "ItemPath": "/sitecore/content/Home",
     "AssetIds": [],
     "PublicLinks": []
   }
   ```

4. Expect `200` or `202` response

5. **Verify in Application Insights**:
   - Transaction search → Recent requests to `/api/SitecorePublishAPI`
   - Check dependencies for ContentHub and MongoDB calls

6. **Verify in Cosmos DB**:
   - Connect with MongoDB Compass
   - Check `AssetItemLinks` collection

### Running Integration Tests

1. Create `appsettings.Test.json` in AssetUsageServiceTests:
   ```json
   {
     "ServiceBusQueue": {
       "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
     },
     "ContentHub": {
       "IntegrationTestsEnabled": "true",
       "Endpoint": "https://your-instance.sitecoresandbox.cloud",
       "ClientId": "your-oauth-client-id",
       "ClientSecret": "your-oauth-client-secret",
       "TestPublicLink": "https://your-instance.sitecoresandbox.cloud/api/public/content/xxxxx",
       "TestPublicLinks": [
         "https://your-instance.sitecoresandbox.cloud/api/public/content/xxxxx",
         "https://your-instance.sitecoresandbox.cloud/api/public/content/yyyyy"
       ]
     }
   }
   ```

2. Run integration tests:
   ```bash
   dotnet test --filter "Category=Integration"
   ```

---

## Common Integration Error Messages

> **Note**: In Azure Function app settings, use double underscore (`__`) which maps to colon (`:`) in .NET configuration. For example, `ContentHub__Endpoint` in app settings becomes `ContentHub:Endpoint` in code.

| Error | Cause | Solution |
|-------|-------|----------|
| `MongoAuthenticationException: SASL authentication failed` | Invalid credentials or connection string | Verify connection string format and credentials |
| `Configuration key 'ContentHub:Endpoint' not found` | Missing app setting | Add `ContentHub__Endpoint` to Function App config (double underscore) |
| `401 Unauthorized` | Invalid function key | Update function key in Sitecore config |
| `The specified queue does not exist` | Queue name mismatch | Verify queue names in configuration |
| `SecretUri format is invalid` | Malformed Key Vault reference | Use `@Microsoft.KeyVault(SecretUri=...)` format |
