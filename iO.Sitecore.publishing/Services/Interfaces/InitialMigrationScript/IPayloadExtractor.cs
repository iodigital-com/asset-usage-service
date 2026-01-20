using iO.Sitecore.Publishing.Models;
using Sitecore.Data.Items;
using System.Collections.Generic;

namespace iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript
{
    public interface IPayloadExtractor
    {
        List<AssetUsageEvent> ExtractPayloads(Item[] items);
    }
}