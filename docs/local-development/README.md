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

4. Configure Local azure ServiceBus Queues 
Install [Docker desktop](https://docs.docker.com/desktop/setup/install/windows-install/) and make sure it is running 

Switch to Linux container (right click docker icon in menubar and click `Switch to linux containers...` if you see `Switch to windows containers...` you are already on linux containers) 

To run the azure servicebus Queue local go to the folder **DockerAzureServiceBusQueues** in your terminal and run:
```zsh
 docker-compose up -d
 ```
Wait for it to say:
- Container sqlserver:            `Healthy`
- Container servicebus-emulator:  `Started`

Check your `local.settings.json` to ensure you have the correct queue names and that the `Disabled` property of the queue functions you want to use is set to `false`

5. Start MongoDB:
```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```

6. Run the application:
```bash
func start
```

7. Configure Sitecore:
- Copy `iO.Sitecore.publishing.dll` to your Sitecore instance bin folder
- Create `iO.Publishing.Events.config` in `App_Config\Include\zzz.iO\`
- Create `AssetUsageService.config` in `App_Config\Include\`
- Update `AssetUsageService.ApiEndpoint` to point to your local function
- Restart Sitecore
