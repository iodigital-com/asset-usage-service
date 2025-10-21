using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Events
{
    public class AssetUsageServiceClient
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly string endpointUrl;

        public AssetUsageServiceClient()
        {
            endpointUrl = Settings.GetSetting("AssetUsageService.Endpoint", "http://localhost:7183/api/SitecorePublishAPI");
        }

        public async Task SendAsync(AssetUsageEvent payload)
        {
            if (payload == null)
            {
                Log.Warn("[AssetUsageServiceClient] Payload is null; skipping send.", this);
                return;
            }

            try
            {
                string jsonPayload = JsonSerializer.Serialize(payload);
                using (var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage httpResponse = await SharedHttpClient.PostAsync(endpointUrl, httpContent);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        Log.Info($"[AssetUsageServiceClient] Successfully sent data for ItemId={payload.ItemId}", this);
                    }
                    else
                    {
                        string responseBody = await httpResponse.Content.ReadAsStringAsync();
                        Log.Error($"[AssetUsageServiceClient] Failed. Status={httpResponse.StatusCode}, Body={responseBody}", this);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("[AssetUsageServiceClient] Error sending to Azure Function", exception, this);
            }
        }
    }
}