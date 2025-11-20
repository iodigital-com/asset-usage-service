using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class AssetExtractionService : IAssetExtractionService
    {
        private const string ImageFieldType = "image";
        private const string RichTextFieldType = "rich text";
        private const string ThumbnailSourceAttribute = "thumbnailsrc";
        private const string GatewayUrlPattern = @"/api/gateway/(\d+)/";

        private static readonly Regex GatewayIdRegex = new Regex(GatewayUrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ImageSourceRegex = new Regex(@"<img[^>]+src=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly PublishLoggingService _loggingService;

        public AssetExtractionService(PublishLoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public List<string> ExtractAssetIds(Item item)
        {
            return ExtractFromFields(item, ProcessAssetIdField, exception => _loggingService.LogExtractAssetIdsError(item.Paths.FullPath, exception));
        }

        public List<string> ExtractPublicLinksFromAnyField(Item item)
        {
            return ExtractFromFields(item, ExtractPublicLinkFromAnyField, exception => _loggingService.LogExtractPublicLinkError(item.Paths.FullPath, exception));
        }

        public List<string> ExtractPublicLinks(Item item)
        {
            return ExtractFromFields(item, ProcessPublicLinkFieldForRichText, exception => _loggingService.LogExtractPublicLinkError(item.Paths.FullPath, exception));
        }

        private List<string> ExtractFromFields(Item item, Action<Field, HashSet<string>> fieldProcessor, Action<Exception> errorLogger)
        {
            var extractedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value))
                        continue;

                    fieldProcessor(field, extractedValues);
                }
            }
            catch (Exception exception)
            {
                errorLogger(exception);
            }

            return extractedValues.ToList();
        }

        private void ProcessAssetIdField(Field field, HashSet<string> extractedAssetIds)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == ImageFieldType)
            {
                var imageField = (ImageField)field;
                var thumbnailSourceUrl = imageField.GetAttribute(ThumbnailSourceAttribute);

                ExtractGatewayIdsFromUrl(extractedAssetIds, thumbnailSourceUrl);
            }
        }

        private void ProcessPublicLinkFieldForRichText(Field field, HashSet<string> extractedPublicLinks)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == RichTextFieldType)
            {
                ExtractPublicLinkFromAnyField(field, extractedPublicLinks);
            }
        }

        private void ExtractPublicLinkFromAnyField(Field field, HashSet<string> extractedPublicLinks)
        {
            if (field == null || string.IsNullOrWhiteSpace(field.Value))
                return;

            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == ImageFieldType)
            {
                var imageField = (ImageField)field;
                var thumbnailSourceUrl = imageField.GetAttribute(ThumbnailSourceAttribute);

                AddValueIfNotEmpty(extractedPublicLinks, thumbnailSourceUrl);
                _loggingService.LogAssetIdExtractedFromUrl(thumbnailSourceUrl, $"Image field: {field.Name}");
                return;
            }

            if (fieldTypeKey == RichTextFieldType)
            {
                var richTextContent = field.InheritedValue ?? field.Value;

                if (!string.IsNullOrWhiteSpace(richTextContent))
                {
                    ExtractImageUrlsFromHtmlContent(extractedPublicLinks, richTextContent);
                }
                return;
            }

            var fieldValue = field.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(fieldValue) && IsValidUrl(fieldValue))
            {
                AddValueIfNotEmpty(extractedPublicLinks, fieldValue);
                _loggingService.LogAssetIdExtractedFromUrl(fieldValue, $"Field: {field.Name}");
            }
        }

        private void ExtractImageUrlsFromHtmlContent(HashSet<string> extractedUrls, string htmlContent)
        {
            var imageSourceMatches = ImageSourceRegex.Matches(htmlContent);

            foreach (Match imageMatch in imageSourceMatches)
            {
                if (imageMatch.Success && imageMatch.Groups.Count > 1)
                {
                    var imageUrl = imageMatch.Groups[1].Value;
                    AddValueIfNotEmpty(extractedUrls, imageUrl);
                }
            }
        }

        private void AddValueIfNotEmpty(HashSet<string> targetCollection, string value)
        {
            var trimmedValue = value?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                targetCollection.Add(trimmedValue);
                _loggingService.LogAssetIdAdded(trimmedValue);
            }
        }

        private void ExtractGatewayIdsFromUrl(HashSet<string> extractedIds, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var gatewayIdMatch = GatewayIdRegex.Match(url);

            if (gatewayIdMatch.Success && gatewayIdMatch.Groups.Count > 1)
            {
                var gatewayId = gatewayIdMatch.Groups[1].Value;

                AddValueIfNotEmpty(extractedIds, gatewayId);
                _loggingService.LogAssetIdExtractedFromUrl(gatewayId, url);
            }
        }

        private bool IsValidUrl(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}