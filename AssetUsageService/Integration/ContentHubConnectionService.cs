using Google.Protobuf.WellKnownTypes;
using iO.Sitecore.publishing.Events;
using Microsoft.Extensions.Logging;
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
using System.Security.Policy;
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

        private static readonly string LogFilePath = @"C:\inetpub\wwwroot\Sitecore-xp-localsc.dev.local\App_Data\logs\published-items.json";
        private static readonly object FileLock = new object();
        private static readonly Regex GatewayIdRegex = new Regex(@"/api/gateway/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly AssetUsageServiceClient AssetUsageClient = new AssetUsageServiceClient();

        public void OnItemProcessed(object sender, EventArgs args)
        {
            try
            {
                var itemProcessedArguments = args as ItemProcessedEventArgs;
                if (itemProcessedArguments == null)
                {
                    Log.Info("[OnItemProcessed] ItemProcessedEventArgs is null; returning.", this);
                    return;
                }

                var publishContext = itemProcessedArguments.Context;
                if (publishContext == null)
                {
                    Log.Info("[OnItemProcessed] Context is null; returning.", this);
                    return;
                }

                var itemId = publishContext.ItemId;
                if (ID.IsNullOrEmpty(itemId))
                {
                    Log.Info("[OnItemProcessed] ItemId is null or empty; returning.", this);
                    return;
                }

                var publishOptions = publishContext.PublishOptions;
                if (publishOptions == null)
                {
                    Log.Info("[OnItemProcessed] PublishOptions is null; returning.", this);
                    return;
                }

                var sourceDatabase = publishOptions.SourceDatabase;
                if (sourceDatabase == null)
                {
                    Log.Info("[OnItemProcessed] SourceDatabase is null; returning.", this);
                    return;
                }

                var versionToPublish = publishContext.VersionToPublish;
                var language = versionToPublish?.Language ?? Language.Current;
                var version = versionToPublish?.Version ?? Version.Latest;

                var item = sourceDatabase.GetItem(itemId, language, version);
                if (item == null)
                {
                    Log.Info($"[OnItemProcessed] Could not retrieve item {itemId} from {sourceDatabase.Name}; returning.", this);
                    return;
                }

                var assetIds = GetAssetIds(item);

                var azurePayload = new AssetUsageEvent
                {
                    ItemId = item.ID.ToString(),
                    ItemPath = item.Paths.FullPath,
                    ItemName = item.Name,
                    TemplateName = item.TemplateName,
                    Language = item.Language.Name,
                    Version = item.Version.Number,
                    PublishedAtUtc = DateTime.UtcNow,
                    PublishedBy = GetPublishedBy(),
                    AssetIds = assetIds,
                    TargetDatabase = publishOptions.TargetDatabase?.Name ?? string.Empty
                };

                Task.Run(async () => await AssetUsageClient.SendAsync(azurePayload));

                var publishRecord = new
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventType = "ItemProcessed",
                    ItemId = item.ID.ToString(),
                    AssetId = assetIds.FirstOrDefault() ?? string.Empty,
                    AssetIds = assetIds,
                    ItemName = item.Name,
                    ItemPath = item.Paths.FullPath,
                    TemplateName = item.TemplateName,
                    TemplateId = item.TemplateID.ToString(),
                    Language = item.Language.Name,
                    Version = item.Version.Number,
                    SourceDatabase = sourceDatabase.Name,
                    TargetDatabase = publishOptions.TargetDatabase?.Name ?? string.Empty,
                    PublishMode = publishOptions.Mode.ToString(),
                    DeepPublish = publishOptions.Deep
                };

                WriteToJsonFile(publishRecord);
            }
            catch (Exception exception)
            {
                Log.Error("[OnItemProcessed] Error", exception, this);
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

                var publishOptions = publisher.Options;
                if (publishOptions == null)
                {
                    Log.Info("[OnPublishEnd] Publisher.Options is null; returning.", this);
                    return;
                }

                var rootItem = publishOptions.RootItem;
                var targetDatabaseName = publishOptions.TargetDatabase?.Name ?? string.Empty;

                if (rootItem == null)
                {
                    Log.Info("[OnPublishEnd] RootItem is null; returning.", this);
                    return;
                }

                IEnumerable<Item> itemsToLog = publishOptions.Deep
                    ? rootItem.Axes.GetDescendants().Concat(new[] { rootItem })
                    : new[] { rootItem };

                var totalItems = 0;
                foreach (var item in itemsToLog)
                {
                    totalItems++;
                    var assetIds = GetAssetIds(item);

                    var publishRecord = new
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        EventType = "PublishEnd",
                        ItemId = item.ID.ToString(),
                        AssetId = assetIds.FirstOrDefault() ?? string.Empty,
                        AssetIds = assetIds,
                        ItemName = item.Name,
                        ItemPath = item.Paths.FullPath,
                        TemplateName = item.TemplateName,
                        TemplateId = item.TemplateID.ToString(),
                        Language = item.Language.Name,
                        Version = item.Version.Number,
                        SourceDatabase = item.Database?.Name ?? string.Empty,
                        TargetDatabase = targetDatabaseName,
                        PublishMode = publishOptions.Mode.ToString(),
                        DeepPublish = publishOptions.Deep
                    };

                    WriteToJsonFile(publishRecord);
                }
            }
            catch (Exception exception)
            {
                Log.Error("[OnPublishEnd] Error", exception, this);
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
                var publishEndRemoteArguments = args as PublishEndRemoteEventArgs;
                if (publishEndRemoteArguments == null)
                {
                    Log.Info("[OnPublishEndRemote] PublishEndRemoteEventArgs is null; returning.", this);
                    return;
                }
                var databases = Factory.GetDatabases()
                    .Where(database => database.RemoteEvents.EventQueue.Name == publishEndRemoteArguments.EventQueueName)
                    .ToList();

                if (databases.Count == 0)
                {
                    Log.Info("[OnPublishEndRemote] No databases matched EventQueueName.", this);
                }
                else
                {
                    foreach (var database in databases)
                    {
                        Log.Info($"[OnPublishEndRemote] Raised by database: {database.Name}", this);
                    }
                }

                var summaryRecord = new
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventType = "PublishEndRemote",
                    EventQueueName = publishEndRemoteArguments.EventQueueName,
                    DatabasesRaised = databases.Select(d => d.Name).ToList()
                };

                WriteToJsonFile(summaryRecord);
            }
            catch (Exception exception)
            {
                Log.Error("[OnPublishEndRemote] Error", exception, this);
            }
            finally
            {
                Log.Info("[OnPublishEndRemote] Exit.", this);
            }
        }

        private static List<string> GetAssetIds(Item item)
        {
            var assetIdsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item == null)
            {
                Log.Info("[GetAssetIds] Item is null; returning empty list.", typeof(PublishingEventHandler));
                return assetIdsSet.ToList();
            }

            try
            {
                item.Fields.ReadAll();

                foreach (Field field in item.Fields)
                {
                    if (string.IsNullOrEmpty(field?.Value)) continue;

                    var fieldTypeKey = (field.TypeKey ?? string.Empty).ToLowerInvariant();

                    if (fieldTypeKey == "image" || fieldTypeKey == "general link" || fieldTypeKey == "link")
                    {
                        Log.Info($"[GetAssetIds] Inspecting field '{field.Name}' (type='{fieldTypeKey}')", typeof(PublishingEventHandler));
                    }

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
                                var element = XElement.Parse(field.Value);
                                AddIfNotEmpty(assetIdsSet, (string)element.Attribute("DamId"));
                                AddIfNotEmpty(assetIdsSet, (string)element.Attribute("dam-id"));
                                AddIfNotEmpty(assetIdsSet, (string)element.Attribute("stylelabs-content-id"));
                                ExtractIdsFromUrl(assetIdsSet, (string)element.Attribute("href"));
                                ExtractIdsFromUrl(assetIdsSet, (string)element.Attribute("url"));
                            }
                            catch (Exception exception)
                            {
                                Log.Warn($"[GetAssetIds] Malformed link XML in field '{field.Name}' on '{item.Paths.FullPath}'", exception, typeof(PublishingEventHandler));
                            }
                            break;

                        default:
                            foreach (Match urlMatch in GatewayIdRegex.Matches(field.Value))
                            {
                                if (urlMatch.Success && urlMatch.Groups.Count > 1)
                                {
                                    var gatewayIdValue = urlMatch.Groups[1].Value;
                                    AddIfNotEmpty(assetIdsSet, gatewayIdValue);
                                    Log.Info($"[GetAssetIds] Matched gateway id '{gatewayIdValue}' in field '{field.Name}'", typeof(PublishingEventHandler));
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warn($"[GetAssetIds] Error for item {item.Paths.FullPath}", exception, typeof(PublishingEventHandler));
            }

            return assetIdsSet.ToList();
        }

        private static void AddIfNotEmpty(HashSet<string> sink, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmedValue = value.Trim();
                sink.Add(trimmedValue);
                Log.Info($"[AddIfNotEmpty] Added id '{trimmedValue}'", typeof(PublishingEventHandler));
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
                Log.Info($"[ExtractIdsFromUrl] Extracted id '{gatewayIdValue}' from URL '{url}'", typeof(PublishingEventHandler));
            }
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

        private void WriteToJsonFile(object record)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(record);

                lock (FileLock)
                {
                    File.AppendAllText(LogFilePath, json + Environment.NewLine);
                }
                Log.Info("[WriteToJsonFile] Append complete.", this);
            }
            catch (Exception exception)
            {
                Log.Error("[WriteToJsonFile] Error writing JSON", exception, this);
            }
        }
    }
}