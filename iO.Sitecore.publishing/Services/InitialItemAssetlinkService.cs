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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public class InitialItemAssetLinkService
    {
        private readonly Database _database;
        private readonly AssetUsageServiceClient _client;
        private readonly IAssetExtractionService _assetExtractionService;
        private readonly PublishLoggingService _loggingService;
        private readonly string _contentHubEndpoint;
        private readonly string _databaseName;
        private readonly string _rootItemId;
        private const string ContentHubEndpointSetting = "AssetUsageService.ContentHubEndpoint";
        private const string DatabaseNameSetting = "AssetUsageService.DatabaseName";
        private const string RootItemIdSetting = "AssetUsageService.RootItemId";
        private const string LogPrefix = "[InitialItemAssetLinkService]";

        static InitialItemAssetLinkService()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 200;
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.UseNagleAlgorithm = false;
        }

        public InitialItemAssetLinkService()
        {
            using (new SecurityDisabler())
            {
                _databaseName = Settings.GetSetting(DatabaseNameSetting, "master");
                _database = Factory.GetDatabase(_databaseName);
            }

            if (_database == null)
            {
                throw new InvalidOperationException("Database not found.");
            }

            _client = new AssetUsageServiceClient();
            _loggingService = new PublishLoggingService(this);
            _assetExtractionService = new AssetExtractionService(_loggingService);
            _contentHubEndpoint = Settings.GetSetting(ContentHubEndpointSetting);
            _rootItemId = Settings.GetSetting(RootItemIdSetting);
        }

        public async Task ExecuteMigrationAsync()
        {
            Log.Info($"{LogPrefix} Migration started", this);

            MigrationProgressTracker.Reset();
            MigrationProgressTracker.IsRunning = true;
            MigrationProgressTracker.StartTime = DateTime.UtcNow;

            try
            {
                var rootItemId = _rootItemId;

                MigrationProgressTracker.CurrentPhase = 1;
                MigrationProgressTracker.PhaseDescription = "Counting items";

                var allItems = CollectAllItems(rootItemId);
                if (allItems == null || allItems.Length == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items found.";
                    return;
                }
                MigrationProgressTracker.TotalItems = allItems.Length;
                MigrationProgressTracker.ProcessedItems = allItems.Length;

                MigrationProgressTracker.CurrentPhase = 2;
                MigrationProgressTracker.PhaseDescription = "Extracting assets";
                MigrationProgressTracker.ProcessedItems = 0;

                var payloads = ExtractAllPayloadsParallel(allItems);
                MigrationProgressTracker.ExtractedCount = payloads.Count;

                allItems = null;

                if (payloads.Count == 0)
                {
                    MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                    return;
                }

                MigrationProgressTracker.CurrentPhase = 3;
                MigrationProgressTracker.PhaseDescription = "Sending to service";
                MigrationProgressTracker.ProcessedItems = 0;
                MigrationProgressTracker.TotalItems = payloads.Count;

                await SendAllPayloadsAsync(payloads);

                Log.Info($"{LogPrefix} Migration completed: {MigrationProgressTracker.SuccessCount} success, {MigrationProgressTracker.FailureCount} failed", this);
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

        private Item[] CollectAllItems(string rootItemId)
        {
            using (new SecurityDisabler())
            {
                var rootItem = _database.GetItem(new ID(rootItemId));
                if (rootItem == null) return Array.Empty<Item>();

                var descendants = rootItem.Axes.GetDescendants();
                var result = new Item[descendants.Length + 1];
                result[0] = rootItem;
                descendants.CopyTo(result, 1);

                return result;
            }
        }

        private List<AssetUsageEvent> ExtractAllPayloadsParallel(Item[] items)
        {
            var payloads = new ConcurrentBag<AssetUsageEvent>();
            int processed = 0;

            Parallel.ForEach(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                item =>
                {
                    try
                    {
                        using (new SecurityDisabler())
                        {
                            var payload = ExtractPayload(item);
                            if (payload != null)
                            {
                                payloads.Add(payload);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        MigrationProgressTracker.ProcessedItems = Interlocked.Increment(ref processed);
                    }
                });

            return payloads.ToList();
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
            int maxConcurrent = GetConcurrency();
            int completed = 0;
            int successCount = 0;
            int failureCount = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(
                    payloads,
                    new ParallelOptions { MaxDegreeOfParallelism = maxConcurrent },
                    payload =>
                    {
                        try
                        {
                            _client.SendAsync(payload).GetAwaiter().GetResult();
                            Interlocked.Increment(ref successCount);
                        }
                        catch
                        {
                            Interlocked.Increment(ref failureCount);
                        }
                        finally
                        {
                            MigrationProgressTracker.ProcessedItems = Interlocked.Increment(ref completed);
                            MigrationProgressTracker.SuccessCount = successCount;
                            MigrationProgressTracker.FailureCount = failureCount;
                        }
                    });
            });
        }

        private int GetConcurrency()
        {
            var setting = Settings.GetSetting("AssetUsageService.MaxConcurrency", "100");
            if (int.TryParse(setting, out int value) && value > 0 && value <= 200)
            {
                return value;
            }
            return 100;
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