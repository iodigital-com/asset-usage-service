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
        private readonly AssetUsageServiceClient _client;
        private readonly string _auditLogPath;

        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public PublishTelemetryService(AssetUsageServiceClient client, string auditLogPath)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _auditLogPath = string.IsNullOrWhiteSpace(auditLogPath) ? throw new ArgumentException(nameof(auditLogPath)) : auditLogPath;
        }

        public void RecordItemProcessed(Item item, PublishOptions options, string sourceDatabaseName)
        {
            var assetIds = ExtractAssetIds(item);
            var publicLink = ExtractPublicLink(item);
            var payload = BuildAssetUsageEvent(item, options, assetIds, publicLink);
            Task.Run(() => _client.SendAsync(payload));
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
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var typeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (typeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            AddIfNotEmpty(ids, imageField.GetAttribute("DamId"));
                            AddIfNotEmpty(ids, imageField.GetAttribute("dam-id"));
                            AddIfNotEmpty(ids, imageField.GetAttribute("stylelabs-content-id"));
                            ExtractIdsFromUrl(ids, imageField.GetAttribute("Thumbnail"));
                            ExtractIdsFromUrl(ids, imageField.GetAttribute("thumbnailsrc"));
                            ExtractIdsFromUrl(ids, imageField.GetAttribute("Source"));
                            ExtractIdsFromUrl(ids, imageField.GetAttribute("src"));
                            break;

                        case "general link":
                        case "link":
                            try
                            {
                                var x = XElement.Parse(field.Value);
                                AddIfNotEmpty(ids, (string)x.Attribute("DamId"));
                                AddIfNotEmpty(ids, (string)x.Attribute("dam-id"));
                                AddIfNotEmpty(ids, (string)x.Attribute("stylelabs-content-id"));
                                ExtractIdsFromUrl(ids, (string)x.Attribute("url"));
                                ExtractIdsFromUrl(ids, (string)x.Attribute("href"));
                                ExtractIdsFromUrl(ids, (string)x.Attribute("Source"));
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"[ExtractAssetIds] Malformed link XML in field '{field.Name}' on '{item.Paths.FullPath}'", ex, typeof(PublishTelemetryService));
                            }
                            break;

                        default:
                            foreach (Match m in GatewayIdRegex.Matches(field.Value))
                            {
                                if (m.Success && m.Groups.Count > 1)
                                {
                                    AddIfNotEmpty(ids, m.Groups[1].Value);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[ExtractAssetIds] Error for item {item.Paths.FullPath}", ex, typeof(PublishTelemetryService));
            }

            return ids.ToList();
        }

        private static string ExtractPublicLink(Item item)
        {
            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;
                    var typeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    switch (typeKey)
                    {
                        case "image":
                            var imageField = (ImageField)field;
                            var chUrl = FirstNonEmpty(
                                imageField.GetAttribute("Source"),
                                imageField.GetAttribute("source"),
                                imageField.GetAttribute("src"),
                                imageField.GetAttribute("url"),
                                imageField.GetAttribute("public_link")
                            );
                            if (!string.IsNullOrEmpty(chUrl)) return chUrl;

                            if (imageField.MediaItem != null)
                            {
                                var mediaUrl = MediaManager.GetMediaUrl(imageField.MediaItem);
                                if (!string.IsNullOrWhiteSpace(mediaUrl)) return mediaUrl;
                            }
                            break;

                        case "general link":
                        case "link":
                            var linkField = new LinkField(field);
                            var lfUrl = FirstNonEmpty(linkField.Url);
                            if (!string.IsNullOrEmpty(lfUrl)) return lfUrl;

                            try
                            {
                                var x = XElement.Parse(field.Value);
                                var mappedUrl = FirstNonEmpty(
                                    (string)x.Attribute("url"),
                                    (string)x.Attribute("href"),
                                    (string)x.Attribute("Source"),
                                    (string)x.Attribute("source"),
                                    (string)x.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(mappedUrl)) return mappedUrl;
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"[ExtractPublicLink] Malformed link XML in field '{field.Name}'", ex, typeof(PublishTelemetryService));
                            }
                            break;

                        case "file":
                            try
                            {
                                var x = XElement.Parse(field.Value);
                                var fileUrl = FirstNonEmpty(
                                    (string)x.Attribute("url"),
                                    (string)x.Attribute("href"),
                                    (string)x.Attribute("Source"),
                                    (string)x.Attribute("src"),
                                    (string)x.Attribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(fileUrl)) return fileUrl;
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"[ExtractPublicLink] Malformed file XML in field '{field.Name}'", ex, typeof(PublishTelemetryService));
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ExtractPublicLink] Error extracting public link from item {item.Paths.FullPath}", ex, typeof(PublishTelemetryService));
            }

            return string.Empty;
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            var trimmed = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                sink.Add(trimmed);
                Log.Info($"[AddIfNotEmpty] Added id '{trimmed}'", typeof(PublishTelemetryService));
            }
        }

        private static void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var m = GatewayIdRegex.Match(url);
            if (m.Success && m.Groups.Count > 1)
            {
                sink.Add(m.Groups[1].Value);
                Log.Info($"[ExtractIdsFromUrl] Extracted id '{m.Groups[1].Value}' from URL '{url}'", typeof(PublishTelemetryService));
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            return string.Empty;
        }

        private static string GetPublishedBy()
        {
            try
            {
                var current = global::Sitecore.Security.Accounts.User.Current;
                if (current != null && current.IsAuthenticated && !string.IsNullOrWhiteSpace(current.Name))
                    return current.Name;
            }
            catch { }

            var win = System.Security.Principal.WindowsIdentity.GetCurrent();
            var name = win?.Name;
            return string.IsNullOrWhiteSpace(name) ? "system" : name;
        }

        private static string NowString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void WriteAudit(object data)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(data);

                var dir = Path.GetDirectoryName(_auditLogPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                lock (FileLock)
                {
                    File.AppendAllText(_auditLogPath, json + Environment.NewLine);
                }

                Log.Info("[WriteAudit] Append complete.", this);
            }
            catch (Exception ex)
            {
                Log.Error("[WriteAudit] Error writing JSON", ex, this);
            }
        }
    }
}