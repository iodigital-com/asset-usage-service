using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        public void OnItemProcessed(object sender, EventArgs args)
        {
            Log.Info($"[OnItemProcessed] Enter. ArgsType={args?.GetType().FullName ?? "null"}", this);
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

                Log.Info($"[OnItemProcessed] Processing item: ID={item.ID}, Path={item.Paths.FullPath}", this);

                var ids = GetAssetIds(item);
                Log.Info($"[OnItemProcessed] Extracted {ids.Count} AssetId(s) for item {item.ID}", this);

                var publishItemData = new
                {
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

                Log.Info($"[OnItemProcessed] Writing JSON for item {item.ID}", this);
                WriteToJsonFile(publishItemData);
                Log.Info($"[OnItemProcessed] Successfully wrote JSON for item {item.ID}", this);
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
            Log.Info($"[OnPublishEnd] Enter. SenderType={sender?.GetType().FullName ?? "null"}, ArgsType={args?.GetType().FullName ?? "null"}", this);
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

                Log.Info($"[OnPublishEnd] RootItem={(rootItem != null ? rootItem.Paths.FullPath : "null")}", this);
                Log.Info($"[OnPublishEnd] TargetDatabase={targetDbName}", this);
                Log.Info($"[OnPublishEnd] Mode={options.Mode}, Deep={options.Deep}", this);

                if (rootItem == null)
                {
                    Log.Info("[OnPublishEnd] RootItem is null; returning.", this);
                    return;
                }

                // *** THIS IS THE KEY FIX: Process items like your old code did ***
                IEnumerable<Item> itemsToLog = options.Deep
                    ? rootItem.Axes.GetDescendants().Concat(new[] { rootItem })
                    : new[] { rootItem };

                int total = 0;
                foreach (var item in itemsToLog)
                {
                    total++;
                    Log.Info($"[OnPublishEnd] Processing item {total}: ID={item.ID}, Path={item.Paths.FullPath}", this);

                    var ids = GetAssetIds(item);
                    Log.Info($"[OnPublishEnd] Extracted AssetIds count={ids.Count} for item {item.ID}", this);

                    var publishItemData = new
                    {
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

                    Log.Info($"[OnPublishEnd] Writing JSON for item {item.ID}", this);
                    WriteToJsonFile(publishItemData);
                    Log.Info($"[OnPublishEnd] Wrote JSON for item {item.ID}", this);
                }

                Log.Info($"[OnPublishEnd] Completed. Logged {total} item(s) to {targetDbName}.", this);
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
            Log.Info($"[OnPublishEndRemote] Enter. SenderType={sender?.GetType().FullName ?? "null"}, ArgsType={args?.GetType().FullName ?? "null"}", this);
            try
            {
                var remoteArgs = args as PublishEndRemoteEventArgs;
                if (remoteArgs == null)
                {
                    Log.Info("[OnPublishEndRemote] PublishEndRemoteEventArgs is null; returning.", this);
                    return;
                }

                Log.Info($"[OnPublishEndRemote] EventQueueName={remoteArgs.EventQueueName}", this);

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

                Log.Info("[OnPublishEndRemote] Writing JSON summary.", this);
                WriteToJsonFile(summary);
                Log.Info("[OnPublishEndRemote] Wrote JSON summary.", this);
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
                Log.Info($"[GetAssetIds] Begin for item: {item.Paths.FullPath}", typeof(PublishingEventHandler));
                item.Fields.ReadAll();
                Log.Info($"[GetAssetIds] Field count: {item.Fields.Count}", typeof(PublishingEventHandler));

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;

                    var typeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    // Log when inspecting image/link fields
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

                Log.Info($"[GetAssetIds] Completed for item: {item.Paths.FullPath}. Found {ids.Count} id(s).", typeof(PublishingEventHandler));
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

        private void WriteToJsonFile(object data)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(data);

                Log.Info("[WriteToJsonFile] Begin append.", this);
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