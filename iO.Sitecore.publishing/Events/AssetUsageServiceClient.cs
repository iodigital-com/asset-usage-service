using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Sitecore.Configuration;
using Sitecore.Diagnostics;

namespace iO.Sitecore.publishing.Events
{
    public class AssetUsageServiceClient
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly string _endpoint;

        public AssetUsageServiceClient()
        {
            _endpoint = Settings.GetSetting("AssetUsageService.Endpoint", "http://localhost:7183/api/SitecorePublishAPI");
        }

        public async Task SendAsync(AssetUsageEvent payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(_endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    Log.Info($"[AssetUsageServiceClient] Successfully sent data for ItemId={payload.ItemId}", this);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Log.Error($"[AssetUsageServiceClient] Failed. Status={response.StatusCode}, Body={body}", this);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AssetUsageServiceClient] Error sending to Azure Function", ex, this);
            }
        }
    }
}