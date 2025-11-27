using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Interfaces.Services;
using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string ContentHubEndpoint = "AssetUsageService.ContentHubEndpoint";

        public InitialItemAssetLinkService()
        {
            _webDatabase = Factory.GetDatabase("web");

            if (_webDatabase == null)
            {
                throw new InvalidOperationException("Web database not found. Check Sitecore configuration.");
            }

            _client = new AssetUsageServiceClient();
            _loggingService = new PublishLoggingService(this);
            _assetExtractionService = new AssetExtractionService(_loggingService);
            _contentHubEndpoint = Settings.GetSetting(ContentHubEndpoint);
        }

        public async Task ExecuteMigrationAsync()
        {
            Log.Info("[InitialItemAssetLinkService] Migration started", this);

            MigrationProgressTracker.Reset();
            MigrationProgressTracker.IsRunning = true;
            MigrationProgressTracker.StartTime = DateTime.UtcNow;

            try
            {
                var rootItem = _webDatabase.GetRootItem();

                if (rootItem == null)
                {
                    MigrationProgressTracker.ErrorMessage = "Root item not found in web database.";
                    Log.Error("[InitialItemAssetLinkService] Root item not found in web database.", this);
                    return;
                }

                var totalItemCount = CountItemsInTree(rootItem);
                MigrationProgressTracker.TotalItems = totalItemCount;

                Log.Info($"[InitialItemAssetLinkService] Found {totalItemCount} total items to scan", this);

                if (totalItemCount == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items found in web database.";
                    Log.Warn("[InitialItemAssetLinkService] No items found in web database.", this);
                    return;
                }

                await ProcessItemTreeRecursivelyAsync(rootItem);

                Log.Info($"[InitialItemAssetLinkService] Migration completed: {MigrationProgressTracker.SuccessCount} successful, {MigrationProgressTracker.FailureCount} failed", this);

                if (MigrationProgressTracker.SuccessCount == 0 && MigrationProgressTracker.FailureCount == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                }
            }
            catch (Exception exception)
            {
                Log.Error("[InitialItemAssetLinkService] Migration failed", exception, this);
                MigrationProgressTracker.ErrorMessage = exception.Message;
            }
            finally
            {
                MigrationProgressTracker.IsRunning = false;
                MigrationProgressTracker.EndTime = DateTime.UtcNow;
            }
        }

        private int CountItemsInTree(Item rootItem)
        {
            if (rootItem == null)
            {
                return 0;
            }

            int count = 0;
            var queue = new Queue<Item>();
            queue.Enqueue(rootItem);

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                if (currentItem == null)
                {
                    continue;
                }

                count++;

                var children = currentItem.GetChildren();
                if (children != null && children.Count > 0)
                {
                    foreach (Item child in children)
                    {
                        if (child != null)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            return count;
        }

        private async Task ProcessItemTreeRecursivelyAsync(Item item)
        {
            if (item == null)
            {
                return;
            }

            var queue = new Queue<Item>();
            queue.Enqueue(item);

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                if (currentItem == null)
                {
                    continue;
                }

                await ProcessSingleItemAsync(currentItem);

                var children = currentItem.GetChildren();
                if (children != null && children.Count > 0)
                {
                    foreach (Item child in children)
                    {
                        if (child != null)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }
        }

        private async Task ProcessSingleItemAsync(Item item)
        {
            if (item == null)
            {
                return;
            }

            if (item.Paths == null || string.IsNullOrWhiteSpace(item.Paths.FullPath))
            {
                Log.Warn("[InitialItemAssetLinkService] Item has invalid path, skipping", this);
                MigrationProgressTracker.ProcessedItems++;
                return;
            }

            MigrationProgressTracker.CurrentItem = item.Paths.FullPath;

            try
            {
                item.Fields.ReadAll();

                var fieldData = item.Fields
                    .Cast<Field>()
                    .Select(f => (
                        TypeKey: f.TypeKey ?? string.Empty,
                        Value: f.Value ?? string.Empty,
                        InheritedValue: f.InheritedValue ?? string.Empty,
                        Name: f.Name ?? string.Empty
                    ))
                    .ToList();

                var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);

                if ((publicLinks == null || publicLinks.Count == 0) && !string.IsNullOrWhiteSpace(_contentHubEndpoint))
                {
                    MigrationProgressTracker.ProcessedItems++;
                    return;
                }

                if (string.IsNullOrWhiteSpace(_contentHubEndpoint) || HasContentHubLinks(publicLinks, _contentHubEndpoint))
                {
                    try
                    {
                        await SendItemToAssetUsageServiceAsync(item);
                        MigrationProgressTracker.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[InitialItemAssetLinkService] Failed to process item {item.Paths.FullPath}", ex, this);
                        MigrationProgressTracker.FailureCount++;
                    }
                }

                MigrationProgressTracker.ProcessedItems++;
            }
            catch (Exception ex)
            {
                Log.Warn($"[InitialItemAssetLinkService] Failed to extract links from item {item.Paths.FullPath}: {ex.Message}", this);
                MigrationProgressTracker.ProcessedItems++;
            }
        }

        private bool HasContentHubLinks(List<string> publicLinks, string contentHubEndpoint)
        {
            if (publicLinks == null || publicLinks.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(contentHubEndpoint))
            {
                return false;
            }

            return publicLinks.Any(link =>
                !string.IsNullOrWhiteSpace(link) &&
                link.IndexOf(contentHubEndpoint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task SendItemToAssetUsageServiceAsync(Item item)
        {
            if (item == null)
            {
                return;
            }

            item.Fields.ReadAll();

            var fieldData = item.Fields
                .Cast<Field>()
                .Select(f => (
                    TypeKey: f.TypeKey ?? string.Empty,
                    Value: f.Value ?? string.Empty,
                    InheritedValue: f.InheritedValue ?? string.Empty,
                    Name: f.Name ?? string.Empty
                ))
                .ToList();

            var assetIds = _assetExtractionService.ExtractAssetIdsFromFieldData(fieldData);
            var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);

            if ((assetIds == null || assetIds.Count == 0) && (publicLinks == null || publicLinks.Count == 0))
            {
                Log.Info($"[InitialItemAssetLinkService] No asset IDs or public links found for item {item.Paths.FullPath}, skipping send", this);
                return;
            }

            var payload = new AssetUsageEvent
            {
                PublicLinks = publicLinks ?? new List<string>(),
                ItemId = item.ID.ToString(),
                ItemPath = item.Paths.FullPath,
                ItemName = item.Name,
                TemplateName = item.TemplateName,
                Language = item.Language?.Name ?? string.Empty,
                Version = item.Version?.Number ?? 0,
                PublishedAtUtc = DateTime.UtcNow,
                AssetIds = assetIds ?? new List<string>(),
                TargetDatabase = "web"
            };

            await _client.SendAsync(payload);
        }
    }
}