using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;
using Sitecore.Configuration;
using Sitecore.Data;
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
        private const int BatchSize = 500;
        private readonly Database _webDatabase;
        private readonly AssetUsageServiceClient _client;
        private readonly AssetExtractionService _assetExtractionService;
        private readonly PublishLoggingService _loggingService;
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
                    throw new InvalidOperationException("Root item not found in web database.");
                }

                var allItems = new List<Item> { rootItem };
                allItems.AddRange(rootItem.Axes.GetDescendants());

                var filteredItems = FilterItemsWithContentHubLinks(allItems);

                Log.Info($"[InitialItemAssetLinkService] Found {filteredItems.Count} items with Content Hub links (out of {allItems.Count} total)", this);

                if (filteredItems.Count == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                }
                else
                {
                    MigrationProgressTracker.TotalItems = filteredItems.Count;

                    await ProcessItemsInBatchesAsync(filteredItems);

                    Log.Info($"[InitialItemAssetLinkService] Migration completed: {MigrationProgressTracker.SuccessCount} successful, {MigrationProgressTracker.FailureCount} failed", this);
                }
            }
            catch (Exception exception)
            {
                Log.Error("[InitialItemAssetLinkService] Migration failed", exception, this);
                MigrationProgressTracker.ErrorMessage = exception.Message;
                throw;
            }
            finally
            {
                MigrationProgressTracker.IsRunning = false;
                MigrationProgressTracker.EndTime = DateTime.UtcNow;
            }
        }

        private List<Item> FilterItemsWithContentHubLinks(List<Item> items)
        {
            var contentHubEndpoint = Settings.GetSetting(ContentHubEndpoint);

            if (string.IsNullOrWhiteSpace(contentHubEndpoint))
            {
                Log.Warn("[InitialItemAssetLinkService] ContentHub endpoint not configured, processing all items", this);
                return items;
            }

            var filteredItems = new List<Item>();

            foreach (var item in items)
            {
                try
                {
                    var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyField(item);

                    if (HasContentHubLinks(publicLinks, contentHubEndpoint))
                    {
                        filteredItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[InitialItemAssetLinkService] Failed to process item {item.Paths.FullPath}: {ex.Message}", this);
                }
            }

            return filteredItems;
        }

        private bool HasContentHubLinks(List<string> publicLinks, string contentHubEndpoint)
        {
            if (publicLinks == null || publicLinks.Count == 0)
            {
                return false;
            }

            return publicLinks.Any(link =>
                !string.IsNullOrWhiteSpace(link) &&
                link.IndexOf(contentHubEndpoint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task ProcessItemsInBatchesAsync(List<Item> items)
        {
            MigrationProgressTracker.ProcessedItems = 0;
            MigrationProgressTracker.SuccessCount = 0;
            MigrationProgressTracker.FailureCount = 0;

            var totalBatches = (int)Math.Ceiling((double)items.Count / BatchSize);

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batch = items.Skip(batchIndex * BatchSize).Take(BatchSize).ToList();

                foreach (var item in batch)
                {
                    MigrationProgressTracker.CurrentItem = item.Paths.FullPath;

                    try
                    {
                        await SendItemToAssetUsageServiceAsync(item);
                        MigrationProgressTracker.SuccessCount++;
                    }
                    catch (Exception exception)
                    {
                        Log.Error($"[InitialItemAssetLinkService] Failed to process item {item.Paths.FullPath}", exception, this);
                        MigrationProgressTracker.FailureCount++;
                    }

                    MigrationProgressTracker.ProcessedItems++;
                }
            }
        }

        private async Task SendItemToAssetUsageServiceAsync(Item item)
        {
            var assetIds = _assetExtractionService.ExtractAssetIds(item);
            var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyField(item);

            var payload = new AssetUsageEvent
            {
                PublicLinks = publicLinks,
                ItemId = item.ID.ToString(),
                ItemPath = item.Paths.FullPath,
                ItemName = item.Name,
                TemplateName = item.TemplateName,
                Language = item.Language.Name,
                Version = item.Version.Number,
                PublishedAtUtc = DateTime.UtcNow,
                AssetIds = assetIds,
                TargetDatabase = "web"
            };

            await _client.SendAsync(payload);
        }
    }
}