using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Interfaces.Services;
using iO.Sitecore.Publishing.Models;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public class InitialItemAssetLinkService
    {
        private readonly Database _webDatabase;
        private readonly AssetUsageServiceClient _client;
        private readonly IAssetExtractionService _assetExtractionService;
        private readonly PublishLoggingService _loggingService;
        private readonly string _contentHubEndpoint;
        private const string ContentHubEndpoint = "AssetUsageService.ContentHubEndpoint";
        private const string LogPrefix = "[InitialItemAssetLinkService]";

        public InitialItemAssetLinkService()
        {
            Log.Info($"{LogPrefix} ========== CONSTRUCTOR START ==========", this);
            Log.Info($"{LogPrefix} Initializing InitialItemAssetLinkService...", this);

            try
            {
                Log.Info($"{LogPrefix} Attempting to get database 'TestMaster'...", this);

                using (new SecurityDisabler())
                {
                    _webDatabase = Factory.GetDatabase("TestMaster");
                }

                if (_webDatabase == null)
                {
                    Log.Error($"{LogPrefix} CRITICAL: TestMaster database is NULL!", this);
                    Log.Info($"{LogPrefix} Listing all available databases:", this);

                    var databases = Factory.GetDatabases();
                    if (databases == null)
                    {
                        Log.Error($"{LogPrefix} Factory.GetDatabases() returned NULL!", this);
                    }
                    else
                    {
                        Log.Info($"{LogPrefix} Found {databases.Count} databases:", this);
                        foreach (var db in databases)
                        {
                            if (db != null)
                            {
                                Log.Info($"{LogPrefix}   - Database: '{db.Name}' (ConnectionString: {db.ConnectionStringName ?? "NULL"})", this);
                            }
                            else
                            {
                                Log.Warn($"{LogPrefix}   - NULL database entry in list", this);
                            }
                        }
                    }

                    throw new InvalidOperationException("TestMaster database not found. Check Sitecore configuration.");
                }

                Log.Info($"{LogPrefix} SUCCESS: Database '{_webDatabase.Name}' obtained", this);
                Log.Info($"{LogPrefix}   - ConnectionString: {_webDatabase.ConnectionStringName ?? "NULL"}", this);
                Log.Info($"{LogPrefix}   - ReadOnly: {_webDatabase.ReadOnly}", this);

                Log.Info($"{LogPrefix} Creating AssetUsageServiceClient...", this);
                _client = new AssetUsageServiceClient();
                Log.Info($"{LogPrefix} SUCCESS: AssetUsageServiceClient created", this);

                Log.Info($"{LogPrefix} Creating PublishLoggingService...", this);
                _loggingService = new PublishLoggingService(this);
                Log.Info($"{LogPrefix} SUCCESS: PublishLoggingService created", this);

                Log.Info($"{LogPrefix} Creating AssetExtractionService...", this);
                _assetExtractionService = new AssetExtractionService(_loggingService);
                Log.Info($"{LogPrefix} SUCCESS: AssetExtractionService created", this);

                Log.Info($"{LogPrefix} Reading ContentHubEndpoint setting '{ContentHubEndpoint}'...", this);
                _contentHubEndpoint = Settings.GetSetting(ContentHubEndpoint);
                Log.Info($"{LogPrefix} ContentHubEndpoint value: '{_contentHubEndpoint ?? "NULL"}'", this);

                Log.Info($"{LogPrefix} ========== CONSTRUCTOR COMPLETE ==========", this);
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} CONSTRUCTOR EXCEPTION: {ex.GetType().Name}: {ex.Message}", ex, this);
                Log.Error($"{LogPrefix} Stack Trace: {ex.StackTrace}", this);
                if (ex.InnerException != null)
                {
                    Log.Error($"{LogPrefix} Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", this);
                    Log.Error($"{LogPrefix} Inner Stack Trace: {ex.InnerException.StackTrace}", this);
                }
                throw;
            }
        }

        public async Task ExecuteMigrationAsync()
        {
            Log.Info($"{LogPrefix} ========================================", this);
            Log.Info($"{LogPrefix} ========== MIGRATION START ==========", this);
            Log.Info($"{LogPrefix} ========================================", this);
            Log.Info($"{LogPrefix} Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC", this);

            Log.Info($"{LogPrefix} Resetting MigrationProgressTracker...", this);
            MigrationProgressTracker.Reset();
            MigrationProgressTracker.IsRunning = true;
            MigrationProgressTracker.StartTime = DateTime.UtcNow;
            Log.Info($"{LogPrefix} MigrationProgressTracker reset complete", this);
            Log.Info($"{LogPrefix}   - IsRunning: {MigrationProgressTracker.IsRunning}", this);
            Log.Info($"{LogPrefix}   - StartTime: {MigrationProgressTracker.StartTime}", this);

            Log.Info($"{LogPrefix} Entering SecurityDisabler context for entire migration...", this);

            using (new SecurityDisabler())
            {
                Log.Info($"{LogPrefix} SecurityDisabler context active", this);

                try
                {
                    var rootItemId = "{0DE95AE4-41AB-4D01-9EB0-67441B7C2450}";
                    Log.Info($"{LogPrefix} Target Root Item ID: {rootItemId}", this);
                    Log.Info($"{LogPrefix} Target Database: {_webDatabase?.Name ?? "NULL"}", this);

                    if (_webDatabase == null)
                    {
                        Log.Error($"{LogPrefix} CRITICAL: _webDatabase is NULL!", this);
                        MigrationProgressTracker.ErrorMessage = "Database is null";
                        return;
                    }

                    Log.Info($"{LogPrefix} Attempting to get root item...", this);
                    Item rootItem = null;

                    try
                    {
                        var id = new ID(rootItemId);
                        Log.Info($"{LogPrefix} Created Sitecore ID object: {id}", this);
                        Log.Info($"{LogPrefix} Calling _webDatabase.GetItem(id)...", this);

                        rootItem = _webDatabase.GetItem(id);

                        Log.Info($"{LogPrefix} GetItem() returned: {(rootItem == null ? "NULL" : "Item object")}", this);
                    }
                    catch (Exception getItemEx)
                    {
                        Log.Error($"{LogPrefix} EXCEPTION during GetItem():", this);
                        Log.Error($"{LogPrefix}   - Type: {getItemEx.GetType().FullName}", this);
                        Log.Error($"{LogPrefix}   - Message: {getItemEx.Message}", this);
                        Log.Error($"{LogPrefix}   - Stack: {getItemEx.StackTrace}", this);
                        if (getItemEx.InnerException != null)
                        {
                            Log.Error($"{LogPrefix}   - Inner Type: {getItemEx.InnerException.GetType().FullName}", this);
                            Log.Error($"{LogPrefix}   - Inner Message: {getItemEx.InnerException.Message}", this);
                            Log.Error($"{LogPrefix}   - Inner Stack: {getItemEx.InnerException.StackTrace}", this);
                        }
                        MigrationProgressTracker.ErrorMessage = $"Exception getting root item: {getItemEx.Message}";
                        return;
                    }

                    if (rootItem == null)
                    {
                        MigrationProgressTracker.ErrorMessage = "Root item not found in database.";
                        Log.Error($"{LogPrefix} CRITICAL: Root item is NULL!", this);
                        Log.Error($"{LogPrefix} Root item ID '{rootItemId}' was not found in database '{_webDatabase.Name}'", this);

                        Log.Info($"{LogPrefix} Attempting to verify database connectivity...", this);
                        try
                        {
                            Log.Info($"{LogPrefix} Getting Sitecore root item (ItemIDs.RootID)...", this);
                            var sitecoreRoot = _webDatabase.GetItem(ItemIDs.RootID);
                            Log.Info($"{LogPrefix} Sitecore root item: {(sitecoreRoot == null ? "NULL" : GetSafeItemPath(sitecoreRoot))}", this);

                            Log.Info($"{LogPrefix} Getting Content root item (ItemIDs.ContentRoot)...", this);
                            var contentRoot = _webDatabase.GetItem(ItemIDs.ContentRoot);
                            Log.Info($"{LogPrefix} Content root item: {(contentRoot == null ? "NULL" : GetSafeItemPath(contentRoot))}", this);

                            if (contentRoot != null)
                            {
                                Log.Info($"{LogPrefix} Listing children of Content root:", this);
                                var children = contentRoot.GetChildren();
                                if (children != null)
                                {
                                    foreach (Item child in children)
                                    {
                                        if (child != null)
                                        {
                                            Log.Info($"{LogPrefix}   - {child.ID}: {child.Name}", this);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception verifyEx)
                        {
                            Log.Error($"{LogPrefix} Database verification failed: {verifyEx.Message}", verifyEx, this);
                        }

                        return;
                    }

                    Log.Info($"{LogPrefix} ========== ROOT ITEM DETAILS ==========", this);
                    LogItemDetails(rootItem, "ROOT");

                    Log.Info($"{LogPrefix} Starting item count in tree...", this);
                    var countStartTime = DateTime.UtcNow;
                    var totalItemCount = CountItemsInTree(rootItem);
                    var countDuration = DateTime.UtcNow - countStartTime;

                    MigrationProgressTracker.TotalItems = totalItemCount;
                    Log.Info($"{LogPrefix} Item count complete:", this);
                    Log.Info($"{LogPrefix}   - Total items found: {totalItemCount}", this);
                    Log.Info($"{LogPrefix}   - Count duration: {countDuration.TotalSeconds:F2} seconds", this);

                    if (totalItemCount == 0)
                    {
                        MigrationProgressTracker.ErrorMessage = "No items found in tree.";
                        Log.Warn($"{LogPrefix} WARNING: No items found in tree! Root item has no children.", this);
                        return;
                    }

                    Log.Info($"{LogPrefix} ========== STARTING RECURSIVE PROCESSING ==========", this);
                    var processStartTime = DateTime.UtcNow;
                    await ProcessItemTreeRecursivelyAsync(rootItem);
                    var processDuration = DateTime.UtcNow - processStartTime;

                    Log.Info($"{LogPrefix} ========== MIGRATION COMPLETE ==========", this);
                    Log.Info($"{LogPrefix} Processing duration: {processDuration.TotalSeconds:F2} seconds", this);
                    Log.Info($"{LogPrefix} Final Statistics:", this);
                    Log.Info($"{LogPrefix}   - Total Items: {MigrationProgressTracker.TotalItems}", this);
                    Log.Info($"{LogPrefix}   - Processed Items: {MigrationProgressTracker.ProcessedItems}", this);
                    Log.Info($"{LogPrefix}   - Success Count: {MigrationProgressTracker.SuccessCount}", this);
                    Log.Info($"{LogPrefix}   - Failure Count: {MigrationProgressTracker.FailureCount}", this);
                    Log.Info($"{LogPrefix}   - Items/second: {(processDuration.TotalSeconds > 0 ? MigrationProgressTracker.ProcessedItems / processDuration.TotalSeconds : 0):F2}", this);

                    if (MigrationProgressTracker.SuccessCount == 0 && MigrationProgressTracker.FailureCount == 0)
                    {
                        MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                        Log.Warn($"{LogPrefix} WARNING: No items contained Content Hub links", this);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"{LogPrefix} ========== MIGRATION EXCEPTION ==========", this);
                    Log.Error($"{LogPrefix} Exception Type: {exception.GetType().FullName}", this);
                    Log.Error($"{LogPrefix} Exception Message: {exception.Message}", exception, this);
                    Log.Error($"{LogPrefix} Stack Trace: {exception.StackTrace}", this);

                    if (exception.InnerException != null)
                    {
                        Log.Error($"{LogPrefix} Inner Exception Type: {exception.InnerException.GetType().FullName}", this);
                        Log.Error($"{LogPrefix} Inner Exception Message: {exception.InnerException.Message}", this);
                        Log.Error($"{LogPrefix} Inner Stack Trace: {exception.InnerException.StackTrace}", this);
                    }

                    MigrationProgressTracker.ErrorMessage = exception.Message;
                }
                finally
                {
                    MigrationProgressTracker.IsRunning = false;
                    MigrationProgressTracker.EndTime = DateTime.UtcNow;

                    var totalDuration = MigrationProgressTracker.EndTime - MigrationProgressTracker.StartTime;
                    Log.Info($"{LogPrefix} ========== MIGRATION FINALIZED ==========", this);
                    Log.Info($"{LogPrefix} Total Duration: {totalDuration?.TotalSeconds:F2} seconds", this);
                    Log.Info($"{LogPrefix} End Time: {MigrationProgressTracker.EndTime:yyyy-MM-dd HH:mm:ss.fff} UTC", this);
                    Log.Info($"{LogPrefix} Error Message: {MigrationProgressTracker.ErrorMessage ?? "None"}", this);
                    Log.Info($"{LogPrefix} ========================================", this);
                }
            }

            Log.Info($"{LogPrefix} SecurityDisabler context exited", this);
        }

        private string GetSafeItemPath(Item item)
        {
            if (item == null)
            {
                return "NULL";
            }

            try
            {
                return item.Paths?.FullPath ?? $"[ID:{item.ID}]";
            }
            catch (Exception)
            {
                return $"[ID:{item.ID}]";
            }
        }

        private void LogItemDetails(Item item, string context)
        {
            if (item == null)
            {
                Log.Warn($"{LogPrefix} [{context}] Item is NULL - cannot log details", this);
                return;
            }

            try
            {
                Log.Info($"{LogPrefix} [{context}] Item Details:", this);
                Log.Info($"{LogPrefix}   - ID: {item.ID}", this);
                Log.Info($"{LogPrefix}   - Name: {item.Name ?? "NULL"}", this);
                Log.Info($"{LogPrefix}   - Path: {GetSafeItemPath(item)}", this);
                Log.Info($"{LogPrefix}   - TemplateID: {item.TemplateID}", this);
                Log.Info($"{LogPrefix}   - TemplateName: {item.TemplateName ?? "NULL"}", this);
                Log.Info($"{LogPrefix}   - Language: {item.Language?.Name ?? "NULL"}", this);
                Log.Info($"{LogPrefix}   - Version: {item.Version?.Number ?? -1}", this);
                Log.Info($"{LogPrefix}   - HasChildren: {item.HasChildren}", this);

                try
                {
                    var childCount = item.Children?.Count ?? -1;
                    Log.Info($"{LogPrefix}   - ChildCount: {childCount}", this);
                }
                catch (Exception childEx)
                {
                    Log.Warn($"{LogPrefix}   - ChildCount: EXCEPTION - {childEx.Message}", this);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} [{context}] Exception logging item details: {ex.Message}", ex, this);
            }
        }

        private int CountItemsInTree(Item rootItem)
        {
            Log.Info($"{LogPrefix} [CountItemsInTree] START", this);

            if (rootItem == null)
            {
                Log.Warn($"{LogPrefix} [CountItemsInTree] rootItem is NULL, returning 0", this);
                return 0;
            }

            Log.Info($"{LogPrefix} [CountItemsInTree] Root: {rootItem.ID} ({rootItem.Name})", this);

            int count = 0;
            int nullItems = 0;
            int exceptions = 0;
            var queue = new Queue<Item>();
            queue.Enqueue(rootItem);

            Log.Info($"{LogPrefix} [CountItemsInTree] Starting BFS traversal...", this);

            using (new SecurityDisabler())
            {
                while (queue.Count > 0)
                {
                    var currentItem = queue.Dequeue();

                    if (currentItem == null)
                    {
                        nullItems++;
                        continue;
                    }

                    count++;

                    if (count % 500 == 0)
                    {
                        Log.Info($"{LogPrefix} [CountItemsInTree] Progress: {count} items counted, queue size: {queue.Count}", this);
                    }

                    try
                    {
                        var children = currentItem.GetChildren();

                        if (children != null && children.Count > 0)
                        {
                            foreach (Item child in children)
                            {
                                if (child != null)
                                {
                                    queue.Enqueue(child);
                                }
                                else
                                {
                                    nullItems++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions++;
                        Log.Warn($"{LogPrefix} [CountItemsInTree] Exception getting children for {currentItem.ID}: {ex.Message}", this);
                    }
                }
            }

            Log.Info($"{LogPrefix} [CountItemsInTree] COMPLETE:", this);
            Log.Info($"{LogPrefix}   - Total count: {count}", this);
            Log.Info($"{LogPrefix}   - Null items encountered: {nullItems}", this);
            Log.Info($"{LogPrefix}   - Exceptions encountered: {exceptions}", this);

            return count;
        }

        private async Task ProcessItemTreeRecursivelyAsync(Item item)
        {
            Log.Info($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] START", this);

            if (item == null)
            {
                Log.Error($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] CRITICAL: Input item is NULL!", this);
                return;
            }

            Log.Info($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] Starting with: {item.ID} ({item.Name})", this);

            var queue = new Queue<Item>();
            queue.Enqueue(item);

            int processedCount = 0;
            int nullDequeues = 0;
            int processExceptions = 0;
            int childExceptions = 0;
            int pathExceptions = 0;

            Log.Info($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] Beginning queue processing...", this);

            using (new SecurityDisabler())
            {
                while (queue.Count > 0)
                {
                    var currentItem = queue.Dequeue();

                    if (currentItem == null)
                    {
                        nullDequeues++;
                        continue;
                    }

                    processedCount++;

                    if (processedCount % 100 == 0)
                    {
                        Log.Info($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] Progress: {processedCount} processed, queue: {queue.Count}, success: {MigrationProgressTracker.SuccessCount}, fail: {MigrationProgressTracker.FailureCount}, pathErrors: {pathExceptions}", this);
                    }

                    try
                    {
                        await ProcessSingleItemAsync(currentItem);
                    }
                    catch (ArgumentNullException argNullEx)
                    {
                        processExceptions++;
                        Log.Error($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] ArgumentNullException for item {currentItem.ID}: {argNullEx.ParamName} - {argNullEx.Message}", this);
                        MigrationProgressTracker.ProcessedItems++;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        processExceptions++;
                        Log.Error($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] Exception processing item {currentItem.ID}: {ex.Message}", this);
                        MigrationProgressTracker.ProcessedItems++;
                        continue;
                    }

                    try
                    {
                        var children = currentItem.GetChildren();

                        if (children != null)
                        {
                            foreach (Item child in children)
                            {
                                if (child != null)
                                {
                                    queue.Enqueue(child);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        childExceptions++;
                        Log.Error($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] Exception getting children for {currentItem.ID}: {ex.Message}", this);
                    }
                }
            }

            Log.Info($"{LogPrefix} [ProcessItemTreeRecursivelyAsync] COMPLETE:", this);
            Log.Info($"{LogPrefix}   - Items processed: {processedCount}", this);
            Log.Info($"{LogPrefix}   - Null dequeues: {nullDequeues}", this);
            Log.Info($"{LogPrefix}   - Process exceptions: {processExceptions}", this);
            Log.Info($"{LogPrefix}   - Child exceptions: {childExceptions}", this);
            Log.Info($"{LogPrefix}   - Path exceptions: {pathExceptions}", this);
        }

        private async Task ProcessSingleItemAsync(Item item)
        {
            if (item == null)
            {
                Log.Error($"{LogPrefix} [ProcessSingleItemAsync] CRITICAL: item parameter is NULL!", this);
                throw new ArgumentNullException(nameof(item), "Item cannot be null");
            }

            var itemId = item.ID.ToString();
            var itemName = item.Name ?? "UNKNOWN";

            using (new SecurityDisabler())
            {
                // Try to get FullPath for logging only - don't fail if it doesn't work
                string fullPath = $"[ID:{itemId}]";
                try
                {
                    if (item.Paths != null)
                    {
                        fullPath = item.Paths.FullPath ?? fullPath;
                    }
                }
                catch (Exception pathEx)
                {
                    Log.Debug($"{LogPrefix} [ProcessSingleItemAsync] Could not get FullPath for {itemId}, using ID: {pathEx.Message}", this);
                    // Continue processing - path is only for display
                }

                MigrationProgressTracker.CurrentItem = fullPath;

                try
                {
                    // Read all fields
                    try
                    {
                        item.Fields.ReadAll();
                    }
                    catch (Exception readEx)
                    {
                        Log.Error($"{LogPrefix} [ProcessSingleItemAsync] Exception in Fields.ReadAll() for {itemId}: {readEx.Message}", this);
                        MigrationProgressTracker.ProcessedItems++;
                        return;
                    }

                    // Extract field data
                    List<(string TypeKey, string Value, string InheritedValue, string Name)> fieldData;

                    try
                    {
                        var fields = item.Fields;
                        if (fields == null)
                        {
                            Log.Warn($"{LogPrefix} [ProcessSingleItemAsync] item.Fields is NULL for {itemId}", this);
                            MigrationProgressTracker.ProcessedItems++;
                            return;
                        }

                        fieldData = fields
                            .Cast<Field>()
                            .Select(f =>
                            {
                                if (f == null)
                                {
                                    return (TypeKey: string.Empty, Value: string.Empty, InheritedValue: string.Empty, Name: string.Empty);
                                }
                                return (
                                    TypeKey: f.TypeKey ?? string.Empty,
                                    Value: f.Value ?? string.Empty,
                                    InheritedValue: f.InheritedValue ?? string.Empty,
                                    Name: f.Name ?? string.Empty
                                );
                            })
                            .ToList();
                    }
                    catch (Exception fieldEx)
                    {
                        Log.Error($"{LogPrefix} [ProcessSingleItemAsync] Exception extracting field data for {itemId}: {fieldEx.Message}", this);
                        MigrationProgressTracker.ProcessedItems++;
                        return;
                    }

                    // Extract public links
                    List<string> publicLinks;
                    try
                    {
                        publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);
                    }
                    catch (Exception linkEx)
                    {
                        Log.Error($"{LogPrefix} [ProcessSingleItemAsync] Exception extracting public links for {itemId}: {linkEx.Message}", this);
                        MigrationProgressTracker.ProcessedItems++;
                        return;
                    }

                    bool noLinks = publicLinks == null || publicLinks.Count == 0;
                    bool hasEndpoint = !string.IsNullOrWhiteSpace(_contentHubEndpoint);

                    if (noLinks && hasEndpoint)
                    {
                        MigrationProgressTracker.ProcessedItems++;
                        return;
                    }

                    bool hasContentHubLinks = HasContentHubLinks(publicLinks, _contentHubEndpoint);

                    if (string.IsNullOrWhiteSpace(_contentHubEndpoint) || hasContentHubLinks)
                    {
                        Log.Info($"{LogPrefix} [ProcessSingleItemAsync] Sending item {itemId} ({itemName}) with {publicLinks?.Count ?? 0} links...", this);

                        try
                        {
                            await SendItemToAssetUsageServiceAsync(item, fullPath);
                            MigrationProgressTracker.SuccessCount++;
                            Log.Info($"{LogPrefix} [ProcessSingleItemAsync] SUCCESS: Item {itemId} sent (total: {MigrationProgressTracker.SuccessCount})", this);
                        }
                        catch (Exception sendEx)
                        {
                            Log.Error($"{LogPrefix} [ProcessSingleItemAsync] FAILED to send item {itemId}: {sendEx.Message}", this);
                            MigrationProgressTracker.FailureCount++;
                        }
                    }

                    MigrationProgressTracker.ProcessedItems++;
                }
                catch (Exception ex)
                {
                    Log.Error($"{LogPrefix} [ProcessSingleItemAsync] Unhandled exception for item {itemId}: {ex.Message}", this);
                    MigrationProgressTracker.ProcessedItems++;
                }
            }
        }

        private bool HasContentHubLinks(List<string> publicLinks, string contentHubEndpoint)
        {
            if (publicLinks == null || publicLinks.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(contentHubEndpoint))
            {
                return false;
            }

            return publicLinks.Any(link =>
                !string.IsNullOrWhiteSpace(link) &&
                link.IndexOf(contentHubEndpoint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task SendItemToAssetUsageServiceAsync(Item item, string itemPath = null)
        {
            if (item == null)
            {
                Log.Error($"{LogPrefix} [SendItemToAssetUsageServiceAsync] CRITICAL: item is NULL!", this);
                throw new ArgumentNullException(nameof(item), "Item cannot be null");
            }

            var itemId = item.ID.ToString();

            // Use provided path or try to get it, fallback to ID
            if (string.IsNullOrEmpty(itemPath))
            {
                try
                {
                    itemPath = item.Paths?.FullPath ?? $"[ID:{itemId}]";
                }
                catch
                {
                    itemPath = $"[ID:{itemId}]";
                }
            }

            using (new SecurityDisabler())
            {
                try
                {
                    // Read all fields
                    item.Fields.ReadAll();

                    // Extract field data
                    var fieldData = item.Fields
                        .Cast<Field>()
                        .Select(f => (
                            TypeKey: f.TypeKey ?? string.Empty,
                            Value: f.Value ?? string.Empty,
                            InheritedValue: f.InheritedValue ?? string.Empty,
                            Name: f.Name ?? string.Empty
                        ))
                        .ToList();

                    // Extract asset IDs
                    var assetIds = _assetExtractionService.ExtractAssetIdsFromFieldData(fieldData);

                    // Extract public links
                    var publicLinks = _assetExtractionService.ExtractPublicLinksFromAnyFieldData(fieldData);

                    // Check if we have anything to send
                    bool noAssetIds = assetIds == null || assetIds.Count == 0;
                    bool noPublicLinks = publicLinks == null || publicLinks.Count == 0;

                    if (noAssetIds && noPublicLinks)
                    {
                        Log.Info($"{LogPrefix} [SendItemToAssetUsageServiceAsync] No asset IDs or public links for {itemId}, skipping", this);
                        return;
                    }

                    // Build payload
                    var payload = new AssetUsageEvent
                    {
                        PublicLinks = publicLinks ?? new List<string>(),
                        ItemId = itemId,
                        ItemPath = itemPath,
                        ItemName = item.Name ?? string.Empty,
                        TemplateName = item.TemplateName ?? string.Empty,
                        Language = item.Language?.Name ?? string.Empty,
                        Version = item.Version?.Number ?? 0,
                        PublishedAtUtc = DateTime.UtcNow,
                        AssetIds = assetIds ?? new List<string>(),
                        TargetDatabase = "web"
                    };

                    Log.Info($"{LogPrefix} [SendItemToAssetUsageServiceAsync] Sending: {itemId} | Links: {payload.PublicLinks.Count} | Assets: {payload.AssetIds.Count}", this);

                    // Send to service
                    await _client.SendAsync(payload);

                    Log.Info($"{LogPrefix} [SendItemToAssetUsageServiceAsync] SUCCESS: {itemId}", this);
                }
                catch (Exception ex)
                {
                    Log.Error($"{LogPrefix} [SendItemToAssetUsageServiceAsync] FAILED {itemId}: {ex.GetType().Name}: {ex.Message}", this);
                    if (ex.InnerException != null)
                    {
                        Log.Error($"{LogPrefix}   - Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", this);
                    }
                    throw;
                }
            }
        }
    }
}