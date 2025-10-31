using Sitecore.Data.Events;
using Sitecore.Publishing.Pipelines.PublishItem;
using System;
using System.Threading.Tasks;

namespace iO.Sitecore.Publishing.Services
{
    public interface IPublishTelemetryService
    {
        void ProcessItemProcessing(ItemProcessingEventArgs eventArgs);
        void ProcessItemProcessed(ItemProcessedEventArgs eventArgs);
        Task ProcessPublishEndAsync(EventArgs args);
        void ProcessPublishEndRemote(PublishEndRemoteEventArgs eventArgs);
    }
}