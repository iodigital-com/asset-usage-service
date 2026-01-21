using iO.Sitecore.Publishing.Models;
using Sitecore.Data;
using Sitecore.Data.Items;
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

        public void LogRecordItemStart()
        {
            Log.Info("═══════════════════════════════════════════════════════════════", owner);
            Log.Info("RecordItemProcessedAsync: Starting to process item for telemetry", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogItemDetails(Item item, string sourceDatabaseName, string targetDatabaseName)
        {
            Log.Info("Item Details:", owner);
            Log.Info($"  ID: {item.ID}", owner);
            Log.Info($"  Name: {item.Name}", owner);
            Log.Info($"  Path: {item.Paths.FullPath}", owner);
            Log.Info($"  Template: {item.TemplateName} ({item.TemplateID})", owner);
            Log.Info($"  Language: {item.Language.Name}", owner);
            Log.Info($"  Version: {item.Version.Number}", owner);
            Log.Info($"  Database: {item.Database.Name}", owner);
            Log.Info($"  Source Database: {sourceDatabaseName}", owner);
            Log.Info($"  Target Database: {targetDatabaseName}", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogAssetIdsExtracted(List<string> assetIds)
        {
            Log.Info("Extracting Asset IDs...", owner);

            if (assetIds != null && assetIds.Any())
            {
                Log.Info($"  Found {assetIds.Count} Asset ID(s):", owner);
                foreach (var assetId in assetIds)
                {
                    Log.Info($"    - {assetId}", owner);
                }
            }
            else
            {
                Log.Info("  No Asset IDs found", owner);
            }

            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogPublicLinksExtracted(List<string> publicLinks)
        {
            Log.Info("Extracting Public Links...", owner);

            if (publicLinks != null && publicLinks.Any())
            {
                Log.Info($"  Found {publicLinks.Count} Public Link(s):", owner);
                foreach (var link in publicLinks)
                {
                    Log.Info($"    - {link}", owner);
                }
            }
            else
            {
                Log.Info("  No Public Links found", owner);
            }

            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogPayloadCreated(AssetUsageEvent payload)
        {
            Log.Info("Creating AssetUsageEvent payload...", owner);
            Log.Info("Payload created successfully:", owner);
            Log.Info($"  ItemId: {payload.ItemId}", owner);
            Log.Info($"  ItemName: {payload.ItemName}", owner);
            Log.Info($"  ItemPath: {payload.ItemPath}", owner);
            Log.Info($"  Template: {payload.TemplateName}", owner);
            Log.Info($"  Language: {payload.Language}", owner);
            Log.Info($"  Version: {payload.Version}", owner);
            Log.Info($"  PublishedAtUtc: {payload.PublishedAtUtc:yyyy-MM-dd HH:mm:ss.fff}", owner);
            Log.Info($"  TargetDatabase: {payload.TargetDatabase}", owner);
            Log.Info($"  AssetIds Count: {payload.AssetIds?.Count ?? 0}", owner);
            Log.Info($"  PublicLinks Count: {payload.PublicLinks?.Count ?? 0}", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogSendingPayload()
        {
            Log.Info("Sending payload to AssetUsageService...", owner);
        }

        public void LogPayloadSentSuccess()
        {
            Log.Info("[SUCCESS] Payload sent successfully to AssetUsageService", owner);
            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogPayloadSendFailure(ID itemId, Exception ex)
        {
            Log.Error($"[FAILED] Failed to send payload to AssetUsageService for item {itemId}", ex, owner);
        }

        public void LogAuditRecordCreated(dynamic record)
        {
            Log.Info("Creating audit record...", owner);
            Log.Info("Audit record created:", owner);
            Log.Info($"  EventType: {record.EventType}", owner);
            Log.Info($"  Timestamp: {record.Timestamp}", owner);
            Log.Info($"  ItemId: {record.ItemId}", owner);
            Log.Info($"  ItemName: {record.ItemName}", owner);
            Log.Info($"  ItemPath: {record.ItemPath}", owner);
            Log.Info($"  TemplateId: {record.TemplateId}", owner);
            Log.Info($"  TemplateName: {record.TemplateName}", owner);
            Log.Info($"  Language: {record.Language}", owner);
            Log.Info($"  Version: {record.Version}", owner);
            Log.Info($"  SourceDatabase: {record.SourceDatabase}", owner);
            Log.Info($"  TargetDatabase: {record.TargetDatabase}", owner);
            Log.Info($"  PublishMode: {record.PublishMode}", owner);
            Log.Info($"  DeepPublish: {record.DeepPublish}", owner);
            Log.Info($"  Primary AssetId: {record.AssetId}", owner);

            if (record.AssetIds != null && record.AssetIds.Count > 0)
            {
                Log.Info($"  All AssetIds ({record.AssetIds.Count}):", owner);
                foreach (var assetId in record.AssetIds)
                {
                    Log.Info($"    - {assetId}", owner);
                }
            }

            if (record.PublicLinks != null && record.PublicLinks.Count > 0)
            {
                Log.Info($"  PublicLinks ({record.PublicLinks.Count}):", owner);
                foreach (var link in record.PublicLinks)
                {
                    Log.Info($"    - {link}", owner);
                }
            }

            Log.Info("───────────────────────────────────────────────────────────────", owner);
        }

        public void LogWritingAuditRecord()
        {
            Log.Info("Writing audit record to file...", owner);
        }

        public void LogAuditRecordWriteSuccess()
        {
            Log.Info("[SUCCESS] Audit record written successfully", owner);
        }

        public void LogAuditRecordWriteFailure(ID itemId, Exception ex)
        {
            Log.Error($"[FAILED] Failed to write audit record for item {itemId}", ex, owner);
        }

        public void LogRecordItemComplete(string itemName, ID itemId)
        {
            Log.Info("───────────────────────────────────────────────────────────────", owner);
            Log.Info($"RecordItemProcessedAsync: Completed successfully for item {itemName} ({itemId})", owner);
            Log.Info("═══════════════════════════════════════════════════════════════", owner);
        }
    }
}