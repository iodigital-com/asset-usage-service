using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Events
{
    /// <summary>
    /// Client for sending asset usage events to the Azure Function endpoint.
    /// Requires the "AssetUsageService.ApiEndpoint" setting to be configured in Sitecore configuration files
    /// with a valid HTTP/HTTPS URL pointing to the Azure Function endpoint.
    /// </summary>
    public class AssetUsageServiceClient : IDisposable
    {
        private const string ApiEndpointSettingName = "AssetUsageService.ApiEndpoint";

        private static readonly Lazy<HttpClient> LazyHttpClient = new Lazy<HttpClient>(CreateHttpClient, LazyThreadSafetyMode.ExecutionAndPublication);
        private static HttpClient SharedHttpClient => LazyHttpClient.Value;
        private readonly string endpointUrl;
        private readonly JsonSerializerOptions jsonOptions;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetUsageServiceClient"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the "AssetUsageService.ApiEndpoint" setting is not configured or contains an invalid URL.
        /// </exception>
        /// <remarks>
        /// The "AssetUsageService.ApiEndpoint" setting must be configured in Sitecore configuration files
        /// (e.g., App_Config/Include or App_Config/Layers) with a valid HTTP/HTTPS URL.
        /// Example: &lt;setting name="AssetUsageService.ApiEndpoint" value="https://your-function-app.azurewebsites.net/api/endpoint" /&gt;
        /// </remarks>
        public AssetUsageServiceClient()
        {
            endpointUrl = GetAndValidateEndpointUrl();
            jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };
        }

        public async Task SendAsync(AssetUsageEvent payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (payload == null)
            {
                Log.Warn("[AssetUsageServiceClient] Payload is null; skipping send.", typeof(AssetUsageServiceClient));
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.ItemId))
            {
                Log.Warn("[AssetUsageServiceClient] Payload ItemId is null or empty; skipping send.", typeof(AssetUsageServiceClient));
                return;
            }

            HttpResponseMessage httpResponse = null;
            try
            {
                string jsonPayload = JsonSerializer.Serialize(payload, jsonOptions);

                using (var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    httpResponse = await SharedHttpClient.PostAsync(endpointUrl, httpContent, cancellationToken).ConfigureAwait(false);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        Log.Info($"[AssetUsageServiceClient] Successfully sent data for ItemId={payload.ItemId}", typeof(AssetUsageServiceClient));
                    }
                    else
                    {
                        await HandleHttpError(httpResponse, payload.ItemId).ConfigureAwait(false);
                    }
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || cancellationToken.IsCancellationRequested)
            {
                Log.Warn($"[AssetUsageServiceClient] Request timeout or cancelled for ItemId={payload.ItemId}", typeof(AssetUsageServiceClient));
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[AssetUsageServiceClient] HTTP request failed for ItemId={payload.ItemId}. Error: {ex.Message}", ex, typeof(AssetUsageServiceClient));
            }
            catch (JsonException ex)
            {
                Log.Error($"[AssetUsageServiceClient] JSON serialization failed for ItemId={payload.ItemId}. Error: {ex.Message}", ex, typeof(AssetUsageServiceClient));
            }
            catch (Exception ex)
            {
                Log.Error($"[AssetUsageServiceClient] Unexpected error sending data for ItemId={payload.ItemId}. Error: {ex.Message}", ex, typeof(AssetUsageServiceClient));
            }
            finally
            {
                httpResponse?.Dispose();
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            return client;
        }

        private string GetAndValidateEndpointUrl()
        {
            var url = Settings.GetSetting(ApiEndpointSettingName, string.Empty);

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException($"{ApiEndpointSettingName} setting is required but not configured.");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new InvalidOperationException($"{ApiEndpointSettingName} setting '{url}' is not a valid HTTP/HTTPS URL.");
            }

            Log.Info($"[AssetUsageServiceClient] Using Azure Function endpoint: {url}", typeof(AssetUsageServiceClient));

            return url;
        }

        private async Task HandleHttpError(HttpResponseMessage response, string itemId)
        {
            string responseBody = string.Empty;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn($"[AssetUsageServiceClient] Could not read error response body: {ex.Message}", typeof(AssetUsageServiceClient));
                responseBody = "[Could not read response]";
            }

            Log.Error($"[AssetUsageServiceClient] HTTP request failed for ItemId={itemId}. " +
                     $"Status: {response.StatusCode} ({(int)response.StatusCode}), " +
                     $"Body: {responseBody?.Substring(0, Math.Min(responseBody.Length, 500))}",
                     typeof(AssetUsageServiceClient));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AssetUsageServiceClient));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                disposed = true;
            }
        }

        ~AssetUsageServiceClient()
        {
            Dispose(false);
        }
    }
}