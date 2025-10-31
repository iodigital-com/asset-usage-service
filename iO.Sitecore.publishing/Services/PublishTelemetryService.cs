using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Version = Sitecore.Data.Version;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class PublishTelemetryService : IPublishTelemetryService
    {
        private readonly AssetUsageServiceClient assetUsageClient;
        private readonly PublishLoggingService loggingService;
        private readonly IAssetExtractionService assetExtractionService;
        private readonly IAuditLoggingService auditLoggingService;

        private static readonly ConcurrentDictionary<string, ItemProcessingInfo> _processingItems = new ConcurrentDictionary<string, ItemProcessingInfo>();
        private static readonly ConcurrentBag<ItemUpdateInfo> _updatedItems = new ConcurrentBag<ItemUpdateInfo>();
        private static readonly ConcurrentDictionary<string, PublishContextInfo> _publishContexts = new ConcurrentDictionary<string, PublishContextInfo>();
        private static readonly object _statsLock = new object();
        private const int UPDATEDTIMESTAMPTHRESHOLDSECONDS = 1;

        public PublishTelemetryService(AssetUsageServiceClient client, string auditLogPath)
        {
            assetUsageClient = client ?? throw new ArgumentNullException(nameof(client));
            loggingService = new PublishLoggingService(this);
            assetExtractionService = new AssetExtractionService(loggingService);
            auditLoggingService = new AuditLoggingService(auditLogPath, loggingService);
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

        public async Task ProcessPublishEndAsync(EventArgs args)
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
                await SendUpdatedItemsToTelemetryAsync(updatedItemsList, publishOptions);
            }

            LogRevisionSummary();
            ClearBuffers();
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
            return Math.Abs((processingInfo.TargetUpdated - newUpdated).TotalSeconds) > UPDATEDTIMESTAMPTHRESHOLDSECONDS;
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

        private async Task SendUpdatedItemsToTelemetryAsync(List<ItemUpdateInfo> updatedItems, PublishOptions publishOptions)
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
                        await RecordItemProcessedAsync(publishedItem, publishOptions, sourceDatabaseName);
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
        }

        private async Task RecordItemProcessedAsync(Item item, PublishOptions options, string sourceDatabaseName)
        {
            var assetIds = assetExtractionService.ExtractAssetIds(item);
            var publicLink = assetExtractionService.ExtractPublicLink(item);

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

            await assetUsageClient.SendAsync(payload);

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

            auditLoggingService.WriteAudit(record);
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
            auditLoggingService.WriteAudit(summary);
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
    }
}