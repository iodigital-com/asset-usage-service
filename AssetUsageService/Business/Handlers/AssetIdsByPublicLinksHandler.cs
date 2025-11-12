using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers.interfaces;
using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Base.Querying;
using Stylelabs.M.Base.Querying.Filters;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Framework.Essentials.LoadOptions;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Business.Handlers;

public sealed class AssetIdsByPublicLinksHandler : IEventHandler<AssetIdsByPublicLinksEvent>
{
    private const string RelativeUrlProperty = "RelativeUrl";
    private const string AssetToPublicLinkRelation = "AssetToPublicLink";

    private readonly IContentHubConnectionService _contentHubConnection;
    private readonly ILogger<AssetIdsByPublicLinksHandler> _logger;

    public AssetIdsByPublicLinksHandler(IContentHubConnectionService contentHubConnection, ILogger<AssetIdsByPublicLinksHandler> logger)
    {
        _contentHubConnection = contentHubConnection;
        _logger = logger;
    }

    public async Task HandleAsync(AssetIdsByPublicLinksEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(@event.PublicLinks);

        try
        {
            var assetIds = await GetAssetIdsByPublicLinksAsync(@event.PublicLinks, cancellationToken);

            _logger.LogInformation("Successfully retrieved {Count} asset IDs from {LinkCount} public links",
                assetIds.Count, @event.PublicLinks.Count);
            @event.AssetIds = assetIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve asset IDs from public links");
            throw;
        }
    }

    private async Task<List<int>> GetAssetIdsByPublicLinksAsync(IReadOnlyList<string> publicLinks, CancellationToken cancellationToken)
    {
        var assetIds = new List<int>(publicLinks.Count);
        var contentHubClient = _contentHubConnection.CreateClient();

        foreach (var url in publicLinks)
        {
            var assetId = await TryGetAssetIdFromPublicLinkAsync(contentHubClient, url, cancellationToken);
            if (assetId.HasValue)
            {
                assetIds.Add((int)assetId.Value);
            }
        }

        return assetIds;
    }

    private async Task<long?> TryGetAssetIdFromPublicLinkAsync(IWebMClient contentHubClient, string url, CancellationToken cancellationToken)
    {
        try
        {
            var relativeUrl = ExtractRelativeUrl(url);
            if (relativeUrl is null)
            {
                return null;
            }

            var publicLinkId = await GetPublicLinkIdByRelativeUrlAsync(contentHubClient, relativeUrl, cancellationToken);

            if (!publicLinkId.HasValue)
            {
                return null;
            }

            var assetId = await GetAssetIdFromPublicLinkAsync(contentHubClient, publicLinkId.Value, cancellationToken);

            return assetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing public link: {Url}", url);
            return null;
        }
    }

    private string? ExtractRelativeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Invalid URL format: {Url}", url);
            return null;
        }

        if (uri.Segments.Length == 0)
        {
            _logger.LogWarning("URL has no path segments: {Url}", url);
            return null;
        }

        var segment = uri.Segments[^1];
        if (string.IsNullOrEmpty(segment))
        {
            _logger.LogWarning("URL segment is empty: {Url}", url);
            return null;
        }

        return segment.Split('?')[0];
    }

    private async Task<long?> GetPublicLinkIdByRelativeUrlAsync(IWebMClient contentHubClient, string relativeUrl, CancellationToken cancellationToken)
    {
        try
        {
            var query = new Query
            {
                Filter = new PropertyQueryFilter
                {
                    Property = RelativeUrlProperty,
                    Value = relativeUrl,
                    DataType = FilterDataType.String
                }
            };

            var result = await contentHubClient.Querying.QueryAsync(query);

            if (!result.Items.Any())
            {
                _logger.LogWarning("No public link found for relative URL: {RelativeUrl}", relativeUrl);
                return null;
            }

            return result.Items.First().Id.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public link ID for relative URL: {RelativeUrl}", relativeUrl);
            throw;
        }
    }

    private async Task<long?> GetAssetIdFromPublicLinkAsync(IWebMClient contentHubClient, long publicLinkId, CancellationToken cancellationToken)
    {
        var publicLink = await contentHubClient.Entities.GetAsync(
            publicLinkId,
            new EntityLoadConfiguration(
                CultureLoadOption.Default,
                PropertyLoadOption.All,
                new RelationLoadOption(AssetToPublicLinkRelation))
            );

        var assetToPublicLinkRelation = publicLink.GetRelation<IChildToManyParentsRelation>(AssetToPublicLinkRelation);
        var assetId = assetToPublicLinkRelation?.GetIds().FirstOrDefault();

        if (!assetId.HasValue)
        {
            _logger.LogWarning("No asset found for public link ID: {PublicLinkId}", publicLinkId);
        }

        return assetId;
    }
}

