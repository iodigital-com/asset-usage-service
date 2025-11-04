using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class AssetExtractionService : IAssetExtractionService
    {
        private readonly PublishLoggingService loggingService;
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public AssetExtractionService(PublishLoggingService loggingService)
        {
            this.loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public List<string> ExtractAssetIds(Item item)
        {
            var assetIdsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (fieldTypeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("DamId"));
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("dam-id"));
                            AddIfNotEmpty(assetIdsSet, imageField.GetAttribute("stylelabs-content-id"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Thumbnail"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("thumbnailsrc"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("Source"));
                            ExtractIdsFromUrl(assetIdsSet, imageField.GetAttribute("src"));
                            break;

                        case "general link":
                        case "link":
                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("DamId"));
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("dam-id"));
                                AddIfNotEmpty(assetIdsSet, (string)xmlElement.Attribute("stylelabs-content-id"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("url"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("href"));
                                ExtractIdsFromUrl(assetIdsSet, (string)xmlElement.Attribute("Source"));
                            }
                            catch (Exception exception)
                            {
                                loggingService.LogMalformedLinkXml(field.Name, item.Paths.FullPath, exception);
                            }
                            break;

                        default:
                            foreach (Match urlMatch in GatewayIdRegex.Matches(field.Value))
                            {
                                if (urlMatch.Success && urlMatch.Groups.Count > 1)
                                {
                                    AddIfNotEmpty(assetIdsSet, urlMatch.Groups[1].Value);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                loggingService.LogExtractAssetIdsError(item.Paths.FullPath, exception);
            }

            return assetIdsSet.ToList();
        }

        public string ExtractPublicLink(Item item)
        {
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (fieldTypeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            var contentHubUrl = FirstNonEmpty(
                                imageField.GetAttribute("Source"),
                                imageField.GetAttribute("source"),
                                imageField.GetAttribute("src"),
                                imageField.GetAttribute("url"),
                                imageField.GetAttribute("public_link")
                            );
                            if (!string.IsNullOrEmpty(contentHubUrl)) return contentHubUrl;

                            if (imageField.MediaItem != null)
                            {
                                var mediaUrl = MediaManager.GetMediaUrl(imageField.MediaItem);
                                if (!string.IsNullOrWhiteSpace(mediaUrl)) return mediaUrl;
                            }
                            break;

                        case "general link":
                        case "link":
                            var linkField = new LinkField(field);
                            var linkFieldUrl = FirstNonEmpty(linkField.Url);
                            if (!string.IsNullOrEmpty(linkFieldUrl)) return linkFieldUrl;

                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                var mappedUrl = FirstNonEmpty(
                                    (string)xmlElement.Attribute("url"),
                                    (string)xmlElement.Attribute("href"),
                                    (string)xmlElement.Attribute("Source"),
                                    (string)xmlElement.Attribute("source"),
                                    (string)xmlElement.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(mappedUrl)) return mappedUrl;
                            }
                            catch (Exception exception)
                            {
                                loggingService.LogMalformedPublicLinkXml(field.Name, exception);
                            }
                            break;

                        case "file":
                            try
                            {
                                var xmlElement = XElement.Parse(field.Value);
                                var fileUrl = FirstNonEmpty(
                                    (string)xmlElement.Attribute("url"),
                                    (string)xmlElement.Attribute("href"),
                                    (string)xmlElement.Attribute("Source"),
                                    (string)xmlElement.Attribute("src"),
                                    (string)xmlElement.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(fileUrl)) return fileUrl;
                            }
                            catch (Exception exception)
                            {
                                loggingService.LogMalformedFileXml(field.Name, exception);
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                loggingService.LogExtractPublicLinkError(item.Paths.FullPath, exception);
            }

            return string.Empty;
        }

        private void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            var trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                sink.Add(trimmedValue);
                loggingService.LogAssetIdAdded(trimmedValue);
            }
        }

        private void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var urlMatch = GatewayIdRegex.Match(url);
            if (urlMatch.Success && urlMatch.Groups.Count > 1)
            {
                var gatewayIdValue = urlMatch.Groups[1].Value;
                sink.Add(gatewayIdValue);
                loggingService.LogAssetIdExtractedFromUrl(gatewayIdValue, url);
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var valueCandidate in values)
            {
                if (!string.IsNullOrWhiteSpace(valueCandidate)) return valueCandidate.Trim();
            }
            return string.Empty;
        }
    }
}