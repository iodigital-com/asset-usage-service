## Deployment

This guide covers deploying the .NET 8 isolated Azure Function that integrates with Sitecore Content Hub and Cosmos DB for MongoDB (vCore).

### Overview

All environment-specific values are provided via Azure Key Vault and GitHub Secrets; nothing is hard-coded in source.

> **Note**: Double-underscore (`__`) in app settings maps to `:` for .NET configuration.

### Required App Settings

- `ContentHub__Endpoint`
- `ContentHub__ClientId`
- `ContentHub__ClientSecret`
- `MongoDb__ConnectionString` (note the capital S)
- `MongoDB__DatabaseName`

### Prerequisites

Before deploying, you'll need the following values:

- `<AZ_SUBSCRIPTION_ID>` - Your Azure subscription ID
- `<AZ_TENANT_ID>` - Your Azure tenant ID
- `<AZ_CLIENT_ID>` - GitHub OIDC service principal client ID
- `<RESOURCE_GROUP>` - Azure resource group name
- `<FUNCTION_APP_NAME>` - Function app name (e.g., `asset-usage-fa`)
- `<KEYVAULT_NAME>` - Key vault name (e.g., `asset-usage-kv`)
- `<MONGO_CONNECTION_STRING>` - SRV format; tls=true; SCRAM-SHA-256; optionally authSource=admin
- `<MONGO_DATABASE_NAME>` - Logical database name (e.g., `asset-usage`)
- `<CONTENTHUB_ENDPOINT>` - ContentHub endpoint (e.g., `https://your-env.sitecoresandbox.cloud`)
- `<CONTENTHUB_CLIENT_ID>` - ContentHub client ID
- `<CONTENTHUB_CLIENT_SECRET>` - ContentHub client secret

### Step 1: Prepare Azure Resources and Identity

1. Ensure the Function App (`<FUNCTION_APP_NAME>`) exists and uses .NET 8 isolated (Linux)
2. Enable system-assigned Managed Identity on the Function App
3. Grant Key Vault access:
   - **If using Azure RBAC**: Assign "Key Vault Secrets User" to the Function App's identity at vault scope
   - **If using Access Policies**: Grant get/list on secrets

### Step 2: Create Key Vault Secrets

Create the following versionless secrets (one per environment):

- `MongoDbConnectionString` = `<MONGO_CONNECTION_STRING>`
- `ContentHub-ClientId` = `<CONTENTHUB_CLIENT_ID>`
- `ContentHub-ClientSecret` = `<CONTENTHUB_CLIENT_SECRET>`

### Step 3: Configure Function App Settings

Set the following application settings (no secrets in plain text):

```
ContentHub__Endpoint = <CONTENTHUB_ENDPOINT>
ContentHub__ClientId = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/ContentHub-ClientId)
ContentHub__ClientSecret = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/ContentHub-ClientSecret)
MongoDb__ConnectionString = @Microsoft.KeyVault(SecretUri=https://<KEYVAULT_NAME>.vault.azure.net/secrets/MongoDbConnectionString)
MongoDB__DatabaseName = <MONGO_DATABASE_NAME>
```

Restart the Function App and verify in Kudu `/Env` that `WEBSITE_KEYVAULT_REFERENCES` shows "Resolved".

### Step 4: Set Up CI/CD with GitHub Actions

#### Option A: Azure-Generated Workflow (Recommended)

1. In Azure Portal, go to: Function App > Deployment Center
2. Set Source: GitHub
3. Choose your repository and branch `main`
4. Set Runtime: .NET 8 (isolated)
5. Set Build provider: GitHub Actions
6. Click Save

Azure will automatically commit a workflow file and create the needed GitHub secrets for OIDC.

#### Option B: Manual Workflow Setup

1. Create the following repository secrets:
   - `AZURE_SUBSCRIPTION_ID` = `<AZ_SUBSCRIPTION_ID>`
   - `AZURE_TENANT_ID` = `<AZ_TENANT_ID>`
   - `AZURE_CLIENT_ID` = `<AZ_CLIENT_ID>`

2. Add this file at `.github/workflows/function-deploy.yml`:

```yaml
name: Build and deploy dotnet core project to Azure Function App

on:
  push:
    branches: [ main ]
  workflow_dispatch:

env:
  DOTNET_VERSION: '8.0.x'
  AZURE_FUNCTIONAPP_PACKAGE_PATH: './AssetUsageService'    # change if your project path differs
  FUNCTION_APP_NAME: '<FUNCTION_APP_NAME>'                 # or set via repo/environment variables

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read

    steps:
    - name: Checkout
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Add Sitecore NuGet feed
      run: dotnet nuget add source https://nuget.sitecore.com/resources/v3/index.json --name SitecoreOfficial

    - name: Build
      run: |
        pushd '${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}'
        dotnet restore
        dotnet build --configuration Release --output ./output
        popd

    - name: Azure login (OIDC)
      uses: azure/login@v2
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

    - name: Deploy to Azure Functions
      uses: Azure/functions-action@v1
      with:
        app-name: ${{ env.FUNCTION_APP_NAME }}
        slot-name: 'Production'
        package: '${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}/output'
```

> **Notes**:
> - If you deploy multiple environments, use GitHub "environments" (dev/stage/prod) with environment-level secrets and set `FUNCTION_APP_NAME` per environment
> - For private NuGet sources, add an authenticated `dotnet nuget add source` with `--username` and `--password ${{ secrets.<FEED_TOKEN> }} --store-password-in-clear-text`
> - The Sitecore NuGet feed step is **required** for building this project

### Step 5: Configure Sitecore to Use Your Function

#### Recommended Approach (No Secrets in URL)

Sitecore keeps a clean endpoint; the HTTP client sends the function key in header `x-functions-key`.

**Configuration**:
```
AssetUsageService.ApiEndpoint = https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI
AssetUsageService.FunctionKey = <FUNCTION_KEY>
```
Store the function key outside source control (e.g., Sitecore Secret Manager/Key Vault or appSetting).

When calling, set header: `x-functions-key: <FUNCTION_KEY>`

#### Legacy Approach (Query String)

If you must embed the code in the query string:

1. Get the function-level key (not the _master host key):
```bash
az functionapp function keys list \
  -g <RESOURCE_GROUP> \
  -n <FUNCTION_APP_NAME> \
  --function-name SitecorePublishAPI \
  --query "default" -o tsv
```

2. Update your Sitecore configuration:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <settings>
      <setting name="AssetUsageService.ApiEndpoint" 
               value="https://<FUNCTION_APP_NAME>.azurewebsites.net/api/SitecorePublishAPI?code=<FUNCTION_KEY>" />
    </settings>
  </sitecore>
</configuration>
```

### Step 6: Smoke Test

1. In Azure Portal, navigate to: Functions > SitecorePublishAPI > Test/Run
2. Set Header: `Content-Type=application/json`
3. Use this test body:
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

4. Expect `200`/`204` response
5. Verify in Application Insights:
   - Dependencies show `200`/`204` for Content Hub calls
   - MongoDB writes are visible in `<MONGO_DATABASE_NAME>`

### Troubleshooting

#### Key Vault Resolution Issues

After changing any secret version, restart the Function App or Save an app setting to force immediate re-resolution.

#### SASL Authentication Errors

- Ensure your Key Vault value matches a mongosh-tested SRV URI
- Include `tls=true` in connection string
- Add `authSource=admin` if your user is in the admin database
- Confirm hostname is the vCore SRV (`*.mongocluster.cosmos.azure.com`)

#### Missing "ContentHub:Endpoint" Error

- Verify `ContentHub__Endpoint` is set (note double underscore)
- Ensure `ClientId`/`ClientSecret` are present as Key Vault references and valid

#### Verify Settings in Kudu

Navigate to `/Env` in Kudu:
- Key Vault-backed settings should show "[Hidden - Resolved]"
- `WEBSITE_KEYVAULT_REFERENCES` section should show status "Resolved"

### Visual Studio Publish (Optional, One-Time)

For initial deployment or quick updates:

1. Right-click your Function project
2. Select Publish > Azure > Function App (Linux)
3. Select `<FUNCTION_APP_NAME>`
4. Click Publish

> **Note**: Use CI/CD workflow for ongoing deployments.
