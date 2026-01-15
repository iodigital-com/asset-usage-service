using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Interfaces.Services;
using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public class InitialItemAssetLinkService
    {
        private readonly Database _webDatabase;
        private readonly AssetUsageServiceClient _client;
        private readonly IAssetExtractionService _assetExtractionService;
        private readonly PublishLoggingService _loggingService;
        private readonly string _contentHubEndpoint;
        private const string ContentHubEndpointSetting = "AssetUsageService.ContentHubEndpoint";
        private const string LogPrefix = "[InitialItemAssetLinkService]";

        public InitialItemAssetLinkService()
        {
            using (new SecurityDisabler())
            {
                _webDatabase = Factory.GetDatabase("TestMaster");
            }

            if (_webDatabase == null)
            {
                throw new InvalidOperationException("TestMaster database not found.");
            }

            _client = new AssetUsageServiceClient();
            _loggingService = new PublishLoggingService(this);
            _assetExtractionService = new AssetExtractionService(_loggingService);
            _contentHubEndpoint = Settings.GetSetting(ContentHubEndpointSetting);
        }

        public async Task ExecuteMigrationAsync()
        {
            Log.Info($"{LogPrefix} Migration started", this);

            MigrationProgressTracker.Reset();
            MigrationProgressTracker.IsRunning = true;
            MigrationProgressTracker.StartTime = DateTime.UtcNow;

            try
            {
                var rootItemId = "{0DE95AE4-41AB-4D01-9EB0-67441B7C2450}";

                // FASE 1: Count
                MigrationProgressTracker.CurrentPhase = 1;
                MigrationProgressTracker.PhaseDescription = "Counting items";
                Log.Info($"{LogPrefix} Phase 1: Counting items...", this);

                var allItems = CollectAllItems(rootItemId);
                if (allItems == null || allItems.Count == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items found.";
                    return;
                }
                MigrationProgressTracker.TotalItems = allItems.Count;
                Log.Info($"{LogPrefix} Phase 1 complete: {allItems.Count} items found", this);

                // FASE 2: Extract
                MigrationProgressTracker.CurrentPhase = 2;
                MigrationProgressTracker.PhaseDescription = "Extracting assets";
                MigrationProgressTracker.ProcessedItems = 0;
                Log.Info($"{LogPrefix} Phase 2: Extracting assets...", this);

                var payloads = ExtractAllPayloads(allItems);
                MigrationProgressTracker.ExtractedCount = payloads.Count;
                Log.Info($"{LogPrefix} Phase 2 complete: {payloads.Count} items with assets", this);

                allItems.Clear();
                allItems = null;

                if (payloads.Count == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                    return;
                }

                // FASE 3: Send
                MigrationProgressTracker.CurrentPhase = 3;
                MigrationProgressTracker.PhaseDescription = "Sending to service";
                MigrationProgressTracker.ProcessedItems = 0;
                MigrationProgressTracker.TotalItems = payloads.Count;
                Log.Info($"{LogPrefix} Phase 3: Sending to service...", this);

                await SendAllPayloadsAsync(payloads);
                Log.Info($"{LogPrefix} Phase 3 complete: {MigrationProgressTracker.SuccessCount} success, {MigrationProgressTracker.FailureCount} failed", this);
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} Migration failed: {ex.Message}", ex, this);
                MigrationProgressTracker.ErrorMessage = ex.Message;
            }
            finally
            {
                MigrationProgressTracker.IsRunning = false;
                MigrationProgressTracker.EndTime = DateTime.UtcNow;
            }
        }

        private List<Item> CollectAllItems(string rootItemId)
        {
            var items = new List<Item>();

            using (new SecurityDisabler())
            {
                var rootItem = _webDatabase.GetItem(new ID(rootItemId));
                if (rootItem == null)
                {
                    Log.Error($"{LogPrefix} Root item {rootItemId} not found", this);
                    return items;
                }

                var queue = new Queue<Item>();
                queue.Enqueue(rootItem);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current == null) continue;

                    items.Add(current);
                    MigrationProgressTracker.ProcessedItems = items.Count;
                    MigrationProgressTracker.CurrentItem = GetSafeItemPath(current);

                    try
                    {
                        foreach (Item child in current.GetChildren())
                        {
                            if (child != null) queue.Enqueue(child);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"{LogPrefix} Error getting children for {current.ID}: {ex.Message}", this);
                    }
                }
            }

            return items;
        }

        private List<AssetUsageEvent> ExtractAllPayloads(List<Item> items)
        {
            var payloads = new List<AssetUsageEvent>();
            int processed = 0;

            using (new SecurityDisabler())
            {
                foreach (var item in items)
                {
                    processed++;
                    if (processed % 1000 == 0)
                    {
                        Log.Info($"{LogPrefix} Extraction progress: {processed}/{items.Count}", this);
                    }

                    MigrationProgressTracker.ProcessedItems = processed;
                    MigrationProgressTracker.CurrentItem = GetSafeItemPath(item);

                    try
                    {
                        var payload = ExtractPayload(item);
                        if (payload != null)
                        {
                            payloads.Add(payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"{LogPrefix} Extract failed for {item.ID}: {ex.Message}", this);
                    }
                }
            }

            return payloads;
        }

        private AssetUsageEvent ExtractPayload(Item item)
        {
            item.Fields.ReadAll();

            var fieldData = item.Fields
                .Cast<Field>()
                .Where(f => f != null)
                .Select(f => (
                    TypeKey: f.TypeKey ?? string.Empty,
                    Value: f.Value ?? string.Empty,
                    InheritedValue: f.InheritedValue ?? string.Empty,
                    Name: f.Name ?? string.Empty
                ))
                .ToList();

            var assetIds = _assetExtractionService.ExtractAssetIdsFromFieldData(fieldData);
            var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);

            bool hasAssets = assetIds != null && assetIds.Count > 0;
            bool hasLinks = publicLinks != null && publicLinks.Count > 0;

            if (!hasAssets && !hasLinks) return null;

            if (!string.IsNullOrWhiteSpace(_contentHubEndpoint) && !HasContentHubLinks(publicLinks))
            {
                return null;
            }

            return new AssetUsageEvent
            {
                PublicLinks = publicLinks ?? new List<string>(),
                ItemId = item.ID.ToString(),
                ItemPath = GetSafeItemPath(item),
                ItemName = item.Name ?? string.Empty,
                TemplateName = item.TemplateName ?? string.Empty,
                Language = item.Language?.Name ?? string.Empty,
                Version = item.Version?.Number ?? 0,
                PublishedAtUtc = DateTime.UtcNow,
                AssetIds = assetIds ?? new List<string>(),
                TargetDatabase = "web"
            };
        }

        private async Task SendAllPayloadsAsync(List<AssetUsageEvent> payloads)
        {
            const int maxConcurrent = 20;
            var semaphore = new SemaphoreSlim(maxConcurrent);
            var tasks = new List<Task>();
            int completed = 0;

            foreach (var payload in payloads)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await _client.SendAsync(payload);
                        System.Threading.Interlocked.Increment(ref completed);
                        MigrationProgressTracker.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{LogPrefix} Send failed for {payload.ItemId}: {ex.Message}", this);
                        System.Threading.Interlocked.Increment(ref completed);
                        MigrationProgressTracker.FailureCount++;
                    }
                    finally
                    {
                        MigrationProgressTracker.ProcessedItems = completed;
                        MigrationProgressTracker.CurrentItem = payload.ItemPath;
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private bool HasContentHubLinks(List<string> publicLinks)
        {
            if (publicLinks == null || publicLinks.Count == 0 || string.IsNullOrWhiteSpace(_contentHubEndpoint))
            {
                return false;
            }

            return publicLinks.Any(link =>
                !string.IsNullOrWhiteSpace(link) &&
                link.IndexOf(_contentHubEndpoint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string GetSafeItemPath(Item item)
        {
            if (item == null) return "NULL";
            try
            {
                return item.Paths?.FullPath ?? $"[ID:{item.ID}]";
            }
            catch
            {
                return $"[ID:{item.ID}]";
            }
        }
    }
}