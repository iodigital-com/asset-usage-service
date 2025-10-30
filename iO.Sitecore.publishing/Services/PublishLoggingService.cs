using iO.Sitecore.publishing.Models;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Publishing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class PublishLoggingService
    {
        private readonly object owner;

        public PublishLoggingService(object owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void LogInvalidEventArguments(string methodName)
        {
            Log.Debug($"PublishTelemetryService.{methodName}: Invalid event arguments or context.", owner);
        }

        public void LogSourceItemNotFound(ID itemId)
        {
            Log.Debug($"PublishTelemetryService.ProcessItemProcessing: Source item not found. ItemID: {itemId}", owner);
        }

        public void LogPublishedItemNotFound(ID itemId)
        {
            Log.Debug($"PublishTelemetryService.ProcessItemProcessed: Published item not found in target. ItemID: {itemId}", owner);
        }

        // Warn logs
        public void LogNoProcessingInfoFound(ID itemId, string languageName, int versionNumber, bool hasVersionInfo)
        {
            Log.Warn($"PublishTelemetryService.ProcessItemProcessed: No processing info found for item {itemId}, " +
                $"Language: {languageName}, Version: {versionNumber}, " +
                $"HasVersionInfo: {hasVersionInfo}", owner);
        }

        public void LogCouldNotRetrievePublishOptions()
        {
            Log.Warn("PublishTelemetryService.ProcessPublishEnd: Could not retrieve publish options.", owner);
        }

        public void LogTargetDatabaseIsNull()
        {
            Log.Warn("PublishTelemetryService.SendUpdatedItemsToTelemetry: Target database is null.", owner);
        }

        public void LogCouldNotRetrieveItemFromTarget(ID itemId)
        {
            Log.Warn($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Could not retrieve item {itemId} from target database.", owner);
        }

        public void LogMalformedLinkXml(string fieldName, string itemPath, Exception exception)
        {
            Log.Warn($"[ExtractAssetIds] Malformed link XML in field '{fieldName}' on '{itemPath}'", exception, typeof(PublishTelemetryService));
        }

        public void LogExtractAssetIdsError(string itemPath, Exception exception)
        {
            Log.Warn($"[ExtractAssetIds] Error for item {itemPath}", exception, typeof(PublishTelemetryService));
        }

        public void LogMalformedPublicLinkXml(string fieldName, Exception exception)
        {
            Log.Warn($"[ExtractPublicLink] Malformed link XML in field '{fieldName}'", exception, typeof(PublishTelemetryService));
        }

        public void LogMalformedFileXml(string fieldName, Exception exception)
        {
            Log.Warn($"[ExtractPublicLink] Malformed file XML in field '{fieldName}'", exception, typeof(PublishTelemetryService));
        }

        // Error logs
        public void LogExtractPublicLinkError(string itemPath, Exception exception)
        {
            Log.Error($"[ExtractPublicLink] Error extracting public link from item {itemPath}", exception, typeof(PublishTelemetryService));
        }

        public void LogErrorSendingItemToTelemetry(ID itemId, Exception exception)
        {
            Log.Error($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Error sending item {itemId} to telemetry.", exception, owner);
        }

        public void LogWriteAuditError(Exception exception)
        {
            Log.Error("[WriteAudit] Error writing JSON", exception, owner);
        }

        // Info logs
        public void LogItemProcessing(ID itemId, string itemPath, string languageName, int versionNumber, bool hasVersionInfo, string itemKey, string action, ID sourceRevisionId, DateTime sourceUpdated, ID targetRevisionId, DateTime targetUpdated, bool targetExists, PublishMode publishMode, bool deep, bool compareRevisions)
        {
            Log.Info($"PublishTelemetryService.ProcessItemProcessing: " +
                $"ItemID: {itemId}, " +
                $"Path: '{itemPath}', " +
                $"Language: {languageName}, " +
                $"Version: {versionNumber}, " +
                $"HasVersionInfo: {hasVersionInfo}, " +
                $"ItemKey: '{itemKey}', " +
                $"Action: {action}, " +
                $"SourceRevision: {sourceRevisionId}, " +
                $"SourceUpdated: {sourceUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TargetRevision: {targetRevisionId}, " +
                $"TargetUpdated: {targetUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TargetExists: {targetExists}, " +
                $"PublishMode: {publishMode}, " +
                $"Deep: {deep}, " +
                $"CompareRevisions: {compareRevisions}",
                owner);
        }

        public void LogItemProcessed(ID itemId, string itemPath, string language, int version, bool hasVersionInfo, string action, string result, ID oldRevisionId, ID newRevisionId, bool revisionChanged, DateTime oldUpdated, DateTime newUpdated, bool timestampChanged, double processingDurationMs)
        {
            Log.Info($"PublishTelemetryService.ProcessItemProcessed: " +
                $"ItemID: {itemId}, " +
                $"Path: '{itemPath}', " +
                $"Language: {language}, " +
                $"Version: {version}, " +
                $"HasVersionInfo: {hasVersionInfo}, " +
                $"Action: {action}, " +
                $"Result: {result}, " +
                $"OldRevision: {oldRevisionId}, " +
                $"NewRevision: {newRevisionId}, " +
                $"RevisionChanged: {revisionChanged}, " +
                $"OldUpdated: {oldUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"NewUpdated: {newUpdated:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"TimestampChanged: {timestampChanged}, " +
                $"ProcessingDuration: {processingDurationMs}ms",
                owner);
        }

        public void LogItemMarkedAsUpdated(ID itemId, string itemPath, ID oldRevisionId, ID newRevisionId)
        {
            Log.Info($"PublishTelemetryService.ProcessItemProcessed: Item marked as UPDATED. " +
                $"ItemID: {itemId}, " +
                $"Path: '{itemPath}', " +
                $"RevisionChange: {oldRevisionId} -> {newRevisionId}",
                owner);
        }

        public void LogPublishEndRemote(Guid rootItemId, string mode, bool deep, string languageInfo, string sourceDb, string targetDb)
        {
            Log.Info($"PublishTelemetryService.ProcessPublishEndRemote: Remote publish end notification received. " +
                $"RootItemID: {rootItemId}, " +
                $"Mode: {mode}, " +
                $"Deep: {deep}, " +
                $"Language: {languageInfo}, " +
                $"SourceDB: {sourceDb}, " +
                $"TargetDB: {targetDb}",
                owner);
        }

        public void LogPublishEndRemoteSent()
        {
            Log.Info("PublishTelemetryService.ProcessPublishEndRemote: Sent remote publish event to telemetry service.", owner);
        }

        public void LogAssetIdAdded(string assetId)
        {
            Log.Info($"[AddIfNotEmpty] Added id '{assetId}'", typeof(PublishTelemetryService));
        }

        public void LogAssetIdExtractedFromUrl(string assetId, string url)
        {
            Log.Info($"[ExtractIdsFromUrl] Extracted id '{assetId}' from URL '{url}'", typeof(PublishTelemetryService));
        }

        public void LogPublishCompletion()
        {
            Log.Info("═══════════════════════════════════════════════════════════════", owner);
            Log.Info("PublishTelemetryService.ProcessPublishEnd: Publishing completed.", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogPublishOptions(PublishOptions publishOptions)
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

            Log.Info(sb.ToString(), owner);
        }

        public void LogPublishStatistics(int created, int updated, int skipped, int totalProcessed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Publish Statistics (Tracked):");
            sb.AppendLine($"  Created: {created}");
            sb.AppendLine($"  Updated: {updated}");
            sb.AppendLine($"  Skipped: {skipped}");
            sb.AppendLine($"  Total Processed: {totalProcessed}");
            Log.Info(sb.ToString(), owner);
        }

        public void LogUpdatedItems(List<ItemUpdateInfo> updatedItemsList)
        {
            if (!updatedItemsList.Any())
            {
                Log.Info("Updated Items: None (no items were updated during this publish)", owner);
                return;
            }

            Log.Info($"Updated Items: {updatedItemsList.Count} item(s) were updated", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);

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

                Log.Info(sb.ToString(), owner);
            }

            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogRevisionSummary(int totalProcessed, int revisionChanges, int skipped, int newItems, int existingItems, int updatedExisting, int unchanged)
        {
            Log.Info("Revision Comparison Summary:", owner);
            Log.Info($"  Total Items Processed: {totalProcessed}", owner);
            Log.Info($"  Items with Revision Changes: {revisionChanges}", owner);
            Log.Info($"  Items Skipped (No Changes): {skipped}", owner);

            if (totalProcessed > 0)
            {
                Log.Info($"  New Items (Created): {newItems}", owner);
                Log.Info($"  Existing Items: {existingItems}", owner);

                if (existingItems > 0)
                {
                    Log.Info($"    Updated: {updatedExisting}", owner);
                    Log.Info($"    Unchanged: {unchanged}", owner);
                }
            }

            Log.Info("═══════════════════════════════════════════════════════════════", owner);
        }

        public void LogClearBuffers(int processingCount, int updatedCount, int contextCount)
        {
            Log.Info($"PublishTelemetryService.ClearBuffers: Cleared {processingCount} processing items, {updatedCount} updated items, and {contextCount} publish contexts.", owner);
        }

        public void LogNoItemsToSend()
        {
            Log.Info("PublishTelemetryService.SendUpdatedItemsToTelemetry: No items to send.", owner);
        }

        public void LogSendingUpdatedItems(int count)
        {
            Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Sending {count} updated items to telemetry service...", owner);
        }

        public void LogItemSentToTelemetry(string itemPath)
        {
            Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Sent item {itemPath} to telemetry.", owner);
        }

        public void LogSendingCompleted(int successCount, int failureCount)
        {
            Log.Info($"PublishTelemetryService.SendUpdatedItemsToTelemetry: Completed. Success: {successCount}, Failures: {failureCount}", owner);
        }

        public void LogAuditWriteComplete()
        {
            Log.Info("[WriteAudit] Append complete.", owner);
        }
    }
}