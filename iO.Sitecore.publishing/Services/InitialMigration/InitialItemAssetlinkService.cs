using iO.Sitecore.publishing.Services.Interfaces;
using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using iO.Sitecore.Publishing.Configuration;
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Interfaces;
using iO.Sitecore.Publishing.Interfaces.Services;
using iO.Sitecore.Publishing.Models;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using System;
using System.Net;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public class InitialItemAssetLinkService
    {
        private const string LogPrefix = "[InitialItemAssetLinkService]";

        private readonly IMigrationConfiguration _configuration;
        private readonly IItemCollector _itemCollector;
        private readonly IPayloadExtractor _payloadExtractor;
        private readonly IPayloadSender _payloadSender;

        static InitialItemAssetLinkService()
        {
            ConfigureServicePointManager();
        }

        public InitialItemAssetLinkService()
            : this(new MigrationConfiguration())
        {
        }

        public InitialItemAssetLinkService(IMigrationConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _itemCollector = new ItemCollector(_configuration.DatabaseName);

            var loggingService = new PublishLoggingService(this);
            IAssetExtractionService assetExtractionService = new AssetExtractionService(loggingService);
            IContentHubLinkValidator linkValidator = new ContentHubLinkValidator(_configuration.ContentHubEndpoint);

            _payloadExtractor = new PayloadExtractor(assetExtractionService, linkValidator);
            _payloadSender = new PayloadSender(new AssetUsageServiceClient(), _configuration.MaxConcurrency);
        }

        public InitialItemAssetLinkService(
            IMigrationConfiguration configuration,
            IItemCollector itemCollector,
            IPayloadExtractor payloadExtractor,
            IPayloadSender payloadSender)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _itemCollector = itemCollector ?? throw new ArgumentNullException(nameof(itemCollector));
            _payloadExtractor = payloadExtractor ?? throw new ArgumentNullException(nameof(payloadExtractor));
            _payloadSender = payloadSender ?? throw new ArgumentNullException(nameof(payloadSender));
        }

        public async Task ExecuteMigrationAsync()
        {
            Log.Info($"{LogPrefix} Migration started", this);

            InitializeProgressTracker();

            try
            {
                await ExecuteMigrationPhasesAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} Migration failed: {ex.Message}", ex, this);
                MigrationProgressTracker.ErrorMessage = ex.Message;
            }
            finally
            {
                FinalizeProgressTracker();
            }
        }

        private static void ConfigureServicePointManager()
        {
            ServicePointManager.DefaultConnectionLimit = 200;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
        }

        private static void InitializeProgressTracker()
        {
            MigrationProgressTracker.Reset();
            MigrationProgressTracker.IsRunning = true;
            MigrationProgressTracker.StartTime = DateTime.UtcNow;
        }

        private static void FinalizeProgressTracker()
        {
            MigrationProgressTracker.IsRunning = false;
            MigrationProgressTracker.EndTime = DateTime.UtcNow;
        }

        private async Task ExecuteMigrationPhasesAsync()
        {
            var items = ExecuteCollectionPhase();
            if (items == null || items.Length == 0)
            {
                MigrationProgressTracker.ErrorMessage = "No items found.";
                return;
            }

            var payloads = ExecuteExtractionPhase(items);
            items = null;

            if (payloads == null || payloads.Count == 0)
            {
                MigrationProgressTracker.ErrorMessage = "No items with Content Hub links found";
                return;
            }

            await ExecuteSendingPhaseAsync(payloads);

            Log.Info($"{LogPrefix} Migration completed: {MigrationProgressTracker.SuccessCount} success, {MigrationProgressTracker.FailureCount} failed", this);
        }

        private Item[] ExecuteCollectionPhase()
        {
            MigrationProgressTracker.CurrentPhase = 1;
            MigrationProgressTracker.PhaseDescription = "Counting items";

            var items = _itemCollector.CollectItems(_configuration.RootItemId);

            MigrationProgressTracker.TotalItems = items.Length;
            MigrationProgressTracker.ProcessedItems = items.Length;

            return items;
        }

        private System.Collections.Generic.List<AssetUsageEvent> ExecuteExtractionPhase(Item[] items)
        {
            MigrationProgressTracker.CurrentPhase = 2;
            MigrationProgressTracker.PhaseDescription = "Extracting assets";
            MigrationProgressTracker.ProcessedItems = 0;

            var payloads = _payloadExtractor.ExtractPayloads(items);

            MigrationProgressTracker.ExtractedCount = payloads.Count;

            return payloads;
        }

        private async Task ExecuteSendingPhaseAsync(System.Collections.Generic.List<AssetUsageEvent> payloads)
        {
            MigrationProgressTracker.CurrentPhase = 3;
            MigrationProgressTracker.PhaseDescription = "Sending to service";
            MigrationProgressTracker.ProcessedItems = 0;
            MigrationProgressTracker.TotalItems = payloads.Count;

            await _payloadSender.SendPayloadsAsync(payloads);
        }
    }
}