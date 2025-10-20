using iO.Sitecore.publishing.Events;
using iO.Sitecore.Publishing.Services;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;
using System.Linq;
using Version = Sitecore.Data.Version;

namespace iO.Sitecore.Publishing.Events
{
    public sealed class PublishingEventHandler
    {
        private const string AuditLogPath = @"C:\inetpub\wwwroot\SitecoreXPLocalsc.dev.local\App_Data\logs\published-items.json";
        private readonly PublishTelemetryService _telemetry;

        public PublishingEventHandler()
        {
            _telemetry = new PublishTelemetryService(new AssetUsageServiceClient(), AuditLogPath);
        }

        public void OnItemProcessed(object sender, EventArgs args)
        {
            try
            {
                var itemProcessedArgs = args as ItemProcessedEventArgs;
                if (itemProcessedArgs == null)
                {
                    Log.Info("[OnItemProcessed] ItemProcessedEventArgs is null; returning.", this);
                    return;
                }

                var context = itemProcessedArgs.Context;
                if (context == null)
                {
                    Log.Info("[OnItemProcessed] Context is null; returning.", this);
                    return;
                }

                var itemId = context.ItemId;
                if (ID.IsNullOrEmpty(itemId))
                {
                    Log.Info("[OnItemProcessed] ItemId is null or empty; returning.", this);
                    return;
                }

                var options = context.PublishOptions;
                if (options == null)
                {
                    Log.Info("[OnItemProcessed] PublishOptions is null; returning.", this);
                    return;
                }

                var sourceDb = options.SourceDatabase;
                if (sourceDb == null)
                {
                    Log.Info("[OnItemProcessed] SourceDatabase is null; returning.", this);
                    return;
                }

                var versionToPublish = context.VersionToPublish;
                var language = versionToPublish?.Language ?? Language.Current;
                var version = versionToPublish?.Version ?? Version.Latest;

                var item = sourceDb.GetItem(itemId, language, version);
                if (item == null)
                {
                    Log.Info($"[OnItemProcessed] Could not retrieve item {itemId} from {sourceDb.Name}; returning.", this);
                    return;
                }

                _telemetry.RecordItemProcessed(item, options, sourceDb.Name);
            }
            catch (Exception ex)
            {
                Log.Error("[OnItemProcessed] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnItemProcessed] Exit.", this);
            }
        }

        public void OnPublishEnd(object sender, EventArgs args)
        {
            try
            {
                var publisher = Event.ExtractParameter<Publisher>(args, 0) as Publisher;
                if (publisher == null)
                {
                    Log.Info("[OnPublishEnd] Publisher is null; returning.", this);
                    return;
                }

                var options = publisher.Options;
                if (options == null)
                {
                    Log.Info("[OnPublishEnd] Publisher.Options is null; returning.", this);
                    return;
                }

                var rootItem = options.RootItem;
                var targetDbName = options.TargetDatabase?.Name ?? string.Empty;
                if (rootItem == null)
                {
                    Log.Info("[OnPublishEnd] RootItem is null; returning.", this);
                    return;
                }

                var itemsToLog = options.Deep
                    ? rootItem.Axes.GetDescendants().Concat(new[] { rootItem })
                    : new[] { rootItem };

                var count = 0;
                foreach (var item in itemsToLog)
                {
                    count++;
                    var sourceDbName = item.Database?.Name ?? string.Empty;
                    _telemetry.RecordPublishEndItem(item, options, sourceDbName, targetDbName);
                }

                Log.Info($"[OnPublishEnd] Logged {count} item(s).", this);
            }
            catch (Exception ex)
            {
                Log.Error("[OnPublishEnd] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnPublishEnd] Exit.", this);
            }
        }

        public void OnPublishEndRemote(object sender, EventArgs args)
        {
            try
            {
                var remoteArgs = args as PublishEndRemoteEventArgs;
                if (remoteArgs == null)
                {
                    Log.Info("[OnPublishEndRemote] PublishEndRemoteEventArgs is null; returning.", this);
                    return;
                }

                var dbs = Factory.GetDatabases()
                    .Where(db => db.RemoteEvents.EventQueue.Name == remoteArgs.EventQueueName)
                    .ToList();

                var names = dbs.Select(d => d.Name).ToList();
                _telemetry.RecordPublishEndRemote(remoteArgs.EventQueueName, names);
            }
            catch (Exception ex)
            {
                Log.Error("[OnPublishEndRemote] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnPublishEndRemote] Exit.", this);
            }
        }
    }
}