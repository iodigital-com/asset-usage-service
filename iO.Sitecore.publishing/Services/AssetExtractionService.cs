using iO.Sitecore.Publishing.Interfaces.Services;
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

        public List<string> ExtractAssetIdsFromFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields)
        {
            return ExtractFromFieldData(fields, ProcessAssetIdFieldFromData, exception => _loggingService.LogExtractAssetIdsError("FieldData", exception));
        }

        public List<string> ExtractPublicLinksFromAnyFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields)
        {
            return ExtractFromFieldData(fields, ExtractPublicLinkFromAnyFieldFromData, exception => _loggingService.LogExtractPublicLinkError("FieldData", exception));
        }

        public List<string> ExtractPublicLinksFromFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields)
        {
            return ExtractFromFieldData(fields, ProcessPublicLinkFieldForRichTextFromData, exception => _loggingService.LogExtractPublicLinkError("FieldData", exception));
        }

        private List<string> ExtractFromFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields,
            Action<(string TypeKey, string Value, string InheritedValue, string Name), HashSet<string>> fieldProcessor,
            Action<Exception> errorLogger)
        {
            var extractedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (fields == null) return extractedValues.ToList();

                foreach (var field in fields)
                {
                    if (string.IsNullOrEmpty(field.Value))
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

        private void ProcessAssetIdFieldFromData((string TypeKey, string Value, string InheritedValue, string Name) field, HashSet<string> extractedAssetIds)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == ImageFieldType)
            {
                var thumbnailSourceUrl = GetAttributeValueFromMarkup(field.Value, ThumbnailSourceAttribute);

                ExtractGatewayIdsFromUrl(extractedAssetIds, thumbnailSourceUrl);
            }
        }

        // Tuple-based rich text processing.
        private void ProcessPublicLinkFieldForRichTextFromData((string TypeKey, string Value, string InheritedValue, string Name) field, HashSet<string> extractedPublicLinks)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == RichTextFieldType)
            {
                var richTextContent = field.InheritedValue ?? field.Value;

                if (!string.IsNullOrWhiteSpace(richTextContent))
                {
                    ExtractImageUrlsFromHtmlContent(extractedPublicLinks, richTextContent);
                }
            }
        }

        private void ExtractPublicLinkFromAnyFieldFromData((string TypeKey, string Value, string InheritedValue, string Name) field, HashSet<string> extractedPublicLinks)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
                return;

            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == ImageFieldType)
            {
                var thumbnailSourceUrl = GetAttributeValueFromMarkup(field.Value, ThumbnailSourceAttribute);
                AddValueIfNotEmpty(extractedPublicLinks, thumbnailSourceUrl);
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
            var imageSourceMatches = ImageSourceRegex.Matches(htmlContent ?? string.Empty);

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
            return !string.IsNullOrEmpty(value) &&
                   (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetAttributeValueFromMarkup(string markup, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(markup))
                return null;

            var m = Regex.Match(markup, attributeName + "\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : null;
        }
    }
}