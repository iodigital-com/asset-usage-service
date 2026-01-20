using iO.Sitecore.Publishing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript
{
    public interface IPayloadSender
    {
        Task SendPayloadsAsync(List<AssetUsageEvent> payloads);
    }
}
