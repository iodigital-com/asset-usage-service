using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using iO.Sitecore.Publishing.Interfaces.Services;
using iO.Sitecore.Publishing.Models;
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
    public class PayloadExtractor : IPayloadExtractor
    {
        private readonly IAssetExtractionService _assetExtractionService;
        private readonly IContentHubLinkValidator _linkValidator;

        public PayloadExtractor(
            IAssetExtractionService assetExtractionService,
            IContentHubLinkValidator linkValidator)
        {
            _assetExtractionService = assetExtractionService ?? throw new ArgumentNullException(nameof(assetExtractionService));
            _linkValidator = linkValidator ?? throw new ArgumentNullException(nameof(linkValidator));
        }

        public List<AssetUsageEvent> ExtractPayloads(Item[] items)
        {
            if (items == null || items.Length == 0)
            {
                return new List<AssetUsageEvent>();
            }

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
                            var payload = ExtractSinglePayload(item);
                            if (payload != null)
                            {
                                payloads.Add(payload);
                            }
                        }
                    }
                    catch(Exception exception)
                    {
                        Log.Warn($"[InitialItemAssetLinkService] Failed to extract payload for item {item?.ID}: {exception.Message}", this);
                    }
                    finally
                    {
                        MigrationProgressTracker.IncrementProcessedItems();
                        Interlocked.Increment(ref processed);
                    }
                });

            return payloads.ToList();
        }

        private AssetUsageEvent ExtractSinglePayload(Item item)
        {
            if (item == null)
            {
                return null;
            }

            item.Fields.ReadAll();

            var fieldData = ExtractFieldData(item);
            var assetIds = _assetExtractionService.ExtractAssetIdsFromFieldData(fieldData);
            var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);

            bool hasAssets = assetIds?.Count > 0;
            bool hasLinks = publicLinks?.Count > 0;

            if (!hasAssets && !hasLinks)
            {
                return null;
            }

            if (!_linkValidator.HasValidLinks(publicLinks))
            {
                return null;
            }

            return CreatePayload(item, assetIds, publicLinks);
        }

        private static List<(string TypeKey, string Value, string InheritedValue, string Name)> ExtractFieldData(Item item)
        {
            return item.Fields
                .Cast<Field>()
                .Where(f => f != null)
                .Select(f => (
                    TypeKey: f.TypeKey ?? string.Empty,
                    Value: f.Value ?? string.Empty,
                    InheritedValue: f.InheritedValue ?? string.Empty,
                    Name: f.Name ?? string.Empty
                ))
                .ToList();
        }

        private static AssetUsageEvent CreatePayload(Item item, List<string> assetIds, List<string> publicLinks)
        {
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

        private static string GetSafeItemPath(Item item)
        {
            if (item == null)
            {
                return null;
            }

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