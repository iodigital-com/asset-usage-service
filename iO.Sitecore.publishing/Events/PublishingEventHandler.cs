using iO.Sitecore.Publishing.Services;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;
using System.Linq;
using Version = Sitecore.Data.Version;
using ID = Sitecore.Data.ID;

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

        public void OnPublishComplete(object sender, EventArgs eventArgs)
        {
            try
            {
                Log.Info("============ [OnPublishComplete] START ============", this);

                var sitecoreEventArgs = eventArgs as SitecoreEventArgs;
                if (sitecoreEventArgs == null)
                {
                    Log.Warn("[OnPublishComplete] EventArgs is not SitecoreEventArgs", this);
                    return;
                }

                Log.Info($"[OnPublishComplete] Event Name: {sitecoreEventArgs.EventName}", this);

                // Parameter[1]: Number of items processed
                var itemsProcessedCount = sitecoreEventArgs.Parameters[1];
                Log.Info($"[OnPublishComplete] *** ITEMS PROCESSED: {itemsProcessedCount} ***", this);

                // Parameter[0]: DistributedPublishOptions
                var param0 = sitecoreEventArgs.Parameters[0];
                if (param0 is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;

                        var itemType = item.GetType();

                        // Extract properties
                        var targetDbNameProp = itemType.GetProperty("TargetDatabaseName");
                        var sourceDbNameProp = itemType.GetProperty("SourceDatabaseName");
                        var rootItemIdProp = itemType.GetProperty("RootItemId");
                        var itemIdsToPublishProp = itemType.GetProperty("ItemIdsToPublish");
                        var languageNameProp = itemType.GetProperty("LanguageName");
                        var modeProp = itemType.GetProperty("Mode");
                        var publishDateProp = itemType.GetProperty("PublishDate");

                        var targetDbName = targetDbNameProp?.GetValue(item, null) as string;
                        var sourceDbName = sourceDbNameProp?.GetValue(item, null) as string;
                        var rootItemId = rootItemIdProp?.GetValue(item, null);
                        var itemIdsToPublish = itemIdsToPublishProp?.GetValue(item, null);
                        var languageName = languageNameProp?.GetValue(item, null) as string;
                        var mode = modeProp?.GetValue(item, null);
                        var publishDate = publishDateProp?.GetValue(item, null);

                        Log.Info("========== PUBLISH DETAILS ==========", this);
                        Log.Info($"[OnPublishComplete] Source Database: {sourceDbName}", this);
                        Log.Info($"[OnPublishComplete] Target Database: {targetDbName}", this);
                        Log.Info($"[OnPublishComplete] Mode: {mode}", this);
                        Log.Info($"[OnPublishComplete] Language: {languageName}", this);
                        Log.Info($"[OnPublishComplete] Publish Date: {publishDate}", this);
                        Log.Info($"[OnPublishComplete] Root Item ID: {rootItemId}", this);

                        // Get target database
                        if (!string.IsNullOrEmpty(targetDbName))
                        {
                            var targetDb = Factory.GetDatabase(targetDbName);
                            var sourceDb = Factory.GetDatabase(sourceDbName);

                            if (targetDb != null && sourceDb != null)
                            {
                                Log.Info("========== UPDATED ITEMS FROM TARGET DATABASE ==========", this);

                                // Get language
                                var language = string.IsNullOrEmpty(languageName)
                                    ? Language.Parse("en")
                                    : Language.Parse(languageName);

                                // Process ItemIdsToPublish collection
                                if (itemIdsToPublish != null && itemIdsToPublish is System.Collections.IEnumerable itemIds)
                                {
                                    var processedCount = 0;
                                    foreach (var itemIdObj in itemIds)
                                    {
                                        processedCount++;

                                        if (itemIdObj is Guid guid)
                                        {
                                            var itemId = new ID(guid);

                                            // Get item from TARGET database (published version)
                                            var targetItem = targetDb.GetItem(itemId, language);

                                            // Get item from SOURCE database (original version)
                                            var sourceItem = sourceDb.GetItem(itemId, language);

                                            if (targetItem != null)
                                            {
                                                Log.Info($"---------- UPDATED ITEM #{processedCount} ----------", this);
                                                Log.Info($"[OnPublishComplete] *** ITEM UPDATED IN TARGET ***", this);
                                                Log.Info($"  Path: {targetItem.Paths.FullPath}", this);
                                                Log.Info($"  ID: {targetItem.ID}", this);
                                                Log.Info($"  Name: {targetItem.Name}", this);
                                                Log.Info($"  Template: {targetItem.TemplateName} ({targetItem.TemplateID})", this);
                                                Log.Info($"  Language: {targetItem.Language.Name}", this);
                                                Log.Info($"  Version: {targetItem.Version.Number}", this);
                                                Log.Info($"  Updated: {targetItem.Statistics.Updated}", this);
                                                Log.Info($"  Updated By: {targetItem.Statistics.UpdatedBy}", this);
                                                Log.Info($"  Revision: {targetItem.Statistics.Revision}", this);

                                                // Log non-empty fields
                                                Log.Info($"  --- Non-Empty Fields ---", this);
                                                var nonEmptyFields = targetItem.Fields
                                                    .Where(f => !string.IsNullOrEmpty(f.Value) && !f.Name.StartsWith("__"))
                                                    .Take(10);

                                                foreach (var field in nonEmptyFields)
                                                {
                                                    var fieldValue = field.Value.Length > 100
                                                        ? field.Value.Substring(0, 100) + "..."
                                                        : field.Value;
                                                    Log.Info($"    {field.Name}: {fieldValue}", this);
                                                }

                                                // Compare with source to see if it was actually updated
                                                if (sourceItem != null)
                                                {
                                                    var sourceRevision = sourceItem.Statistics.Revision;
                                                    var targetRevision = targetItem.Statistics.Revision;

                                                    if (sourceRevision == targetRevision)
                                                    {
                                                        Log.Info($"  Status: ✓ UPDATED (Revisions match: {sourceRevision})", this);
                                                    }
                                                    else
                                                    {
                                                        Log.Info($"  Status: ⚠ SKIPPED (Source: {sourceRevision}, Target: {targetRevision})", this);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Log.Warn($"[OnPublishComplete] Item {itemId} not found in target database", this);

                                                if (sourceItem != null)
                                                {
                                                    Log.Info($"  Item exists in source: {sourceItem.Paths.FullPath}", this);
                                                    Log.Info($"  Status: ⚠ SKIPPED or FAILED", this);
                                                }
                                            }
                                        }
                                    }

                                    Log.Info($"[OnPublishComplete] Total Items in Collection: {processedCount}", this);
                                }
                                else
                                {
                                    Log.Warn("[OnPublishComplete] ItemIdsToPublish is null or not enumerable", this);
                                }
                            }
                            else
                            {
                                Log.Warn($"[OnPublishComplete] Could not get databases. Target: {targetDb?.Name}, Source: {sourceDb?.Name}", this);
                            }
                        }

                        break; // Only process first DistributedPublishOptions
                    }
                }

                Log.Info("============ [OnPublishComplete] END ============", this);
            }
            catch (Exception ex)
            {
                Log.Error($"[OnPublishComplete] Error: {ex.Message}", ex, this);
            }
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