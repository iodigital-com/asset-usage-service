# Testing

## Running Tests

```bash
# Run all tests
dotnet test

# Run performance tests only
dotnet test --filter "Category=Performance"

# Run integration tests only
dotnet test --filter "Category=Integration"

# Run unit tests only
dotnet test --filter "Category=Unit"
```

## Test Categories

- **Unit Tests**: Business logic and service tests
- **Integration Tests**: ContentHub and MongoDB integration
- **Performance Tests**: Repository performance benchmarks

## Example Test Data

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
## Azure ServiceBus test 
To run the serviceBus tests create a file named `appsettings.Test.json` in the AssetUsageServiceTests directory and paste this in for the local queue:
```json
{
  "ServiceBusQueue": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
  }
}
```
Open `\AssetUsageServiceTests\AssetUsageServiceTests.csproj` file and within the project tags
```xml
 <Project Sdk="Microsoft.NET.Sdk"> 

 </Project>
 ```  
Add the following code:
```xml 
<ItemGroup>
  <None Update="appsettings.Test.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## 
## Setup Steps

1. **Edit `appsettings.Test.json`** with your ContentHub credentials:

```json
{
  "ServiceBusQueue": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
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

2. **Set `IntegrationTestsEnabled` to `true`** to enable tests

3. **Provide valid test data**:
   - `TestPublicLink`: A single valid public link from your ContentHub instance
   - `TestPublicLinks`: An array of 2+ valid public links for multi-link tests

## Getting Test Public Links

1. Log in to your ContentHub instance
2. Navigate to an asset that has a public link
3. Copy the public link URL
4. Paste it into `appsettings.Test.json`
