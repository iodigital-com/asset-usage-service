using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public class PayloadSender : IPayloadSender
    {
        private readonly AssetUsageServiceClient _client;
        private readonly int _maxConcurrency;

        public PayloadSender(AssetUsageServiceClient client, int maxConcurrency)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _maxConcurrency = maxConcurrency;
        }

        public async Task SendPayloadsAsync(List<AssetUsageEvent> payloads)
        {
            if (payloads == null || payloads.Count == 0)
            {
                return;
            }

            await Task.Run(() => SendPayloadsInParallel(payloads));
        }

        private void SendPayloadsInParallel(List<AssetUsageEvent> payloads)
        {
            Parallel.ForEach(
                payloads,
                new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency },
                payload => SendSinglePayload(payload));
        }

        private void SendSinglePayload(AssetUsageEvent payload)
        {
            try
            {
                _client.SendAsync(payload).GetAwaiter().GetResult();
                MigrationProgressTracker.IncrementSuccessCount();
            }
            catch
            {
                MigrationProgressTracker.IncrementFailureCount();
            }
            finally
            {
                MigrationProgressTracker.IncrementProcessedItems();
            }
        }
    }
}