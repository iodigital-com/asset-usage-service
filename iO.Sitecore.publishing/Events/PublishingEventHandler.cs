
using iO.Sitecore.publishing.Events;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.PublishItem;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Version = Sitecore.Data.Version;

namespace iO.Sitecore.Publishing.Events
{
    public class PublishingEventHandler
    {
        public PublishingEventHandler() { }

        private static readonly string LogFilePath = @"C:\inetpub\wwwroot\SitecoreXPLocalsc.dev.local\App_Data\logs\published-items.json";
        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly AssetUsageServiceClient _client = new AssetUsageServiceClient();

        public void OnItemProcessed(object sender, EventArgs args)
        {
            try
            {
                var itemProcessedArgs = args as ItemProcessedEventArgs;
                if (itemProcessedArgs == null)
                {
                    Log.Info("[OnItemProcessed] ItemProcessedEventArgs is null; returning.", this);
                    return;
                }

                var context = itemProcessedArgs.Context;
                if (context == null)
                {
                    Log.Info("[OnItemProcessed] Context is null; returning.", this);
                    return;
                }

                var itemId = context.ItemId;
                if (ID.IsNullOrEmpty(itemId))
                {
                    Log.Info("[OnItemProcessed] ItemId is null or empty; returning.", this);
                    return;
                }

                var options = context.PublishOptions;
                if (options == null)
                {
                    Log.Info("[OnItemProcessed] PublishOptions is null; returning.", this);
                    return;
                }

                var sourceDb = options.SourceDatabase;
                if (sourceDb == null)
                {
                    Log.Info("[OnItemProcessed] SourceDatabase is null; returning.", this);
                    return;
                }

                var versionToPublish = context.VersionToPublish;
                var language = versionToPublish?.Language ?? Language.Current;
                var version = versionToPublish?.Version ?? Version.Latest;

                var item = sourceDb.GetItem(itemId, language, version);
                if (item == null)
                {
                    Log.Info($"[OnItemProcessed] Could not retrieve item {itemId} from {sourceDb.Name}; returning.", this);
                    return;
                }
                var ids = GetAssetIds(item);
               
                var azurePayload = new AssetUsageEvent
                {
                    PublicLink = GetLinkField(item),
                    ItemId = item.ID.ToString(),
                    ItemPath = item.Paths.FullPath,
                    ItemName = item.Name,
                    TemplateName = item.TemplateName,
                    Language = item.Language.Name,
                    Version = item.Version.Number,
                    PublishedAtUtc = DateTime.UtcNow,
                    PublishedBy = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name ?? "system",
                    AssetIds = ids,
                    TargetDatabase = options.TargetDatabase?.Name ?? string.Empty
                };

                Task.Run(async () => await _client.SendAsync(azurePayload));

                var publishItemData = new
                {
                    PublicLink = GetLinkField(item),
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventType = "ItemProcessed",
                    ItemId = item.ID.ToString(),
                    AssetId = ids.FirstOrDefault() ?? string.Empty,
                    AssetIds = ids,
                    ItemName = item.Name,
                    ItemPath = item.Paths.FullPath,
                    TemplateName = item.TemplateName,
                    TemplateId = item.TemplateID.ToString(),
                    Language = item.Language.Name,
                    Version = item.Version.Number,
                    SourceDatabase = sourceDb.Name,
                    TargetDatabase = options.TargetDatabase?.Name ?? string.Empty,
                    PublishMode = options.Mode.ToString(),
                    DeepPublish = options.Deep
                };

                WriteToJsonFile(publishItemData);
            }
            catch (Exception ex)
            {
                Log.Error("[OnItemProcessed] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnItemProcessed] Exit.", this);
            }
        }

        public void OnPublishEnd(object sender, EventArgs args)
        {
            try
            {
                var publisher = Event.ExtractParameter<Publisher>(args, 0) as Publisher;
                if (publisher == null)
                {
                    Log.Info("[OnPublishEnd] Publisher is null; returning.", this);
                    return;
                }

                var options = publisher.Options;
                if (options == null)
                {
                    Log.Info("[OnPublishEnd] Publisher.Options is null; returning.", this);
                    return;
                }

                var rootItem = options.RootItem;
                var targetDbName = options.TargetDatabase?.Name ?? string.Empty;

                if (rootItem == null)
                {
                    Log.Info("[OnPublishEnd] RootItem is null; returning.", this);
                    return;
                }

                IEnumerable<Item> itemsToLog = options.Deep
                    ? rootItem.Axes.GetDescendants().Concat(new[] { rootItem })
                    : new[] { rootItem };

                int total = 0;
                foreach (var item in itemsToLog)
                {
                    total++;
                    var ids = GetAssetIds(item);

                    var publishItemData = new
                    {
                        PublicLink = GetLinkField(item),
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        EventType = "PublishEnd",
                        ItemId = item.ID.ToString(),
                        AssetId = ids.FirstOrDefault() ?? string.Empty,
                        AssetIds = ids,
                        ItemName = item.Name,
                        ItemPath = item.Paths.FullPath,
                        TemplateName = item.TemplateName,
                        TemplateId = item.TemplateID.ToString(),
                        Language = item.Language.Name,
                        Version = item.Version.Number,
                        SourceDatabase = item.Database?.Name ?? string.Empty,
                        TargetDatabase = targetDbName,
                        PublishMode = options.Mode.ToString(),
                        DeepPublish = options.Deep
                    };

                    WriteToJsonFile(publishItemData);
                }

            }
            catch (Exception ex)
            {
                Log.Error("[OnPublishEnd] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnPublishEnd] Exit.", this);
            }
        }

        public void OnPublishEndRemote(object sender, EventArgs args)
        {
            try
            {
                var remoteArgs = args as PublishEndRemoteEventArgs;
                if (remoteArgs == null)
                {
                    Log.Info("[OnPublishEndRemote] PublishEndRemoteEventArgs is null; returning.", this);
                    return;
                }
                var dbs = Factory.GetDatabases()
                    .Where(db => db.RemoteEvents.EventQueue.Name == remoteArgs.EventQueueName)
                    .ToList();

                if (dbs.Count == 0)
                {
                    Log.Info("[OnPublishEndRemote] No databases matched EventQueueName.", this);
                }
                else
                {
                    foreach (var db in dbs)
                    {
                        Log.Info($"[OnPublishEndRemote] Raised by database: {db.Name}", this);
                    }
                }

                var summary = new
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventType = "PublishEndRemote",
                    EventQueueName = remoteArgs.EventQueueName,
                    DatabasesRaised = dbs.Select(d => d.Name).ToList()
                };

                WriteToJsonFile(summary);
            }
            catch (Exception ex)
            {
                Log.Error("[OnPublishEndRemote] Error", ex, this);
            }
            finally
            {
                Log.Info("[OnPublishEndRemote] Exit.", this);
            }
        }

        private static List<string> GetAssetIds(Item item)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item == null)
            {
                Log.Info("[GetAssetIds] Item is null; returning empty list.", typeof(PublishingEventHandler));
                return ids.ToList();
            }

            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;

                    var typeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    if (typeKey == "image" || typeKey == "general link" || typeKey == "link")
                    {
                        Log.Info($"[GetAssetIds] Inspecting field '{field.Name}' (type='{typeKey}')", typeof(PublishingEventHandler));
                    }

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
                                ExtractIdsFromUrl(ids, (string)x.Attribute("Source"));
                                ExtractIdsFromUrl(ids, (string)x.Attribute("href"));
                                ExtractIdsFromUrl(ids, (string)x.Attribute("url"));
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"[GetAssetIds] Malformed link XML in field '{field.Name}' on '{item.Paths.FullPath}'", ex, typeof(PublishingEventHandler));
                            }
                            break;

                        default:
                            foreach (Match m in GatewayIdRegex.Matches(field.Value))
                            {
                                if (m.Success && m.Groups.Count > 1)
                                {
                                    var val = m.Groups[1].Value;
                                    AddIfNotEmpty(ids, val);
                                    Log.Info($"[GetAssetIds] Matched gateway id '{val}' in field '{field.Name}'", typeof(PublishingEventHandler));
                                }
                            }
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Warn($"[GetAssetIds] Error for item {item.Paths.FullPath}", ex, typeof(PublishingEventHandler));
            }

            return ids.ToList();
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sink.Add(value.Trim());
                Log.Info($"[AddIfNotEmpty] Added id '{value.Trim()}'", typeof(PublishingEventHandler));
            }
        }

        private static void ExtractIdsFromUrl(HashSet<string> sink, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var m = GatewayIdRegex.Match(url);
            if (m.Success && m.Groups.Count > 1)
            {
                var val = m.Groups[1].Value;
                sink.Add(val);
                Log.Info($"[ExtractIdsFromUrl] Extracted id '{val}' from URL '{url}'", typeof(PublishingEventHandler));
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

        private static string GetLinkField(Item item)
        {
            if (item == null) return string.Empty;

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
                            {
                                var imageField = (ImageField)field;

                                var chUrl = FirstNonEmpty(
                                    imageField.GetAttribute("Source"),
                                    imageField.GetAttribute("source"),
                                    imageField.GetAttribute("src"),
                                    imageField.GetAttribute("url"),
                                    imageField.GetAttribute("public_link")
                                );
                                if (!string.IsNullOrEmpty(chUrl))
                                {
                                    Log.Info($"[GetLinkField] Image '{field.Name}' → CH URL: {chUrl}", typeof(PublishingEventHandler));
                                    return chUrl;
                                }

                                if (imageField.MediaItem != null)
                                {
                                    var mediaUrl = MediaManager.GetMediaUrl(imageField.MediaItem);
                                    if (!string.IsNullOrWhiteSpace(mediaUrl))
                                    {
                                        Log.Info($"[GetLinkField] Image '{field.Name}' → Media URL: {mediaUrl}", typeof(PublishingEventHandler));
                                        return mediaUrl;
                                    }
                                }
                                break;
                            }

                        case "general link":
                        case "link":
                            {
                                var linkField = new LinkField(field);
                                var lfUrl = FirstNonEmpty(linkField.Url);
                                if (!string.IsNullOrEmpty(lfUrl))
                                {
                                    Log.Info($"[GetLinkField] Link '{field.Name}' → Url: {lfUrl}", typeof(PublishingEventHandler));
                                    return lfUrl;
                                }

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
                                    if (!string.IsNullOrEmpty(mappedUrl))
                                    {
                                        Log.Info($"[GetLinkField] Link '{field.Name}' → XML URL: {mappedUrl}", typeof(PublishingEventHandler));
                                        return mappedUrl;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn($"[GetLinkField] Malformed link XML in field '{field.Name}'", ex, typeof(PublishingEventHandler));
                                }
                                break;
                            }

                        case "file":
                            {
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
                                    if (!string.IsNullOrEmpty(fileUrl))
                                    {
                                        Log.Info($"[GetLinkField] File '{field.Name}' → URL: {fileUrl}", typeof(PublishingEventHandler));
                                        return fileUrl;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn($"[GetLinkField] Malformed file XML in field '{field.Name}'", ex, typeof(PublishingEventHandler));
                                }
                                break;
                            }
                    }
                }

                Log.Info($"[GetLinkField] No public link found for item {item.Paths.FullPath}", typeof(PublishingEventHandler));
            }
            catch (Exception ex)
            {
                Log.Error($"[GetLinkField] Error extracting public link from item {item.Paths.FullPath}", ex, typeof(PublishingEventHandler));
            }

            return string.Empty;
        }

        private void WriteToJsonFile(object data)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(data);

                lock (FileLock)
                {
                    File.AppendAllText(LogFilePath, json + Environment.NewLine);
                }
                Log.Info("[WriteToJsonFile] Append complete.", this);
            }
            catch (Exception ex)
            {
                Log.Error("[WriteToJsonFile] Error writing JSON", ex, this);
            }
        }
    }
}
