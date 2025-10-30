using Sitecore;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Version = Sitecore.Data.Version;

namespace iO.Sitecore.publishing.Events
{
    public class PublishEventHandler
    {
        private static readonly ConcurrentDictionary<string, ItemProcessingInfo> _processingItems =
            new ConcurrentDictionary<string, ItemProcessingInfo>();

        private static readonly ConcurrentBag<ItemUpdateInfo> _updatedItems =
            new ConcurrentBag<ItemUpdateInfo>();

        private static readonly ConcurrentDictionary<string, PublishContextInfo> _publishContexts =
            new ConcurrentDictionary<string, PublishContextInfo>();

        private static readonly object _statsLock = new object();

        protected void OnItemProcessing(object sender, EventArgs args)
        {
            try
            {
                var eventArgs = args as ItemProcessingEventArgs;
                if (eventArgs?.Context?.PublishOptions == null)
                {
                    Log.Debug("PublishEventHandler.OnItemProcessing: Invalid event arguments or context.", this);
                    return;
                }

                var context = eventArgs.Context;
                var publishContext = context.PublishContext;

                StorePublishContext(publishContext);

                bool hasVersionInfo = context.VersionToPublish != null;
                Language language = hasVersionInfo ? context.VersionToPublish.Language : (publishContext.PublishOptions.Language ?? Language.Parse("en"));
                Version version = hasVersionInfo ? context.VersionToPublish.Version : Version.Latest;

                Item sourceItem = context.ItemId != ID.Null
                    ? publishContext.PublishOptions.SourceDatabase.GetItem(context.ItemId, language, version)
                    : null;

                if (sourceItem == null)
                {
                    sourceItem = publishContext.PublishOptions.SourceDatabase.GetItem(context.ItemId);

                    if (sourceItem == null)
                    {
                        Log.Debug($"PublishEventHandler.OnItemProcessing: Source item not found. ItemID: {context.ItemId}", this);
                        return;
                    }
                }

                string itemKey = hasVersionInfo
                    ? $"{context.ItemId}_{language.Name}_{version.Number}"
                    : context.ItemId.ToString();

                string sourceRevisionStr = sourceItem.Statistics.Revision;
                ID sourceRevisionId = ID.Null;
                if (!string.IsNullOrEmpty(sourceRevisionStr))
                {
                    sourceRevisionId = ID.Parse(sourceRevisionStr);
                }
                DateTime sourceUpdated = sourceItem.Statistics.Updated;

                Item targetItem = publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId, language, version);

                if (targetItem == null)
                {
                    targetItem = publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId);
                }

                ID targetRevisionId = ID.Null;
                DateTime targetUpdated = DateTime.MinValue;

                if (targetItem != null)
                {
                    string targetRevisionStr = targetItem.Statistics.Revision;
                    if (!string.IsNullOrEmpty(targetRevisionStr))
                    {
                        targetRevisionId = ID.Parse(targetRevisionStr);
                    }
                    targetUpdated = targetItem.Statistics.Updated;
                }

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

                Log.Info($"PublishEventHandler.OnItemProcessing: " +
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
            catch (Exception ex)
            {
                Log.Error($"PublishEventHandler.OnItemProcessing: Error processing item. Exception: {ex.Message}, StackTrace: {ex.StackTrace}", ex, this);
            }
        }

        protected void OnItemProcessed(object sender, EventArgs args)
        {
            try
            {
                var eventArgs = args as ItemProcessedEventArgs;
                if (eventArgs?.Context?.PublishOptions == null)
                {
                    Log.Debug("PublishEventHandler.OnItemProcessed: Invalid event arguments or context.", this);
                    return;
                }

                var context = eventArgs.Context;
                var publishContext = context.PublishContext;

                bool hasVersionInfo = context.VersionToPublish != null;
                Language language = hasVersionInfo ? context.VersionToPublish.Language : (publishContext.PublishOptions.Language ?? Language.Parse("en"));
                Version version = hasVersionInfo ? context.VersionToPublish.Version : Version.Latest;

                string itemKeyWithVersion = $"{context.ItemId}_{language.Name}_{version.Number}";
                string itemKeyWithoutVersion = context.ItemId.ToString();

                ItemProcessingInfo processingInfo;
                bool found = _processingItems.TryGetValue(itemKeyWithVersion, out processingInfo);
                string usedKey = itemKeyWithVersion;

                if (!found)
                {
                    found = _processingItems.TryGetValue(itemKeyWithoutVersion, out processingInfo);
                    usedKey = itemKeyWithoutVersion;
                }

                if (!found)
                {
                    Log.Warn($"PublishEventHandler.OnItemProcessed: No processing info found for item {context.ItemId}, " +
                        $"Language: {language.Name}, Version: {version.Number}, " +
                        $"HasVersionInfo: {hasVersionInfo}, " +
                        $"Tried keys: '{itemKeyWithVersion}' and '{itemKeyWithoutVersion}'", this);
                    return;
                }

                Item publishedItem = publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId, language, version);

                if (publishedItem == null)
                {
                    publishedItem = publishContext.PublishOptions.TargetDatabase.GetItem(context.ItemId);

                    if (publishedItem == null)
                    {
                        Log.Debug($"PublishEventHandler.OnItemProcessed: Published item not found in target. ItemID: {context.ItemId}", this);
                        return;
                    }
                }

                string newRevisionStr = publishedItem.Statistics.Revision;
                ID newRevisionId = ID.Null;
                if (!string.IsNullOrEmpty(newRevisionStr))
                {
                    newRevisionId = ID.Parse(newRevisionStr);
                }
                DateTime newUpdated = publishedItem.Statistics.Updated;

                string result = "Processed";
                if (processingInfo.TargetRevisionId == ID.Null)
                {
                    result = "Created";
                }
                else if (processingInfo.TargetRevisionId != newRevisionId)
                {
                    result = "Updated";
                }
                else if (processingInfo.TargetUpdated != newUpdated)
                {
                    result = "Modified";
                }
                else if (context.Action == PublishAction.None)
                {
                    result = "Skipped";
                }

                Log.Info($"PublishEventHandler.OnItemProcessed: " +
                    $"ItemID: {context.ItemId}, " +
                    $"Path: '{processingInfo.ItemPath}', " +
                    $"Language: {processingInfo.Language}, " +
                    $"Version: {processingInfo.Version}, " +
                    $"HasVersionInfo: {processingInfo.HasVersionInfo}, " +
                    $"UsedKey: '{usedKey}', " +
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

                bool isUpdated = false;
                if (publishContext.PublishOptions.Mode == PublishMode.Smart || publishContext.PublishOptions.Mode == PublishMode.Incremental)
                {
                    if (processingInfo.TargetRevisionId != newRevisionId && newRevisionId != ID.Null)
                    {
                        isUpdated = true;
                    }
                    else if (Math.Abs((processingInfo.TargetUpdated - newUpdated).TotalSeconds) > 1)
                    {
                        isUpdated = true;
                    }
                }
                else
                {
                    isUpdated = newRevisionId != ID.Null;
                }

                if (isUpdated)
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
                        PublishMode = publishContext.PublishOptions.Mode.ToString()
                    };

                    _updatedItems.Add(updateInfo);

                    Log.Info($"PublishEventHandler.OnItemProcessed: Item marked as UPDATED. " +
                        $"ItemID: {context.ItemId}, " +
                        $"Path: '{processingInfo.ItemPath}', " +
                        $"RevisionChange: {processingInfo.TargetRevisionId} -> {newRevisionId}",
                        this);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"PublishEventHandler.OnItemProcessed: Error processing item. Exception: {ex.Message}, StackTrace: {ex.StackTrace}", ex, this);
            }
        }

        protected void OnPublishEnd(object sender, EventArgs args)
        {
            try
            {
                var publisher = Event.ExtractParameter(args, 0) as Publisher;

                PublishOptions publishOptions = null;

                if (publisher != null)
                {
                    publishOptions = publisher.Options;
                }
                else
                {
                    var contextInfo = _publishContexts.Values.FirstOrDefault();
                    if (contextInfo != null)
                    {
                        publishOptions = contextInfo.PublishOptions;
                    }
                }

                if (publishOptions == null)
                {
                    Log.Warn("PublishEventHandler.OnPublishEnd: Could not retrieve publish options.", this);
                    return;
                }

                Log.Info("═══════════════════════════════════════════════════════════════", this);
                Log.Info("PublishEventHandler.OnPublishEnd: Publishing completed.", this);
                Log.Info("───────────────────────────────────────────────────────────────", this);

                if (publishOptions != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Publish Options:");
                    sb.AppendLine($"  Mode: {publishOptions.Mode}");

                    string sourceDbName = (publishOptions.SourceDatabase != null) ? publishOptions.SourceDatabase.Name : "N/A";
                    string targetDbName = (publishOptions.TargetDatabase != null) ? publishOptions.TargetDatabase.Name : "N/A";

                    sb.AppendLine($"  Source Database: {sourceDbName}");
                    sb.AppendLine($"  Target Database: {targetDbName}");

                    if (publishOptions.RootItem != null)
                    {
                        sb.AppendLine($"  Root Item: {publishOptions.RootItem.Paths.FullPath} ({publishOptions.RootItem.ID})");
                    }
                    else
                    {
                        sb.AppendLine($"  Root Item: N/A");
                    }

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

                var processingList = _processingItems.Values.ToList();
                var updatedList = _updatedItems.ToList();

                var newItems = processingList.Where(p => p.TargetRevisionId == ID.Null).Count();
                var updatedExisting = updatedList.Where(u => u.OldRevisionId != ID.Null).Count();
                var skipped = processingList.Count - updatedList.Count;

                var sbStats = new StringBuilder();
                sbStats.AppendLine("Publish Statistics (Tracked):");
                sbStats.AppendLine($"  Created: {newItems}");
                sbStats.AppendLine($"  Updated: {updatedExisting}");
                sbStats.AppendLine($"  Skipped: {skipped}");
                sbStats.AppendLine($"  Total Processed: {processingList.Count}");
                Log.Info(sbStats.ToString(), this);

                if (_updatedItems.IsEmpty)
                {
                    Log.Info("Updated Items: None (no items were updated during this publish)", this);
                }
                else
                {
                    var updatedItemsList = _updatedItems.ToList();

                    Log.Info($"Updated Items: {updatedItemsList.Count} item(s) were updated", this);
                    Log.Info("───────────────────────────────────────────────────────────────", this);

                    foreach (var item in updatedItemsList.OrderBy(i => i.ItemPath))
                    {
                        var sbItem = new StringBuilder();
                        sbItem.AppendLine($"  Item: {item.ItemPath}");
                        sbItem.AppendLine($"    ID: {item.ItemId}");
                        sbItem.AppendLine($"    Language: {item.Language}");
                        sbItem.AppendLine($"    Version: {item.Version}");
                        sbItem.AppendLine($"    Action: {item.Action}");
                        sbItem.AppendLine($"    Result: {item.Result}");
                        sbItem.AppendLine($"    Publish Mode: {item.PublishMode}");
                        sbItem.AppendLine($"    Revision: {item.OldRevisionId} -> {item.NewRevisionId}");
                        sbItem.AppendLine($"    Updated: {item.OldUpdated:yyyy-MM-dd HH:mm:ss.fff} -> {item.NewUpdated:yyyy-MM-dd HH:mm:ss.fff}");
                        sbItem.AppendLine($"    Time Difference: {(item.NewUpdated - item.OldUpdated).TotalSeconds:F3} seconds");

                        Log.Info(sbItem.ToString(), this);
                    }

                    Log.Info("───────────────────────────────────────────────────────────────", this);
                }

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

                lock (_statsLock)
                {
                    int processingCount = _processingItems.Count;
                    int updatedCount = _updatedItems.Count;
                    int contextCount = _publishContexts.Count;

                    _processingItems.Clear();
                    _publishContexts.Clear();

                    while (_updatedItems.TryTake(out _)) { }

                    Log.Info($"PublishEventHandler.ClearPublishBuffers: Cleared {processingCount} processing items, {updatedCount} updated items, and {contextCount} publish contexts.", this);
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
                if (eventArgs == null)
                {
                    Log.Debug("PublishEventHandler.OnPublishEndRemote: Invalid event arguments.", this);
                    return;
                }

                string languageInfo = string.IsNullOrEmpty(eventArgs.LanguageName) ? "All" : eventArgs.LanguageName;
                string sourceDb = string.IsNullOrEmpty(eventArgs.SourceDatabaseName) ? "N/A" : eventArgs.SourceDatabaseName;
                string targetDb = string.IsNullOrEmpty(eventArgs.TargetDatabaseName) ? "N/A" : eventArgs.TargetDatabaseName;

                Log.Info($"PublishEventHandler.OnPublishEndRemote: Remote publish end notification received. " +
                    $"RootItemID: {eventArgs.RootItemId}, " +
                    $"Mode: {eventArgs.Mode}, " +
                    $"Deep: {eventArgs.Deep}, " +
                    $"Language: {languageInfo}, " +
                    $"SourceDB: {sourceDb}, " +
                    $"TargetDB: {targetDb}",
                    this);
            }
            catch (Exception ex)
            {
                Log.Error("PublishEventHandler.OnPublishEndRemote: Error in publish end remote handler.", ex, this);
            }
        }

        private void StorePublishContext(PublishContext publishContext)
        {
            if (publishContext?.PublishOptions == null) return;

            string sourceDbName = publishContext.PublishOptions.SourceDatabase != null ? publishContext.PublishOptions.SourceDatabase.Name : "Unknown";
            string targetDbName = publishContext.PublishOptions.TargetDatabase != null ? publishContext.PublishOptions.TargetDatabase.Name : "Unknown";
            string contextKey = $"{sourceDbName}_{targetDbName}_{DateTime.UtcNow.Ticks}";

            var contextInfo = new PublishContextInfo
            {
                PublishOptions = publishContext.PublishOptions,
                StartTime = DateTime.UtcNow
            };

            _publishContexts.AddOrUpdate(contextKey, contextInfo, (key, existing) => contextInfo);
        }

        private class ItemProcessingInfo
        {
            public ID ItemId { get; set; }
            public string ItemPath { get; set; }
            public string Language { get; set; }
            public int Version { get; set; }
            public ID SourceRevisionId { get; set; }
            public DateTime SourceUpdated { get; set; }
            public ID TargetRevisionId { get; set; }
            public DateTime TargetUpdated { get; set; }
            public string Action { get; set; }
            public DateTime ProcessingTime { get; set; }
            public bool HasVersionInfo { get; set; }
        }

        private class ItemUpdateInfo
        {
            public ID ItemId { get; set; }
            public string ItemPath { get; set; }
            public string Language { get; set; }
            public int Version { get; set; }
            public ID OldRevisionId { get; set; }
            public ID NewRevisionId { get; set; }
            public DateTime OldUpdated { get; set; }
            public DateTime NewUpdated { get; set; }
            public string Action { get; set; }
            public string Result { get; set; }
            public string PublishMode { get; set; }
        }

        private class PublishContextInfo
        {
            public PublishOptions PublishOptions { get; set; }
            public DateTime StartTime { get; set; }
        }
    }
}