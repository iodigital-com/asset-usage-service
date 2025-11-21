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

                // Process items recursively instead of loading all at once
                var totalItemsProcessed = await ProcessItemTreeRecursivelyAsync(rootItem);

                Log.Info($"[InitialItemAssetLinkService] Migration completed: {MigrationProgressTracker.SuccessCount} successful, {MigrationProgressTracker.FailureCount} failed (processed {totalItemsProcessed} total items)", this);
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

        private async Task<int> ProcessItemTreeRecursivelyAsync(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            var processedCount = 1;

            // Process current item if it has Content Hub links
            if (ShouldProcessItem(item))
            {
                MigrationProgressTracker.CurrentItem = item.Paths.FullPath;
                MigrationProgressTracker.TotalItems++;

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

            // Process children recursively
            if (item.HasChildren)
            {
                foreach (Item child in item.Children)
                {
                    processedCount += await ProcessItemTreeRecursivelyAsync(child);
                }
            }

            return processedCount;
        }

        private bool ShouldProcessItem(Item item)
        {
            var contentHubEndpoint = Settings.GetSetting(ContentHubEndpoint);

            if (string.IsNullOrWhiteSpace(contentHubEndpoint))
            {
                // If no endpoint configured, process all items
                return true;
            }

            try
            {
                var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyField(item);
                return HasContentHubLinks(publicLinks, contentHubEndpoint);
            }
            catch (Exception ex)
            {
                Log.Warn($"[InitialItemAssetLinkService] Failed to check item {item.Paths.FullPath}: {ex.Message}", this);
                return false;
            }
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