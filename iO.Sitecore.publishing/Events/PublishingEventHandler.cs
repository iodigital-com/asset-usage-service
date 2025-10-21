using iO.Sitecore.Publishing.Services;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
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
        private readonly PublishTelemetryService publishTelemetryService;

        public PublishingEventHandler()
        {
            publishTelemetryService = new PublishTelemetryService(new AssetUsageServiceClient(), AuditLogPath);
        }

        public void OnItemProcessed(object sender, EventArgs eventArguments)
        {
            try
            {
                var itemProcessedArguments = eventArguments as ItemProcessedEventArgs;
                if (itemProcessedArguments == null)
                {
                    Log.Info("[OnItemProcessed] ItemProcessedEventArgs is null; returning.", this);
                    return;
                }

                var publishContext = itemProcessedArguments.Context;
                if (publishContext == null)
                {
                    Log.Info("[OnItemProcessed] Context is null; returning.", this);
                    return;
                }

                var itemId = publishContext.ItemId;
                if (ID.IsNullOrEmpty(itemId))
                {
                    Log.Info("[OnItemProcessed] ItemId is null or empty; returning.", this);
                    return;
                }

                var publishOptions = publishContext.PublishOptions;
                if (publishOptions == null)
                {
                    Log.Info("[OnItemProcessed] PublishOptions is null; returning.", this);
                    return;
                }

                var sourceDatabase = publishOptions.SourceDatabase;
                if (sourceDatabase == null)
                {
                    Log.Info("[OnItemProcessed] SourceDatabase is null; returning.", this);
                    return;
                }

                var versionToPublish = publishContext.VersionToPublish;
                var itemLanguage = versionToPublish?.Language ?? Language.Current;
                var itemVersion = versionToPublish?.Version ?? Version.Latest;

                var sourceItem = sourceDatabase.GetItem(itemId, itemLanguage, itemVersion);
                if (sourceItem == null)
                {
                    Log.Info($"[OnItemProcessed] Could not retrieve item {itemId} from {sourceDatabase.Name}; returning.", this);
                    return;
                }

                publishTelemetryService.RecordItemProcessed(sourceItem, publishOptions, sourceDatabase.Name);
            }
            catch (Exception exception)
            {
                Log.Error("[OnItemProcessed] Error", exception, this);
            }
            finally
            {
                Log.Info("[OnItemProcessed] Exit.", this);
            }
        }

        public void OnPublishEnd(object sender, EventArgs eventArguments)
        {
            try
            {
                var publisher = Event.ExtractParameter<Publisher>(eventArguments, 0) as Publisher;
                if (publisher == null)
                {
                    Log.Info("[OnPublishEnd] Publisher is null; returning.", this);
                    return;
                }

                var publishOptions = publisher.Options;
                if (publishOptions == null)
                {
                    Log.Info("[OnPublishEnd] Publisher.Options is null; returning.", this);
                    return;
                }

                var rootItem = publishOptions.RootItem;
                var targetDatabaseName = publishOptions.TargetDatabase?.Name ?? string.Empty;
                if (rootItem == null)
                {
                    Log.Info("[OnPublishEnd] RootItem is null; returning.", this);
                    return;
                }

                var itemsToLog = publishOptions.Deep
                    ? rootItem.Axes.GetDescendants().Concat(new[] { rootItem })
                    : new[] { rootItem };

                var totalItemsCount = 0;
                foreach (var publishedItem in itemsToLog)
                {
                    totalItemsCount++;
                    var sourceDatabaseName = publishedItem.Database?.Name ?? string.Empty;
                    publishTelemetryService.RecordPublishEndItem(publishedItem, publishOptions, sourceDatabaseName, targetDatabaseName);
                }

                Log.Info($"[OnPublishEnd] Logged {totalItemsCount} item(s).", this);
            }
            catch (Exception exception)
            {
                Log.Error("[OnPublishEnd] Error", exception, this);
            }
            finally
            {
                Log.Info("[OnPublishEnd] Exit.", this);
            }
        }

        public void OnPublishEndRemote(object sender, EventArgs eventArguments)
        {
            try
            {
                var publishEndRemoteArguments = eventArguments as PublishEndRemoteEventArgs;
                if (publishEndRemoteArguments == null)
                {
                    Log.Info("[OnPublishEndRemote] PublishEndRemoteEventArgs is null; returning.", this);
                    return;
                }

                var databases = Factory.GetDatabases()
                    .Where(database => database.RemoteEvents.EventQueue.Name == publishEndRemoteArguments.EventQueueName)
                    .ToList();

                var databaseNames = databases.Select(database => database.Name).ToList();
                publishTelemetryService.RecordPublishEndRemote(publishEndRemoteArguments.EventQueueName, databaseNames);
            }
            catch (Exception exception)
            {
                Log.Error("[OnPublishEndRemote] Error", exception, this);
            }
            finally
            {
                Log.Info("[OnPublishEndRemote] Exit.", this);
            }
        }
    }
}