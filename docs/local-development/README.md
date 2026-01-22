# Local Development

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
