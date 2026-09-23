# Common Issues and Solutions

This document covers common issues encountered when running the Asset Usage Service and their solutions.

## Key Vault Resolution Issues

### Symptom

App settings show `[Hidden Credential]` but function logs show "configuration not found"

### Solution

1. **Restart the Function App** after changing any secret version, or re-save an app setting to force immediate re-resolution

2. **Verify managed identity permissions**:
   - Check that the Function App's managed identity has `Key Vault Secrets User` role
   - Navigate to Key Vault → Access control (IAM) → Verify role assignment

3. **Verify the SecretUri format**:
   ```
   @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>)
   ```
   - Do not include version in SecretUri for automatic rotation

4. **Check resolution status** in Kudu (`https://<FUNCTION_APP_NAME>.scm.azurewebsites.net/Env`):
   - `WEBSITE_KEYVAULT_REFERENCES` section should show status **Resolved**

### Related Documentation

- [Key Vault References](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [RBAC Guide](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

---

## SASL Authentication Errors

### Symptom

`MongoAuthenticationException: SASL authentication failed`

### Solution

1. **Use SRV format**: Ensure your connection string uses `mongodb+srv://` protocol

2. **Enable TLS**: Include `tls=true` in connection string

3. **Set auth source**: Add `authSource=admin` if your user is in the admin database

4. **Verify hostname**: Confirm hostname matches the vCore SRV endpoint (`.mongocluster.cosmos.azure.com`)

5. **Test locally first**: Test connection string using mongosh before adding to Key Vault

### Example Connection String

```
mongodb+srv://<user>:<password>@<cluster>.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000
```

### Related Documentation

- [Troubleshooting Common Issues](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

---

## Missing "ContentHub:Endpoint" Error

### Symptom

Application logs show `Configuration key 'ContentHub:Endpoint' not found`

### Solution

1. **Check app setting name**: Verify `ContentHub__Endpoint` is set in Function App configuration
   - Note: Use double underscore (`__`) which maps to `:` for .NET configuration

2. **Verify all ContentHub settings exist**:
   - `ContentHub__Endpoint` - Direct value
   - `ContentHub__ClientId` - Key Vault reference
   - `ContentHub__ClientSecret` - Key Vault reference

3. **Restart the Function App** after making configuration changes

4. **Check Key Vault references** show status "Resolved" in Kudu `/Env`

---

## Network Connectivity Issues

### Symptom

Function cannot reach Cosmos DB or Content Hub

### Solution

**For Cosmos DB:**
1. Navigate to Cosmos DB → Networking → Firewall settings
2. Add Function App's outbound IPs
3. Or enable "Allow access from Azure services"

**For Content Hub:**
1. Verify endpoint URL is accessible from Azure
2. Check for any corporate firewall restrictions

**Test connectivity** using Kudu → Debug console → PowerShell:
```powershell
Test-NetConnection <hostname> -Port 443
```

### Related Documentation

- [Cosmos DB vCore Networking](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/troubleshoot-common-issues)

---

## Local Development Issues

### Docker Container Not Starting

**Symptom**: Azure ServiceBus emulator or MongoDB container won't start

**Solution**:
1. Ensure Docker Desktop is running
2. Switch to Linux containers:
   - Right-click Docker icon → `Switch to Linux containers...`
   - If you see `Switch to Windows containers...`, you're already on Linux

3. Run from the `DockerAzureServiceBusQueues` folder:
   ```bash
   cp .env.example .env   # if you do not have a local .env yet
   # Set MSSQL_SA_PASSWORD in .env (strong password required)
   docker-compose up -d
   ```

4. Wait for containers to be healthy:
   - Container sqlserver: `Healthy`
   - Container servicebus-emulator: `Started`

### Function App Won't Start Locally

**Symptom**: `func start` fails or function doesn't respond

**Solution**:

1. **Check local.settings.json exists**:
   ```bash
   cp local.settings.json.example local.settings.json
   ```

2. **Verify MongoDB is running**:
   ```bash
   docker run -d -p 27017:27017 --name mongodb mongo:latest
   ```

3. **Check queue settings**:
   - Ensure correct queue names in `local.settings.json`
   - Set `Disabled` property to `false` for queues you want to use

4. **Install Azure Functions Core Tools v4** if not installed

---

## Sitecore Event Handler Issues

### Events Not Being Captured

**Symptom**: Publishing items doesn't trigger the Asset Usage Service

**Solution**:

1. **Verify DLL is installed**:
   - Copy `iO.Sitecore.publishing.dll` to Sitecore bin folder

2. **Check configuration file**:
   - `iO.Publishing.Events.config` should be in `App_Config\Include\zzz.iO\`

3. **Verify endpoint configuration**:
   - `AssetUsageService.config` should be in `App_Config\Include\`
   - Check `AssetUsageService.ApiEndpoint` points to correct URL

4. **Restart Sitecore** after making changes

5. **Check Sitecore logs** for any errors from `iO.Sitecore.publishing`

---

## Migration Script Issues

### Migration Not Starting

**Symptom**: Clicking "Start Migration" does nothing

**Solution**:

1. **Check configuration** in `App_Config\Include\AssetUsageService.Config`:
   ```xml
   <setting name="AssetUsageService.RootItemId" value="{ROOT_ITEM_ID}" />
   <setting name="AssetUsageService.MaxConcurrency" value="10" />
   <setting name="AssetUsageService.DatabaseName" value="master" />
   ```

2. **Verify admin files are in place**:
   - `MigrateAssets.html` in `[SITECORE_ROOT]\sitecore\admin\`
   - `MigrationHandler.ashx` in `[SITECORE_ROOT]\sitecore\admin\`

3. **Check browser console** for JavaScript errors

4. **Check Sitecore logs** for migration errors:
   - Look for `[MigrationHandler]` entries

### Migration Shows Zero Items

**Symptom**: Phase 1 completes but shows 0 items

**Solution**:

1. **Verify RootItemId** is correct and exists in the database
2. **Check database name** matches your target (master, web, etc.)
3. **Ensure user has read access** to content tree

---

## ContentHub Component Issues

### Asset Usage Tracker Not Displaying

**Symptom**: Component doesn't appear on Asset Details page

**Solution**:

1. **Verify upload completed**:
   - Profile picture → Background processes → Status should be `Success`

2. **Check page configuration**:
   - Navigate to Manage → Pages → Asset details
   - Verify the External component is configured
   - JS bundle should point to `AssetUsageTracker.js`

3. **Check browser console** for JavaScript errors

4. **Verify CMS_BASE_URL** is correct in the component source

### Delete/Archive Modal Not Appearing

**Symptom**: Default delete/archive behavior instead of custom modal

**Solution**:

1. **Remove native operation first**:
   - In Entity operations, click X next to the current Delete/Archive
   - Confirm removal

2. **Add external component action**:
   - Add operation → External component action
   - Configure source to point to `DeleteModal.js` or `ArchiveModal.js`

3. **Set permissions correctly**:
   - Delete modal needs `Delete` permission
   - Archive modal needs `Archive` permission

4. **Save the page** (top-right)

---

## Verify Settings in Kudu

For any Azure Function configuration issues, use Kudu diagnostics:

Navigate to: `https://<FUNCTION_APP_NAME>.scm.azurewebsites.net/Env`

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
