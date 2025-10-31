using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Services;
using Sitecore.Configuration;
using Sitecore.Data.Events;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;

namespace iO.Sitecore.publishing.Events
{
    public class PublishEventHandler
    {
        private static IPublishTelemetryService _telemetryService;
        private static readonly object _telemetryLock = new object();

        protected void OnItemProcessing(object sender, EventArgs args)
        {
            try
            {
                var eventArgs = args as ItemProcessingEventArgs;
                var telemetryService = GetTelemetryService();
                telemetryService?.ProcessItemProcessing(eventArgs);
            }
            catch (Exception ex)
            {
                Log.Error($"PublishEventHandler.OnItemProcessing: Error processing item. Exception: {ex.Message}", ex, this);
            }
        }

        protected void OnItemProcessed(object sender, EventArgs args)
        {
            try
            {
                var eventArgs = args as ItemProcessedEventArgs;
                var telemetryService = GetTelemetryService();
                telemetryService?.ProcessItemProcessed(eventArgs);
            }
            catch (Exception ex)
            {
                Log.Error($"PublishEventHandler.OnItemProcessed: Error processing item. Exception: {ex.Message}", ex, this);
            }
        }

        protected async void OnPublishEnd(object sender, EventArgs args)
        {
            try
            {
                var telemetryService = GetTelemetryService();
                if (telemetryService != null)
                {
                    await telemetryService.ProcessPublishEndAsync(args);
                }
            }
            catch (Exception ex)
            {
                Log.Error("PublishEventHandler.OnPublishEnd: Error in publish end handler.", ex, this);
            }
        }

        protected void OnPublishEndRemote(object sender, EventArgs args)
        {
            try
            {
                var eventArgs = Event.ExtractParameter<PublishEndRemoteEventArgs>(args, 0);
                var telemetryService = GetTelemetryService();
                telemetryService?.ProcessPublishEndRemote(eventArgs);
            }
            catch (Exception ex)
            {
                Log.Error("PublishEventHandler.OnPublishEndRemote: Error in publish end remote handler.", ex, this);
            }
        }

        private IPublishTelemetryService GetTelemetryService()
        {
            if (_telemetryService != null)
                return _telemetryService;

            lock (_telemetryLock)
            {
                if (_telemetryService != null)
                    return _telemetryService;

                try
                {
                    var auditLogPath = Settings.GetSetting("AssetUsage.AuditLogPath",
                        System.IO.Path.Combine(Settings.DataFolder, "logs", "published-items.json"));

                    var client = new AssetUsageServiceClient();
                    _telemetryService = new PublishTelemetryService(client, auditLogPath);

                    Log.Info("PublishEventHandler.GetTelemetryService: Telemetry service initialized successfully.", this);
                    return _telemetryService;
                }
                catch (Exception ex)
                {
                    Log.Error("PublishEventHandler.GetTelemetryService: Failed to initialize telemetry service.", ex, this);
                    return null;
                }
            }
        }
    }
}