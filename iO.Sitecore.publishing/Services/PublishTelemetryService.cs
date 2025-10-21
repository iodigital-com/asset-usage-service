using iO.Sitecore.publishing.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Publishing;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class PublishTelemetryService
    {
        private readonly AssetUsageServiceClient assetUsageClient;
        private readonly string auditLogPath;

        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public PublishTelemetryService(AssetUsageServiceClient client, string auditLogPath)
        {
            assetUsageClient = client ?? throw new ArgumentNullException(nameof(client));
            this.auditLogPath = string.IsNullOrWhiteSpace(auditLogPath) ? throw new ArgumentException(nameof(auditLogPath)) : auditLogPath;
        }

        public void RecordItemProcessed(Item item, PublishOptions options, string sourceDatabaseName)
        {
            var assetIds = ExtractAssetIds(item);
            var publicLink = ExtractPublicLink(item);
            var payload = BuildAssetUsageEvent(item, options, assetIds, publicLink);
            Task.Run(() => assetUsageClient.SendAsync(payload));
            var record = BuildPublishRecord("ItemProcessed", item, options, assetIds, publicLink, sourceDatabaseName, options.TargetDatabase?.Name ?? string.Empty);
            WriteAudit(record);
        }

        public void RecordPublishEndItem(Item item, PublishOptions options, string sourceDatabaseName, string targetDatabaseName)
        {
            var assetIds = ExtractAssetIds(item);
            var publicLink = ExtractPublicLink(item);
            var record = BuildPublishRecord("PublishEnd", item, options, assetIds, publicLink, sourceDatabaseName, targetDatabaseName);
            WriteAudit(record);
        }

        public void RecordPublishEndRemote(string eventQueueName, IEnumerable<string> databasesRaised)
        {
            var summary = new
            {
                Timestamp = NowString(),
                EventType = "PublishEndRemote",
                EventQueueName = eventQueueName,
                DatabasesRaised = databasesRaised?.ToList() ?? new List<string>()
            };
            WriteAudit(summary);
        }

        private static AssetUsageEvent BuildAssetUsageEvent(Item item, PublishOptions options, List<string> assetIds, string publicLink)
        {
            return new AssetUsageEvent
            {
                PublicLink = publicLink,
                ItemId = item.ID.ToString(),
                ItemPath = item.Paths.FullPath,
                ItemName = item.Name,
                TemplateName = item.TemplateName,
                Language = item.Language.Name,
                Version = item.Version.Number,
                PublishedAtUtc = DateTime.UtcNow,
                PublishedBy = GetPublishedBy(),
                AssetIds = assetIds,
                TargetDatabase = options.TargetDatabase?.Name ?? string.Empty
            };
        }

        private static object BuildPublishRecord(string eventType, Item item, PublishOptions options, List<string> assetIds, string publicLink, string sourceDatabase, string targetDatabase)
        {
            return new
            {
                PublicLink = publicLink,
                Timestamp = NowString(),
                EventType = eventType,
                ItemId = item.ID.ToString(),
                AssetId = assetIds.FirstOrDefault() ?? string.Empty,
                AssetIds = assetIds,
                ItemName = item.Name,
                ItemPath = item.Paths.FullPath,
                TemplateName = item.TemplateName,
                TemplateId = item.TemplateID.ToString(),
                Language = item.Language.Name,
                Version = item.Version.Number,
                SourceDatabase = sourceDatabase,
                TargetDatabase = targetDatabase,
                PublishMode = options.Mode.ToString(),
                DeepPublish = options.Deep
            };
        }

        private static List<string> ExtractAssetIds(Item item)
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
                                Log.Warn($"[ExtractAssetIds] Malformed link XML in field '{field.Name}' on '{item.Paths.FullPath}'", exception, typeof(PublishTelemetryService));
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
                Log.Warn($"[ExtractAssetIds] Error for item {item.Paths.FullPath}", exception, typeof(PublishTelemetryService));
            }

            return assetIdsSet.ToList();
        }

        private static string ExtractPublicLink(Item item)
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
                                Log.Warn($"[ExtractPublicLink] Malformed link XML in field '{field.Name}'", exception, typeof(PublishTelemetryService));
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
                                Log.Warn($"[ExtractPublicLink] Malformed file XML in field '{field.Name}'", exception, typeof(PublishTelemetryService));
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[ExtractPublicLink] Error extracting public link from item {item.Paths.FullPath}", exception, typeof(PublishTelemetryService));
            }

            return string.Empty;
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            var trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                sink.Add(trimmedValue);
                Log.Info($"[AddIfNotEmpty] Added id '{trimmedValue}'", typeof(PublishTelemetryService));
            }
        }

        private static void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var urlMatch = GatewayIdRegex.Match(url);
            if (urlMatch.Success && urlMatch.Groups.Count > 1)
            {
                var gatewayIdValue = urlMatch.Groups[1].Value;
                sink.Add(gatewayIdValue);
                Log.Info($"[ExtractIdsFromUrl] Extracted id '{gatewayIdValue}' from URL '{url}'", typeof(PublishTelemetryService));
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

        private static string GetPublishedBy()
        {
            try
            {
                var currentUser = global::Sitecore.Security.Accounts.User.Current;
                if (currentUser != null && currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(currentUser.Name))
                    return currentUser.Name;
            }
            catch { }

            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var userName = windowsIdentity?.Name;
            return string.IsNullOrWhiteSpace(userName) ? "system" : userName;
        }

        private static string NowString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void WriteAudit(object record)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(record);

                var directory = Path.GetDirectoryName(auditLogPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (FileLock)
                {
                    File.AppendAllText(auditLogPath, json + Environment.NewLine);
                }

                Log.Info("[WriteAudit] Append complete.", this);
            }
            catch (Exception exception)
            {
                Log.Error("[WriteAudit] Error writing JSON", exception, this);
            }
        }
    }
}