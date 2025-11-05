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
        private static readonly Regex ImgSrcRegex = new Regex(@"<img[^>]+src=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly PublishLoggingService _loggingService;

        public AssetExtractionService(PublishLoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public List<string> ExtractAssetIds(Item item)
        {
            return ExtractFromFields(item, ProcessAssetIdField, exception => _loggingService.LogExtractAssetIdsError(item.Paths.FullPath, exception));
        }

        public List<string> ExtractPublicLinks(Item item)
        {
            return ExtractFromFields(item, ProcessPublicLinkField, exception => _loggingService.LogExtractPublicLinkError(item.Paths.FullPath, exception));
        }

        private List<string> ExtractFromFields(Item item, Action<Field, HashSet<string>> fieldProcessor, Action<Exception> errorLogger)
        {
            var resultSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value))
                        continue;

                    fieldProcessor(field, resultSet);
                }
            }
            catch (Exception exception)
            {
                errorLogger(exception);
            }

            return resultSet.ToList();
        }

        private void ProcessAssetIdField(Field field, HashSet<string> resultSet)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == ImageFieldType)
            {
                var imageField = (ImageField)field;
                var thumbnailSrc = imageField.GetAttribute(ThumbnailSourceAttribute);

                ExtractIdsFromUrl(resultSet, thumbnailSrc);
            }
        }

        private void ProcessPublicLinkField(Field field, HashSet<string> resultSet)
        {
            var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

            if (fieldTypeKey == RichTextFieldType)
            {
                var richTextContent = string.IsNullOrWhiteSpace(field.InheritedValue) ? field.Value : field.InheritedValue;

                if (!string.IsNullOrWhiteSpace(richTextContent))
                {
                    ExtractImageUrlsFromHtml(resultSet, richTextContent);
                }
            }
        }

        private void ExtractImageUrlsFromHtml(HashSet<string> sink, string htmlContent)
        {
            var matches = ImgSrcRegex.Matches(htmlContent);

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var imageUrl = match.Groups[1].Value;
                    AddIfNotEmpty(sink, imageUrl);
                }
            }
        }

        private void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            var trimmedValue = value?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                sink.Add(trimmedValue);
                _loggingService.LogAssetIdAdded(trimmedValue);
            }
        }

        private void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var urlMatch = GatewayIdRegex.Match(url);

            if (urlMatch.Success && urlMatch.Groups.Count > 1)
            {
                var gatewayIdValue = urlMatch.Groups[1].Value;

                AddIfNotEmpty(sink, gatewayIdValue);
                _loggingService.LogAssetIdExtractedFromUrl(gatewayIdValue, url);
            }
        }
    }
}