using iO.Sitecore.publishing.Models;
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;
using Sitecore.Resources.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Version = Sitecore.Data.Version;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class PublishTelemetryService
    {
        private readonly AssetUsageServiceClient assetUsageClient;
        private readonly string auditLogPath;

        private static readonly ConcurrentDictionary<string, ItemProcessingInfo> _processingItems = new ConcurrentDictionary<string, ItemProcessingInfo>();
        private static readonly ConcurrentBag<ItemUpdateInfo> _updatedItems = new ConcurrentBag<ItemUpdateInfo>();
        private static readonly ConcurrentDictionary<string, PublishContextInfo> _publishContexts = new ConcurrentDictionary<string, PublishContextInfo>();
        private static readonly object _statsLock = new object();
        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public PublishTelemetryService(AssetUsageServiceClient client, string auditLogPath)
        {
            assetUsageClient = client ?? throw new ArgumentNullException(nameof(client));
            this.auditLogPath = string.IsNullOrWhiteSpace(auditLogPath) ? throw new ArgumentException(nameof(auditLogPath)) : auditLogPath;
        }

        public void ProcessItemProcessing(ItemProcessingEventArgs eventArgs)
        {
            if (eventArgs?.Context?.PublishOptions == null)
            {
                Log.Debug("PublishTelemetryService.ProcessItemProcessing: Invalid event arguments or context.", this);
                return;
            }

            var context = eventArgs.Context;
            var publishContext = context.PublishContext;

            StorePublishContext(publishContext);

            var (language, version, hasVersionInfo) = GetLanguageAndVersion(context, publishContext);
            var sourceItem = GetSourceItem(context, publishContext, language, version);

            if (sourceItem == null)
            {
                Log.Debug($"PublishTelemetryService.ProcessItemProcessing: Source item not found. ItemID: {context.ItemId}", this);
                return;
            }

            var itemKey = CreateItemKey(context.ItemId, language, version, hasVersionInfo);
            var (sourceRevisionId, sourceUpdated) = GetItemRevisionInfo(sourceItem);
            var targetItem = GetTargetItem(context, publishContext, language, version);
            var (targetRevisionId, targetUpdated) = GetItemRevisionInfo(targetItem);

            var processingInfo = new ItemProcessingInfo
            {
                ItemId = context.ItemId,
                ItemPath = sourceItem.Paths.FullPath,
                Language = language.Name,
                Version = version.Number,
                SourceRevisionId = sourceRevisionId,
                SourceUpdated = sourceUpdated,
                TargetRevisionId = targetRevisionId,
                TargetUpdated = targetUpdated,
                Action = context.Action.ToString(),
                ProcessingTime = DateTime.UtcNow,
                HasVersionInfo = hasVersionInfo
            };

            _processingItems.AddOrUpdate(itemKey, processingInfo, (key, existing) => processingInfo);

            Log.Info($"PublishTelemetryService.ProcessItemProcessing: " +
                $"ItemID: {context.ItemId}, " +
                $"Path: '{sourceItem.Paths.FullPath}', " +
                $"Language: {language.Name}, " +
                $"Version: {version.Number}, " +
                $"HasVersionInfo: {hasVersionInfo}, " +
                $"ItemKey: '{itemKey}', " +
                $"Action: {context.Action}, " +
                $"SourceRevision: {sourceRevisionId}, " +
                $"SourceUpdated: {sourceUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TargetRevision: {targetRevisionId}, " +
                $"TargetUpdated: {targetUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TargetExists: {targetItem != null}, " +
                $"PublishMode: {publishContext.PublishOptions.Mode}, " +
                $"Deep: {publishContext.PublishOptions.Deep}, " +
                $"CompareRevisions: {publishContext.PublishOptions.CompareRevisions}",
                this);
        }

        public void ProcessItemProcessed(ItemProcessedEventArgs eventArgs)
        {
            if (eventArgs?.Context?.PublishOptions == null)
            {
                Log.Debug("PublishTelemetryService.ProcessItemProcessed: Invalid event arguments or context.", this);
                return;
            }

            var context = eventArgs.Context;
            var publishContext = context.PublishContext;

            var (language, version, hasVersionInfo) = GetLanguageAndVersion(context, publishContext);
            var processingInfo = FindProcessingInfo(context.ItemId, language, version);

            if (processingInfo == null)
            {
                Log.Warn($"PublishTelemetryService.ProcessItemProcessed: No processing info found for item {context.ItemId}, " +
                    $"Language: {language.Name}, Version: {version.Number}, " +
                    $"HasVersionInfo: {hasVersionInfo}", this);
                return;
            }

            var publishedItem = GetTargetItem(context, publishContext, language, version);
            if (publishedItem == null)
            {
                Log.Debug($"PublishTelemetryService.ProcessItemProcessed: Published item not found in target. ItemID: {context.ItemId}", this);
                return;
            }

            var (newRevisionId, newUpdated) = GetItemRevisionInfo(publishedItem);
            var result = DeterminePublishResult(processingInfo, newRevisionId, newUpdated, context.Action);

            Log.Info($"PublishTelemetryService.ProcessItemProcessed: " +
                $"ItemID: {context.ItemId}, " +
                $"Path: '{processingInfo.ItemPath}', " +
                $"Language: {processingInfo.Language}, " +
                $"Version: {processingInfo.Version}, " +
                $"HasVersionInfo: {processingInfo.HasVersionInfo}, " +
                $"Action: {processingInfo.Action}, " +
                $"Result: {result}, " +
                $"OldRevision: {processingInfo.TargetRevisionId}, " +
                $"NewRevision: {newRevisionId}, " +
                $"RevisionChanged: {processingInfo.TargetRevisionId != newRevisionId}, " +
                $"OldUpdated: {processingInfo.TargetUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"NewUpdated: {newUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TimestampChanged: {processingInfo.TargetUpdated != newUpdated}, " +
                $"ProcessingDuration: {(DateTime.UtcNow - processingInfo.ProcessingTime).TotalMilliseconds}ms",
                this);

            if (IsItemUpdated(processingInfo, newRevisionId, newUpdated))
            {
                var updateInfo = new ItemUpdateInfo
                {
                    ItemId = context.ItemId,
                    ItemPath = processingInfo.ItemPath,
                    Language = processingInfo.Language,
                    Version = processingInfo.Version,
                    OldRevisionId = processingInfo.TargetRevisionId,
                    NewRevisionId = newRevisionId,
                    OldUpdated = processingInfo.TargetUpdated,
                    NewUpdated = newUpdated,
                    Action = processingInfo.Action,
                    Result = result,
                    PublishMode = publishContext.PublishOptions.Mode.ToString(),
                    TargetDatabaseName = publishContext.PublishOptions.TargetDatabase?.Name ?? "unknown",
                    SourceDatabaseName = publishContext.PublishOptions.SourceDatabase?.Name ?? "unknown"
                };

                _updatedItems.Add(updateInfo);

                Log.Info($"PublishTelemetryService.ProcessItemProcessed: Item marked as UPDATED. " +
                    $"ItemID: {context.ItemId}, " +
                    $"Path: '{processingInfo.ItemPath}', " +
                    $"RevisionChange: {processingInfo.TargetRevisionId} -> {newRevisionId}",
                    this);
            }
        }

        public void ProcessPublishEnd(EventArgs args)
        {
            var publishOptions = ExtractPublishOptions(args);
            if (publishOptions == null)
            {
                Log.Warn("PublishTelemetryService.ProcessPublishEnd: Could not retrieve publish options.", this);
                return;
            }

            LogPublishCompletion();
            LogPublishOptions(publishOptions);
            LogPublishStatistics();

            var updatedItemsList = _updatedItems.ToList();
            LogUpdatedItems(updatedItemsList);

            if (updatedItemsList.Any())
            {
                SendUpdatedItemsToTelemetry(updatedItemsList, publishOptions);
            }

            LogRevisionSummary();
            ClearBuffers();
        }

        public void ProcessPublishEndRemote(PublishEndRemoteEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                Log.Debug("PublishTelemetryService.ProcessPublishEndRemote: Invalid event arguments.", this);
                return;
            }

            var languageInfo = string.IsNullOrEmpty(eventArgs.LanguageName) ? "All" : eventArgs.LanguageName;
            var sourceDb = string.IsNullOrEmpty(eventArgs.SourceDatabaseName) ? "N/A" : eventArgs.SourceDatabaseName;
            var targetDb = string.IsNullOrEmpty(eventArgs.TargetDatabaseName) ? "N/A" : eventArgs.TargetDatabaseName;

            Log.Info($"PublishTelemetryService.ProcessPublishEndRemote: Remote publish end notification received. " +
                $"RootItemID: {eventArgs.RootItemId}, " +
                $"Mode: {eventArgs.Mode}, " +
                $"Deep: {eventArgs.Deep}, " +
                $"Language: {languageInfo}, " +
                $"SourceDB: {sourceDb}, " +
                $"TargetDB: {targetDb}",
                this);

            var databases = Factory.GetDatabases()
                .Where(database => database.RemoteEvents.EventQueue.Name == eventArgs.EventQueueName)
                .Select(database => database.Name)
                .ToList();

            RecordPublishEndRemote(eventArgs.EventQueueName, databases);
            Log.Info("PublishTelemetryService.ProcessPublishEndRemote: Sent remote publish event to telemetry service.", this);
        }

        private static List<string> ExtractAssetIds(Item item)
        {
            var assetIdsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (fieldTypeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("DamId"));
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("dam-id"));
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("stylelabs-content-id"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Thumbnail"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("thumbnailsrc"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Source"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("src"));
                            break;

                        case "general link":
                        case "link":
                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("DamId"));
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("dam-id"));
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("stylelabs-content-id"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("url"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("href"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("Source"));
                            }
                            catch (Exception exception)
                            {
                                Log.Warn($"[ExtractAssetIds] Malformed link XML in field '{field.Name}' on '{item.Paths.FullPath}'", exception, typeof(PublishTelemetryService));
                            }
                            break;

                        default:
                            foreach (Match urlMatch in GatewayIdRegex.Matches(field.Value))
                            {
                                if (urlMatch.Success && urlMatch.Groups.Count > 1)
                                {
                                    AddIfNotEmpty(assetIdsSet, urlMatch.Groups[1].Value);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warn($"[ExtractAssetIds] Error for item {item.Paths.FullPath}", exception, typeof(PublishTelemetryService));
            }

            return assetIdsSet.ToList();
        }

        private static string ExtractPublicLink(Item item)
        {
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (fieldTypeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            var contentHubUrl = FirstNonEmpty(
                                imageField.GetAttribute("Source"),
                                imageField.GetAttribute("source"),
                                imageField.GetAttribute("src"),
                                imageField.GetAttribute("url"),
                                imageField.GetAttribute("public_link")
                            );
                            if (!string.IsNullOrEmpty(contentHubUrl)) return contentHubUrl;

                            if (imageField.MediaItem != null)
                            {
                                var mediaUrl = MediaManager.GetMediaUrl(imageField.MediaItem);
                                if (!string.IsNullOrWhiteSpace(mediaUrl)) return mediaUrl;
                            }
                            break;

                        case "general link":
                        case "link":
                            var linkField = new LinkField(field);
                            var linkFieldUrl = FirstNonEmpty(linkField.Url);
                            if (!string.IsNullOrEmpty(linkFieldUrl)) return linkFieldUrl;

                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                var mappedUrl = FirstNonEmpty(
                                    (string)xmlElement.Attribute("url"),
                                    (string)xmlElement.Attribute("href"),
                                    (string)xmlElement.Attribute("Source"),
                                    (string)xmlElement.Attribute("source"),
                                    (string)xmlElement.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(mappedUrl)) return mappedUrl;
                            }
                            catch (Exception exception)
                            {
                                Log.Warn($"[ExtractPublicLink] Malformed link XML in field '{field.Name}'", exception, typeof(PublishTelemetryService));
                            }
                            break;

                        case "file":
                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                var fileUrl = FirstNonEmpty(
                                    (string)xmlElement.Attribute("url"),
                                    (string)xmlElement.Attribute("href"),
                                    (string)xmlElement.Attribute("Source"),
                                    (string)xmlElement.Attribute("src"),
                                    (string)xmlElement.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(fileUrl)) return fileUrl;
                            }
                            catch (Exception exception)
                            {
                                Log.Warn($"[ExtractPublicLink] Malformed file XML in field '{field.Name}'", exception, typeof(PublishTelemetryService));
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[ExtractPublicLink] Error extracting public link from item {item.Paths.FullPath}", exception, typeof(PublishTelemetryService));
            }

            return string.Empty;
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            var trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                sink.Add(trimmedValue);
                Log.Info($"[AddIfNotEmpty] Added id '{trimmedValue}'", typeof(PublishTelemetryService));
            }
        }

        private static void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var urlMatch = GatewayIdRegex.Match(url);
            if (urlMatch.Success && urlMatch.Groups.Count > 1)
            {
                var gatewayIdValue = urlMatch.Groups[1].Value;
                sink.Add(gatewayIdValue);
                Log.Info($"[ExtractIdsFromUrl] Extracted id '{gatewayIdValue}' from URL '{url}'", typeof(PublishTelemetryService));
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var valueCandidate in values)
            {
                if (!string.IsNullOrWhiteSpace(valueCandidate)) return valueCandidate.Trim();
            }
            return string.Empty;
        }

        private (Language language, Version version, bool hasVersionInfo) GetLanguageAndVersion(PublishItemContext context, PublishContext publishContext)
        {
            bool hasVersionInfo = context.VersionToPublish != null;
            Language language = hasVersionInfo ? context.VersionToPublish.Language : (publishContext.PublishOptions.Language ?? Language.Parse("en"));
            Version version = hasVersionInfo ? context.VersionToPublish.Version : Version.Latest;
            return (language, version, hasVersionInfo);
        }

        private Item GetSourceItem(PublishItemContext context, PublishContext publishContext, Language language, Version version)
        {
            if (context.ItemId == ID.Null) return null;
            return publishContext.PublishOptions.SourceDatabase.GetItem(context.ItemId, language, version) ??
                   publishContext.PublishOptions.SourceDatabase.GetItem(context.ItemId);
        }

        private Item GetTargetItem(PublishItemContext context, PublishContext publishContext, Language language, Version version)
        {
            return publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId, language, version) ??
                   publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId);
        }

        private string CreateItemKey(ID itemId, Language language, Version version, bool hasVersionInfo)
        {
            return hasVersionInfo ? $"{itemId}_{language.Name}_{version.Number}" : itemId.ToString();
        }

        private (ID revisionId, DateTime updated) GetItemRevisionInfo(Item item)
        {
            if (item == null) return (ID.Null, DateTime.MinValue);
            var revisionStr = item.Statistics.Revision;
            var revisionId = string.IsNullOrEmpty(revisionStr) ? ID.Null : ID.Parse(revisionStr);
            return (revisionId, item.Statistics.Updated);
        }

        private ItemProcessingInfo FindProcessingInfo(ID itemId, Language language, Version version)
        {
            string itemKeyWithVersion = $"{itemId}_{language.Name}_{version.Number}";
            string itemKeyWithoutVersion = itemId.ToString();

            return _processingItems.TryGetValue(itemKeyWithVersion, out var info) ? info :
                   _processingItems.TryGetValue(itemKeyWithoutVersion, out info) ? info : null;
        }

        private string DeterminePublishResult(ItemProcessingInfo processingInfo, ID newRevisionId, DateTime newUpdated, PublishAction action)
        {
            if (processingInfo.TargetRevisionId == ID.Null) return "Created";
            if (processingInfo.TargetRevisionId != newRevisionId) return "Updated";
            if (processingInfo.TargetUpdated != newUpdated) return "Modified";
            if (action == PublishAction.None) return "Skipped";
            return "Processed";
        }

        private bool IsItemUpdated(ItemProcessingInfo processingInfo, ID newRevisionId, DateTime newUpdated)
        {
            if (processingInfo.TargetRevisionId == ID.Null && newRevisionId != ID.Null) return true;
            if (processingInfo.TargetRevisionId != newRevisionId && newRevisionId != ID.Null) return true;
            return Math.Abs((processingInfo.TargetUpdated - newUpdated).TotalSeconds) > 1;
        }

        private PublishOptions ExtractPublishOptions(EventArgs args)
        {
            var publisher = Event.ExtractParameter(args, 0) as Publisher;
            if (publisher?.Options != null) return publisher.Options;

            var contextInfo = _publishContexts.Values.FirstOrDefault();
            return contextInfo?.PublishOptions;
        }

        private void StorePublishContext(PublishContext publishContext)
        {
            if (publishContext?.PublishOptions == null) return;

            var sourceDbName = publishContext.PublishOptions.SourceDatabase?.Name ?? "Unknown";
            var targetDbName = publishContext.PublishOptions.TargetDatabase?.Name ?? "Unknown";
            var contextKey = $"{sourceDbName}_{targetDbName}_{DateTime.UtcNow.Ticks}";

            var contextInfo = new PublishContextInfo
            {
                PublishOptions = publishContext.PublishOptions,
                StartTime = DateTime.UtcNow
            };

            _publishContexts.AddOrUpdate(contextKey, contextInfo, (key, existing) => contextInfo);
        }

        private void LogPublishCompletion()
        {
            Log.Info("═══════════════════════════════════════════════════════════════", this);
            Log.Info("PublishTelemetryService.ProcessPublishEnd: Publishing completed.", this);
            Log.Info("───────────────────────────────────────────────────────────────", this);
        }

        private void LogPublishOptions(PublishOptions publishOptions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Publish Options:");
            sb.AppendLine($"  Mode: {publishOptions.Mode}");
            sb.AppendLine($"  Source Database: {publishOptions.SourceDatabase?.Name ?? "N/A"}");
            sb.AppendLine($"  Target Database: {publishOptions.TargetDatabase?.Name ?? "N/A"}");
            sb.AppendLine($"  Root Item: {publishOptions.RootItem?.Paths.FullPath ?? "N/A"} ({publishOptions.RootItem?.ID})");
            sb.AppendLine($"  Deep: {publishOptions.Deep}");
            sb.AppendLine($"  Compare Revisions: {publishOptions.CompareRevisions}");
            sb.AppendLine($"  Republish All: {publishOptions.RepublishAll}");
            sb.AppendLine($"  From Date: {publishOptions.FromDate:yyyy-MM-dd HH:mm:ss}");

            if (publishOptions.Language != null)
            {
                sb.AppendLine($"  Language: {publishOptions.Language.Name}");
            }
            else
            {
                sb.AppendLine($"  Languages: {string.Join(", ", publishOptions.TargetDatabase.Languages.Select(l => l.Name))}");
            }

            Log.Info(sb.ToString(), this);
        }

        private void LogPublishStatistics()
        {
            var processingList = _processingItems.Values.ToList();
            var updatedList = _updatedItems.ToList();

            var newItems = processingList.Count(p => p.TargetRevisionId == ID.Null);
            var updatedExisting = updatedList.Count(u => u.OldRevisionId != ID.Null);
            var skipped = processingList.Count - updatedList.Count;

            var sb = new StringBuilder();
            sb.AppendLine("Publish Statistics (Tracked):");
            sb.AppendLine($"  Created: {newItems}");
            sb.AppendLine($"  Updated: {updatedExisting}");
            sb.AppendLine($"  Skipped: {skipped}");
            sb.AppendLine($"  Total Processed: {processingList.Count}");
            Log.Info(sb.ToString(), this);
        }

        private void LogUpdatedItems(List<ItemUpdateInfo> updatedItemsList)
        {
            if (!updatedItemsList.Any())
            {
                Log.Info("Updated Items: None (no items were updated during this publish)", this);
                return;
            }

            Log.Info($"Updated Items: {updatedItemsList.Count} item(s) were updated", this);
            Log.Info("───────────────────────────────────────────────────────────────", this);

            foreach (var item in updatedItemsList.OrderBy(i => i.ItemPath))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"  Item: {item.ItemPath}");
                sb.AppendLine($"    ID: {item.ItemId}");
                sb.AppendLine($"    Language: {item.Language}");
                sb.AppendLine($"    Version: {item.Version}");
                sb.AppendLine($"    Action: {item.Action}");
                sb.AppendLine($"    Result: {item.Result}");
                sb.AppendLine($"    Publish Mode: {item.PublishMode}");
                sb.AppendLine($"    Revision: {item.OldRevisionId} -> {item.NewRevisionId}");
                sb.AppendLine($"    Updated: {item.OldUpdated:yyyy-MM-dd HH:mm:ss.fff} -> {item.NewUpdated:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"    Time Difference: {(item.NewUpdated - item.OldUpdated).TotalSeconds:F3} seconds");

                Log.Info(sb.ToString(), this);
            }

            Log.Info("───────────────────────────────────────────────────────────────", this);
        }

        private void LogRevisionSummary()
        {
            var processingList = _processingItems.Values.ToList();
            var updatedList = _updatedItems.ToList();

            Log.Info("Revision Comparison Summary:", this);
            Log.Info($"  Total Items Processed: {processingList.Count}", this);
            Log.Info($"  Items with Revision Changes: {updatedList.Count}", this);
            Log.Info($"  Items Skipped (No Changes): {processingList.Count - updatedList.Count}", this);

            if (processingList.Any())
            {
                var newItemsList = processingList.Where(p => p.TargetRevisionId == ID.Null).ToList();
                var existingItems = processingList.Where(p => p.TargetRevisionId != ID.Null).ToList();

                Log.Info($"  New Items (Created): {newItemsList.Count}", this);
                Log.Info($"  Existing Items: {existingItems.Count}", this);

                if (existingItems.Any())
                {
                    var updatedExistingList = updatedList.Where(u => u.OldRevisionId != ID.Null).ToList();
                    Log.Info($"    Updated: {updatedExistingList.Count}", this);
                    Log.Info($"    Unchanged: {existingItems.Count - updatedExistingList.Count}", this);
                }
            }

            Log.Info("═══════════════════════════════════════════════════════════════", this);
        }

        private void ClearBuffers()
        {
            lock (_statsLock)
            {
                int processingCount = _processingItems.Count;
                int updatedCount = _updatedItems.Count;
                int contextCount = _publishContexts.Count;

                _processingItems.Clear();
                _publishContexts.Clear();
                while (_updatedItems.TryTake(out _)) { }

                Log.Info($"PublishTelemetryService.ClearBuffers: Cleared {processingCount} processing items, {updatedCount} updated items, and {contextCount} publish contexts.", this);
            }
        }

        private void SendUpdatedItemsToTelemetry(List<ItemUpdateInfo> updatedItems, PublishOptions publishOptions)
        {
            if (!updatedItems.Any())
            {
                Log.Info("PublishTelemetryService.SendUpdatedItemsToTelemetry: No items to send.", this);
                return;
            }

            var targetDatabase = publishOptions.TargetDatabase;
            if (targetDatabase == null)
            {
                Log.Warn("PublishTelemetryService.SendUpdatedItemsToTelemetry: Target database is null.", this);
                return;
            }

            var sourceDatabaseName = publishOptions.SourceDatabase?.Name ?? "master";

            Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Sending {updatedItems.Count} updated items to telemetry service...", this);

            Task.Run(() =>
            {
                int successCount = 0;
                int failureCount = 0;

                foreach (var updateInfo in updatedItems)
                {
                    try
                    {
                        var language = Language.Parse(updateInfo.Language);
                        var version = Version.Parse(updateInfo.Version);

                        Item publishedItem = targetDatabase.GetItem(updateInfo.ItemId, language, version) ??
                                           targetDatabase.GetItem(updateInfo.ItemId);

                        if (publishedItem != null)
                        {
                            RecordItemProcessed(publishedItem, publishOptions, sourceDatabaseName);
                            successCount++;

                            Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Sent item {publishedItem.Paths.FullPath} to telemetry.", this);
                        }
                        else
                        {
                            Log.Warn($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Could not retrieve item {updateInfo.ItemId} from target database.", this);
                            failureCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Error sending item {updateInfo.ItemId} to telemetry.", ex, this);
                        failureCount++;
                    }
                }

                Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Completed. Success: {successCount}, Failures: {failureCount}", this);
            });
        }

        private void RecordItemProcessed(Item item, PublishOptions options, string sourceDatabaseName)
        {
            var assetIds = ExtractAssetIds(item);
            var publicLink = ExtractPublicLink(item);

            var payload = new AssetUsageEvent
            {
                PublicLink = publicLink,
                ItemId = item.ID.ToString(),
                ItemPath = item.Paths.FullPath,
                ItemName = item.Name,
                TemplateName = item.TemplateName,
                Language = item.Language.Name,
                Version = item.Version.Number,
                PublishedAtUtc = DateTime.UtcNow,
                PublishedBy = GetPublishedBy(),
                AssetIds = assetIds,
                TargetDatabase = options.TargetDatabase?.Name ?? string.Empty
            };

            Task.Run(() => assetUsageClient.SendAsync(payload));

            var record = new
            {
                PublicLink = publicLink,
                Timestamp = NowString(),
                EventType = "ItemProcessed",
                ItemId = item.ID.ToString(),
                AssetId = assetIds.FirstOrDefault() ?? string.Empty,
                AssetIds = assetIds,
                ItemName = item.Name,
                ItemPath = item.Paths.FullPath,
                TemplateName = item.TemplateName,
                TemplateId = item.TemplateID.ToString(),
                Language = item.Language.Name,
                Version = item.Version.Number,
                SourceDatabase = sourceDatabaseName,
                TargetDatabase = options.TargetDatabase?.Name ?? string.Empty,
                PublishMode = options.Mode.ToString(),
                DeepPublish = options.Deep
            };

            WriteAudit(record);
        }

        private void RecordPublishEndRemote(string eventQueueName, IEnumerable<string> databasesRaised)
        {
            var summary = new
            {
                Timestamp = NowString(),
                EventType = "PublishEndRemote",
                EventQueueName = eventQueueName,
                DatabasesRaised = databasesRaised?.ToList() ?? new List<string>()
            };
            WriteAudit(summary);
        }

        private static string GetPublishedBy()
        {
            try
            {
                var currentUser = global::Sitecore.Security.Accounts.User.Current;
                if (currentUser != null && currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(currentUser.Name))
                    return currentUser.Name;
            }
            catch { }

            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var userName = windowsIdentity?.Name;
            return string.IsNullOrWhiteSpace(userName) ? "system" : userName;
        }

        private static string NowString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void WriteAudit(object record)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(record);

                var directory = Path.GetDirectoryName(auditLogPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (FileLock)
                {
                    File.AppendAllText(auditLogPath, json + Environment.NewLine);
                }

                Log.Info("[WriteAudit] Append complete.", this);
            }
            catch (Exception exception)
            {
                Log.Error("[WriteAudit] Error writing JSON", exception, this);
            }
        }
    }
}