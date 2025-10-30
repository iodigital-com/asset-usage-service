using iO.Sitecore.publishing.Models;
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
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
        private readonly PublishLoggingService loggingService;

        private static readonly ConcurrentDictionary<string, ItemProcessingInfo> _processingItems = new ConcurrentDictionary<string, ItemProcessingInfo>();
        private static readonly ConcurrentBag<ItemUpdateInfo> _updatedItems = new ConcurrentBag<ItemUpdateInfo>();
        private static readonly ConcurrentDictionary<string, PublishContextInfo> _publishContexts = new ConcurrentDictionary<string, PublishContextInfo>();
        private static readonly object _statsLock = new object();
        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly int UpdatedTimestampThresholdSeconds = 1;

        public PublishTelemetryService(AssetUsageServiceClient client, string auditLogPath)
        {
            assetUsageClient = client ?? throw new ArgumentNullException(nameof(client));
            this.auditLogPath = string.IsNullOrWhiteSpace(auditLogPath) ? throw new ArgumentException(nameof(auditLogPath)) : auditLogPath;
            loggingService = new PublishLoggingService(this);
        }

        public void ProcessItemProcessing(ItemProcessingEventArgs eventArgs)
        {
            if (eventArgs?.Context?.PublishOptions == null)
            {
                loggingService.LogInvalidEventArguments("ProcessItemProcessing");
                return;
            }

            var context = eventArgs.Context;
            var publishContext = context.PublishContext;

            StorePublishContext(publishContext);

            var (language, version, hasVersionInfo) = GetLanguageAndVersion(context, publishContext);
            var sourceItem = GetSourceItem(context, publishContext, language, version);

            if (sourceItem == null)
            {
                loggingService.LogSourceItemNotFound(context.ItemId);
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

            loggingService.LogItemProcessing(
                context.ItemId,
                sourceItem.Paths.FullPath,
                language.Name,
                version.Number,
                hasVersionInfo,
                itemKey,
                context.Action.ToString(),
                sourceRevisionId,
                sourceUpdated,
                targetRevisionId,
                targetUpdated,
                targetItem != null,
                publishContext.PublishOptions.Mode,
                publishContext.PublishOptions.Deep,
                publishContext.PublishOptions.CompareRevisions);
        }

        public void ProcessItemProcessed(ItemProcessedEventArgs eventArgs)
        {
            if (eventArgs?.Context?.PublishOptions == null)
            {
                loggingService.LogInvalidEventArguments("ProcessItemProcessed");
                return;
            }

            var context = eventArgs.Context;
            var publishContext = context.PublishContext;

            var (language, version, hasVersionInfo) = GetLanguageAndVersion(context, publishContext);
            var processingInfo = FindProcessingInfo(context.ItemId, language, version);

            if (processingInfo == null)
            {
                loggingService.LogNoProcessingInfoFound(context.ItemId, language.Name, version.Number, hasVersionInfo);
                return;
            }

            var publishedItem = GetTargetItem(context, publishContext, language, version);
            if (publishedItem == null)
            {
                loggingService.LogPublishedItemNotFound(context.ItemId);
                return;
            }

            var (newRevisionId, newUpdated) = GetItemRevisionInfo(publishedItem);
            var result = DeterminePublishResult(processingInfo, newRevisionId, newUpdated, context.Action);

            loggingService.LogItemProcessed(
                context.ItemId,
                processingInfo.ItemPath,
                processingInfo.Language,
                processingInfo.Version,
                processingInfo.HasVersionInfo,
                processingInfo.Action,
                result,
                processingInfo.TargetRevisionId,
                newRevisionId,
                processingInfo.TargetRevisionId != newRevisionId,
                processingInfo.TargetUpdated,
                newUpdated,
                processingInfo.TargetUpdated != newUpdated,
                (DateTime.UtcNow - processingInfo.ProcessingTime).TotalMilliseconds);

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

                loggingService.LogItemMarkedAsUpdated(
                    context.ItemId,
                    processingInfo.ItemPath,
                    processingInfo.TargetRevisionId,
                    newRevisionId);
            }
        }

        public void ProcessPublishEnd(EventArgs args)
        {
            var publishOptions = ExtractPublishOptions(args);
            if (publishOptions == null)
            {
                loggingService.LogCouldNotRetrievePublishOptions();
                return;
            }

            loggingService.LogPublishCompletion();
            loggingService.LogPublishOptions(publishOptions);
            LogPublishStatistics();

            var updatedItemsList = _updatedItems.ToList();
            loggingService.LogUpdatedItems(updatedItemsList);

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
                loggingService.LogInvalidEventArguments("ProcessPublishEndRemote");
                return;
            }

            var languageInfo = string.IsNullOrEmpty(eventArgs.LanguageName) ? "All" : eventArgs.LanguageName;
            var sourceDb = string.IsNullOrEmpty(eventArgs.SourceDatabaseName) ? "N/A" : eventArgs.SourceDatabaseName;
            var targetDb = string.IsNullOrEmpty(eventArgs.TargetDatabaseName) ? "N/A" : eventArgs.TargetDatabaseName;

            loggingService.LogPublishEndRemote(
                eventArgs.RootItemId,
                eventArgs.Mode.ToString(),
                eventArgs.Deep,
                languageInfo,
                sourceDb,
                targetDb);

            var databases = Factory.GetDatabases()
                .Where(database => database.RemoteEvents.EventQueue.Name == eventArgs.EventQueueName)
                .Select(database => database.Name)
                .ToList();

            RecordPublishEndRemote(eventArgs.EventQueueName, databases);
            loggingService.LogPublishEndRemoteSent();
        }

        private static List<string> ExtractAssetIds(Item item, PublishLoggingService logger)
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
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("DamId"), logger);
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("dam-id"), logger);
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("stylelabs-content-id"), logger);
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Thumbnail"), logger);
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("thumbnailsrc"), logger);
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Source"), logger);
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("src"), logger);
                            break;

                        case "general link":
                        case "link":
                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("DamId"), logger);
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("dam-id"), logger);
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("stylelabs-content-id"), logger);
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("url"), logger);
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("href"), logger);
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("Source"), logger);
                            }
                            catch (Exception exception)
                            {
                                logger.LogMalformedLinkXml(field.Name, item.Paths.FullPath, exception);
                            }
                            break;

                        default:
                            foreach (Match urlMatch in GatewayIdRegex.Matches(field.Value))
                            {
                                if (urlMatch.Success && urlMatch.Groups.Count > 1)
                                {
                                    AddIfNotEmpty(assetIdsSet, urlMatch.Groups[1].Value, logger);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogExtractAssetIdsError(item.Paths.FullPath, exception);
            }

            return assetIdsSet.ToList();
        }

        private static string ExtractPublicLink(Item item, PublishLoggingService logger)
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
                                logger.LogMalformedPublicLinkXml(field.Name, exception);
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
                                logger.LogMalformedFileXml(field.Name, exception);
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogExtractPublicLinkError(item.Paths.FullPath, exception);
            }

            return string.Empty;
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value, PublishLoggingService logger)
        {
            var trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                sink.Add(trimmedValue);
                logger.LogAssetIdAdded(trimmedValue);
            }
        }

        private static void ExtractIdsFromUrl(HashSet<string> sink, string url, PublishLoggingService logger)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var urlMatch = GatewayIdRegex.Match(url);
            if (urlMatch.Success && urlMatch.Groups.Count > 1)
            {
                var gatewayIdValue = urlMatch.Groups[1].Value;
                sink.Add(gatewayIdValue);
                logger.LogAssetIdExtractedFromUrl(gatewayIdValue, url);
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
            return Math.Abs((processingInfo.TargetUpdated - newUpdated).TotalSeconds) > UpdatedTimestampThresholdSeconds;
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

        private void LogPublishStatistics()
        {
            var processingList = _processingItems.Values.ToList();
            var updatedList = _updatedItems.ToList();

            var newItems = processingList.Count(p => p.TargetRevisionId == ID.Null);
            var updatedExisting = updatedList.Count(u => u.OldRevisionId != ID.Null);
            var skipped = processingList.Count - updatedList.Count;

            loggingService.LogPublishStatistics(newItems, updatedExisting, skipped, processingList.Count);
        }

        private void LogRevisionSummary()
        {
            var processingList = _processingItems.Values.ToList();
            var updatedList = _updatedItems.ToList();

            var newItemsList = processingList.Where(p => p.TargetRevisionId == ID.Null).ToList();
            var existingItems = processingList.Where(p => p.TargetRevisionId != ID.Null).ToList();
            var updatedExistingList = updatedList.Where(u => u.OldRevisionId != ID.Null).ToList();

            loggingService.LogRevisionSummary(
                processingList.Count,
                updatedList.Count,
                processingList.Count - updatedList.Count,
                newItemsList.Count,
                existingItems.Count,
                updatedExistingList.Count,
                existingItems.Count - updatedExistingList.Count);
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

                loggingService.LogClearBuffers(processingCount, updatedCount, contextCount);
            }
        }

        private void SendUpdatedItemsToTelemetry(List<ItemUpdateInfo> updatedItems, PublishOptions publishOptions)
        {
            if (!updatedItems.Any())
            {
                loggingService.LogNoItemsToSend();
                return;
            }

            var targetDatabase = publishOptions.TargetDatabase;
            if (targetDatabase == null)
            {
                loggingService.LogTargetDatabaseIsNull();
                return;
            }

            var sourceDatabaseName = publishOptions.SourceDatabase?.Name ?? "master";

            loggingService.LogSendingUpdatedItems(updatedItems.Count);

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

                            loggingService.LogItemSentToTelemetry(publishedItem.Paths.FullPath);
                        }
                        else
                        {
                            loggingService.LogCouldNotRetrieveItemFromTarget(updateInfo.ItemId);
                            failureCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        loggingService.LogErrorSendingItemToTelemetry(updateInfo.ItemId, ex);
                        failureCount++;
                    }
                }

                loggingService.LogSendingCompleted(successCount, failureCount);
            });
        }

        private void RecordItemProcessed(Item item, PublishOptions options, string sourceDatabaseName)
        {
            var assetIds = ExtractAssetIds(item, loggingService);
            var publicLink = ExtractPublicLink(item, loggingService);

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

                loggingService.LogAuditWriteComplete();
            }
            catch (Exception exception)
            {
                loggingService.LogWriteAuditError(exception);
            }
        }
    }
}